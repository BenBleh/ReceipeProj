import copy
import gzip
import hashlib
import http.client
import io
import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
from unittest.mock import patch

import requests
from requests.adapters import HTTPAdapter
from PIL import Image

from drafts import (Capture, Catalog, DraftError, caption_draft, source_key,
                    validate_export, website_draft)
from live_sources import (InstagramContext, LiveImporter, PublicAdapter,
                          public_address, public_url, tiktok_identity)
from server import Handler, ThreadingHTTPServer
from source_images import preview_jpeg, probe_thumbnail


SOURCE = "https://example.com/recipe"
CAPTION = ("Golden carrots\nIngredients:\n- 500g carrots\n- 1 tbsp oil\n"
           "Method:\n1. Slice the carrots lengthwise.\n2. Roast until tender.")


def ingredient_text(result):
    return [item["FQIngrediantDescription"] for item in result["draft"]["Ingredients"]]


class DraftTests(unittest.TestCase):
    def test_unicode_separators_and_exact_quantities(self):
        result = caption_draft(CAPTION.replace("\n", "\u2028"), SOURCE)
        self.assertEqual(["500g carrots", "1 tbsp oil"], ingredient_text(result))
        self.assertEqual([0, 1], [step["Num"] for step in result["draft"]["Steps"]])
        validate_export(result["draft"])

    def test_promotional_intro_and_optional_garnish(self):
        result = caption_draft(
            "Back with another meal for $5!\nCRISPY CHICKEN BAO BUNS\n"
            "Ingredients:\n- 300g chicken thighs\n- 4 bao buns\n"
            "Optional: coriander and sesame seeds\nMethod:\n"
            "1. Cook the chicken until done.\n2. Fill the buns and serve.\n", SOURCE)
        self.assertEqual("CRISPY CHICKEN BAO BUNS", result["draft"]["Title"])
        self.assertIn("Optional: coriander and sesame seeds", ingredient_text(result))

    def test_inline_fractions_ranges_and_numbered_method(self):
        result = caption_draft(
            "Cheddar scones\nIngredients: 2 & 1/2 cups flour 3/4 cup yoghurt "
            "100-150g cheddar Method: 1. Mix the ingredients together. "
            "2. Bake until golden.", SOURCE)
        self.assertEqual(["2 & 1/2 cups flour", "3/4 cup yoghurt", "100-150g cheddar"],
                         ingredient_text(result))
        self.assertEqual(2, len(result["draft"]["Steps"]))
        self.assertTrue(any("heuristic" in warning for warning in result["warnings"]))

    def test_collapsed_component_headings(self):
        result = caption_draft(
            "Chicken and broth  Broth  - 500ml water - 1 tbsp stock  "
            "Chicken  - 200g chicken Method: 1. Simmer everything until cooked.", SOURCE)
        draft = result["draft"]
        self.assertEqual(["Broth", "Chicken"], [g["Name"] for g in draft["IngredientGroups"]])
        self.assertEqual([0, 1], [g["Order"] for g in draft["IngredientGroups"]])
        self.assertEqual(draft["IngredientGroups"][1]["Id"], draft["Ingredients"][-1]["GroupId"])
        validate_export(draft)

    def test_nutrition_servings_hashtags_and_signoff_are_not_recipe_items(self):
        result = caption_draft(
            "CREAMY CHICKEN\n481 kcals • 39g PRO, 63g CHO, 15g FAT\nServes 2\n"
            "Ingredients:\n- 300g chicken\nMethod:\n"
            "1. Cook the chicken until done. #dinner #recipe\n2. Enjoy!", SOURCE)
        self.assertEqual(["300g chicken"], ingredient_text(result))
        self.assertEqual(["Cook the chicken until done."],
                         [s["Instructions"] for s in result["draft"]["Steps"]])
        self.assertIn("#dinner #recipe", result["unassigned"])
        self.assertIn("481 kcals", result["raw"])

    def test_ad_hashtag_before_recipe_does_not_remove_later_recipe(self):
        result = caption_draft("#ad Brand collaboration\n" + CAPTION, SOURCE)
        self.assertEqual(2, len(result["draft"]["Ingredients"]))
        self.assertEqual(2, len(result["draft"]["Steps"]))

    def test_inferred_method_boundary_is_flagged(self):
        result = caption_draft(
            "Golden carrots\nIngredients:\n- 500g carrots\n- 1 tbsp oil\n"
            "Preheat the oven to 200C.\nRoast the carrots until tender.", SOURCE)
        self.assertEqual(["500g carrots", "1 tbsp oil"], ingredient_text(result))
        self.assertEqual(1, len(result["draft"]["Steps"]))
        self.assertTrue(any("inferred" in warning for warning in result["warnings"]))

    def test_missing_method_is_not_filled(self):
        result = caption_draft("Golden carrots\nIngredients:\n- 500g carrots", SOURCE)
        self.assertEqual([], result["draft"]["Steps"])
        self.assertTrue(any("No cooking method" in warning for warning in result["warnings"]))
        with self.assertRaises(DraftError):
            validate_export(result["draft"])

    def test_jsonld_sections_not_actions_and_water_is_flagged(self):
        recipe = {
            "@context": "https://schema.org", "@type": "Recipe", "name": "Test focaccia",
            "recipeIngredient": ["500g flour (plus extra)", "1 tsp yeast"],
            "recipeInstructions": [
                {"@type": "HowToSection", "name": "Dough", "itemListElement": [
                    {"@type": "HowToStep", "text": "Mix with 400ml warm water."}]},
                {"@type": "HowToSection", "name": "Baking", "itemListElement": [
                    {"@type": "HowToStep", "text": "Bake until cooked."}]},
            ],
        }
        html = '<script type="application/ld+json">' + json.dumps(recipe) + "</script>"
        with patch("requests.sessions.Session.request", side_effect=AssertionError("No network")):
            result = website_draft(html, SOURCE)
        self.assertEqual(recipe["recipeIngredient"], ingredient_text(result))
        self.assertEqual(2, len(result["draft"]["Steps"]))
        self.assertEqual(["Dough", "Baking"], result["evidence"]["method_sections"])
        self.assertTrue(any("water" in warning for warning in result["warnings"]))

    def test_ambiguous_jsonld_is_rejected(self):
        recipe = {"@type": "Recipe", "name": "One", "recipeIngredient": ["1 carrot"]}
        with self.assertRaisesRegex(DraftError, "unambiguous"):
            website_draft('<script type="application/ld+json">' +
                          json.dumps([recipe, recipe]) + "</script>", SOURCE)

    def test_export_rejects_malformed_values_explicitly(self):
        valid = caption_draft(CAPTION, SOURCE)["draft"]
        for field, value in [("Title", ""), ("Title", "Two\nlines"), ("Ingredients", []),
                             ("Steps", []), ("ImageData", {}), ("Id", "not-an-id")]:
            with self.subTest(field=field, value=value):
                draft = copy.deepcopy(valid)
                draft[field] = value
                with self.assertRaises(DraftError):
                    validate_export(draft)
        for group_id in [{}, [], "missing-group"]:
            with self.subTest(group_id=group_id):
                draft = copy.deepcopy(valid)
                draft["Ingredients"][0]["GroupId"] = group_id
                with self.assertRaisesRegex(DraftError, "unknown group"):
                    validate_export(draft)
        draft = copy.deepcopy(valid)
        draft["Steps"][0]["Num"] = False
        with self.assertRaises(DraftError):
            validate_export(draft)

    def test_unknown_url_has_explicit_offline_fallback(self):
        catalog = Catalog.__new__(Catalog)
        catalog.captures, catalog.aliases = {}, {}
        with patch("requests.sessions.Session.request", side_effect=AssertionError("No network")):
            with self.assertRaisesRegex(DraftError, "No live request was made"):
                catalog.build("https://example.com/not-captured")

    def test_capture_integrity_and_requested_video_identity(self):
        url = "https://www.tiktok.com/@creator/video/123"
        detail = {"__DEFAULT_SCOPE__": {"webapp.video-detail": {
            "statusCode": 0, "itemInfo": {"itemStruct": {"id": "999", "desc": CAPTION}}}}}
        raw = ('<script id="__UNIVERSAL_DATA_FOR_REHYDRATION__">' +
               json.dumps(detail) + "</script>").encode()
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "capture.html"
            path.write_bytes(raw)
            catalog = Catalog.__new__(Catalog)
            catalog.captures, catalog.aliases = {}, {}
            catalog.add(Capture(url, "Sample", "tiktok", path, hashlib.sha256(raw).hexdigest(), "123"))
            with self.assertRaisesRegex(DraftError, "identity/status"):
                catalog.build(url)
            path.write_bytes(raw + b"changed")
            with self.assertRaisesRegex(DraftError, "integrity"):
                catalog.build(url)

    def test_source_identity_and_credential_rejection(self):
        self.assertEqual(source_key("https://www.instagram.com/reel/ABC_123/?igsh=x"),
                         source_key("https://instagram.com/p/ABC_123/"))
        with self.assertRaises(DraftError):
            source_key("https://user:password@example.com/recipe")
        with self.assertRaisesRegex(DraftError, "malformed"):
            source_key("https://[")


class HttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        cls.server.catalog = Catalog.__new__(Catalog)
        cls.server.catalog.captures, cls.server.catalog.aliases = {}, {}
        cls.server.live_importer = LiveImporter()
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join()

    def request(self, method, path, body=None, headers=None):
        client = http.client.HTTPConnection("127.0.0.1", self.server.server_port, timeout=5)
        try:
            encoded = json.dumps(body).encode() if body is not None else None
            client.request(method, path, encoded,
                           headers if headers is not None else {"Content-Type": "application/json"})
            response = client.getresponse()
            return response.status, json.loads(response.read())
        finally:
            client.close()

    def test_paste_edit_review_and_export_without_storage(self):
        status, result = self.request("POST", "/api/draft",
                                      {"mode": "paste", "source": SOURCE, "caption": CAPTION})
        self.assertEqual(200, status)
        draft = result["draft"]
        draft["Title"] = "Edited carrots"
        status, _ = self.request("POST", "/api/export", {"draft": draft, "reviewed": False})
        self.assertEqual(422, status)
        status, result = self.request("POST", "/api/export", {"draft": draft, "reviewed": True})
        self.assertEqual(200, status)
        self.assertEqual("Edited carrots", result["draft"]["Title"])
        self.assertEqual({}, self.server.catalog.captures)

    def test_partial_caption_export_is_blocked(self):
        _, result = self.request("POST", "/api/draft", {
            "mode": "paste", "caption": "Golden carrots\nIngredients:\n- 500g carrots"})
        status, result = self.request("POST", "/api/export", {
            "draft": result["draft"], "reviewed": True})
        self.assertEqual(422, status)
        self.assertIn("one method step", result["error"])

    def test_host_origin_body_and_content_type_guards(self):
        cases = [
            ("GET", "/api/health", None, {"Host": "not-local.example"}, 403),
            ("GET", "/api/health", None, {"Origin": "https://not-local.example"}, 403),
            ("POST", "/api/draft", {}, {"Content-Type": "text/plain"}, 415),
            ("POST", "/api/draft", None,
             {"Content-Type": "application/json", "Content-Length": "100001"}, 413),
        ]
        for method, path, body, headers, expected in cases:
            with self.subTest(headers=headers):
                self.assertEqual(expected, self.request(method, path, body, headers)[0])

    def test_live_mode_uses_new_url_not_capture_catalog(self):
        result = caption_draft(CAPTION, SOURCE)
        with patch.object(self.server.live_importer, "build", return_value=result) as build:
            status, received = self.request("POST", "/api/draft", {"mode": "live", "source": SOURCE})
        self.assertEqual(200, status)
        self.assertEqual(result["draft"], received["draft"])
        build.assert_called_once_with(SOURCE)
        self.assertEqual({}, self.server.catalog.captures)

    def test_live_error_is_explicit_and_does_not_make_a_draft(self):
        with patch.object(self.server.live_importer, "build", side_effect=DraftError("HTTP 403; paste text")):
            status, result = self.request("POST", "/api/draft", {"mode": "live", "source": SOURCE})
        self.assertEqual(422, status)
        self.assertIn("paste text", result["error"])
        self.assertNotIn("draft", result)

    def test_thumbnail_is_explicitly_requested_and_requires_a_boolean(self):
        result = caption_draft(CAPTION, SOURCE)
        result["image"] = {"status": "unavailable", "reason": "Policy exclusion"}
        with patch.object(self.server.live_importer, "build", return_value=result) as build:
            status, received = self.request("POST", "/api/draft",
                                            {"mode": "live", "source": SOURCE, "include_image": True})
        self.assertEqual(200, status)
        self.assertEqual(result["image"], received["image"])
        build.assert_called_once_with(SOURCE, include_image=True)
        status, _ = self.request("POST", "/api/draft",
                                {"mode": "live", "source": SOURCE, "include_image": "true"})
        self.assertEqual(422, status)


def response(status=200, body="", **headers):
    value = requests.Response()
    value.status_code = status
    value._content = body.encode() if isinstance(body, str) else body
    value._content_consumed = True
    value.headers.update({"Content-Type": "text/html; charset=utf-8", **headers})
    value.encoding = "utf-8"
    value.request = requests.Request("GET", SOURCE).prepare()
    value.url = SOURCE
    return value


class LiveTests(unittest.TestCase):
    def test_private_mixed_and_reserved_dns_answers_are_rejected(self):
        for addresses in [["127.0.0.1"], ["192.168.1.1"], ["::1"], ["169.254.169.254"],
                          ["93.184.215.14", "10.0.0.1"]]:
            with self.subTest(addresses=addresses):
                answers = [(2, 1, 6, "", (ip, 443)) for ip in addresses]
                with patch("socket.getaddrinfo", return_value=answers):
                    with self.assertRaisesRegex(DraftError, "private or reserved"):
                        public_address("untrusted.example")

    def test_https_port_and_credential_limits(self):
        for url in ["http://example.com/recipe", "https://example.com:8443/recipe",
                    "https://user@example.com/recipe"]:
            with self.subTest(url=url), self.assertRaises(DraftError):
                public_url(url)

    def test_validated_address_is_pinned_with_original_tls_identity(self):
        adapter = PublicAdapter(LiveImporter())
        request = requests.Request("GET", SOURCE).prepare()
        with patch("live_sources.public_address", return_value="93.184.215.14"):
            with patch.object(adapter.poolmanager, "connection_from_host") as pool:
                adapter.get_connection_with_tls_context(request, True)
        args, kwargs = pool.call_args
        self.assertEqual(("93.184.215.14", 443, "https"), args)
        self.assertEqual("example.com", kwargs["pool_kwargs"]["assert_hostname"])
        self.assertEqual("example.com", kwargs["pool_kwargs"]["server_hostname"])
        adapter.close()

    def test_new_website_checks_robots_and_builds_source_only_draft(self):
        recipe = {"@context": "https://schema.org", "@type": "Recipe", "name": "New carrots",
                  "recipeIngredient": ["500g carrots"],
                  "recipeInstructions": [{"@type": "HowToStep", "text": "Roast until tender."}]}
        html = '<script type="application/ld+json">' + json.dumps(recipe) + "</script>"
        calls = []

        def send(request, **kwargs):
            calls.append(request.url)
            return response(body="User-agent: *\nAllow: /") if request.url.endswith("/robots.txt") else response(body=html)

        with patch.object(PublicAdapter, "send", side_effect=send):
            result = LiveImporter().build(SOURCE)
        self.assertEqual(["https://example.com/robots.txt", SOURCE], calls)
        self.assertEqual("New carrots", result["draft"]["Title"])
        self.assertTrue(result["evidence"]["live_source"])
        self.assertFalse(result["evidence"]["captured_source"])

    def test_robots_exclusion_does_not_request_recipe(self):
        with patch.object(PublicAdapter, "send", return_value=response(body="User-agent: *\nDisallow: /")) as send:
            with self.assertRaisesRegex(DraftError, "robots policy excludes"):
                LiveImporter().build(SOURCE)
        self.assertEqual(1, send.call_count)
        self.assertTrue(send.call_args.args[0].url.endswith("/robots.txt"))

    def test_short_tiktok_homepage_is_not_followed(self):
        with patch.object(PublicAdapter, "send", side_effect=[
                response(body="User-agent: *\nAllow: /"),
                response(302, Location="https://www.tiktok.com/")]) as send:
            with self.assertRaisesRegex(DraftError, "does not identify a video"):
                LiveImporter().build("https://vm.tiktok.com/ABC123/")
        self.assertEqual(2, send.call_count)

    def test_direct_tiktok_identity_and_unknown_post(self):
        url = "https://www.tiktok.com/@creator/video/123"
        detail = {"__DEFAULT_SCOPE__": {"webapp.video-detail": {
            "statusCode": 0, "itemInfo": {"itemStruct": {"id": "123", "desc": CAPTION}}}}}
        html = '<script id="__UNIVERSAL_DATA_FOR_REHYDRATION__">' + json.dumps(detail) + "</script>"
        with patch.object(PublicAdapter, "send", side_effect=[
                response(body="User-agent: *\nAllow: /"), response(body=html)]):
            result = LiveImporter().build(url)
        self.assertEqual("tiktok", result["evidence"]["platform"])
        self.assertEqual(2, len(result["draft"]["Steps"]))
        self.assertEqual("123", tiktok_identity(url))
        self.assertEqual("123", tiktok_identity("https://www.tiktok.com/@/video/123"))
        with patch.object(PublicAdapter, "send", side_effect=[
                response(body="User-agent: *\nAllow: /"),
                response(302, Location="https://www.tiktok.com/@creator/video/999")]) as send:
            with self.assertRaisesRegex(DraftError, "changed or lost"):
                LiveImporter().build(url)
        self.assertEqual(2, send.call_count)

    def test_instagram_copied_graphql_session_is_protected(self):
        adapter = PublicAdapter(LiveImporter(), instagram=True)
        context = InstagramContext(adapter)
        try:
            self.assertIs(adapter, context._session.get_adapter("https://www.instagram.com/"))
            self.assertFalse(context._session.trust_env)
            with context._graphql_session(None) as session:
                self.assertIs(adapter, session.get_adapter("https://www.instagram.com/graphql/query"))
                self.assertFalse(session.trust_env)
        finally:
            context.close()

    def test_instagram_unknown_post_builds_from_caption(self):
        from types import SimpleNamespace
        post = SimpleNamespace(shortcode="NEW_123", caption=CAPTION)
        with patch("instaloader.Post.from_shortcode", return_value=post):
            result = LiveImporter().build("https://www.instagram.com/reel/NEW_123/")
        self.assertEqual("instagram", result["evidence"]["platform"])
        self.assertEqual(2, len(result["draft"]["Ingredients"]))

    def test_restricted_host_stops_without_another_request(self):
        importer = LiveImporter()
        adapter = PublicAdapter(importer)
        request = requests.Request("GET", SOURCE).prepare()
        with patch.object(HTTPAdapter, "send", return_value=response(403)) as send:
            for _ in range(2):
                with self.assertRaisesRegex(DraftError, "HTTP 403"):
                    adapter.send(request)
        self.assertEqual(1, send.call_count)
        adapter.close()

    def test_instagram_redirect_and_other_hosts_are_not_followed(self):
        adapter = PublicAdapter(LiveImporter(), instagram=True)
        request = requests.Request("POST", "https://www.instagram.com/graphql/query").prepare()
        with patch.object(HTTPAdapter, "send", return_value=response(302, Location="/accounts/login/")) as send:
            with self.assertRaisesRegex(DraftError, "HTTP 302"):
                adapter.send(request)
        self.assertEqual(1, send.call_count)
        with self.assertRaisesRegex(DraftError, "unsupported endpoint"):
            adapter.send(requests.Request("GET", "https://elsewhere.example/").prepare())
        adapter.close()

    def test_streamed_response_size_is_bounded(self):
        class Raw:
            def __init__(self, data):
                self.stream = io.BytesIO(data)

            def read1(self, size, decode_content=False):
                return self.stream.read(size)

            def close(self):
                self.stream.close()

        data = b"x" * 1_000_001
        for encoding, body in [("identity", data), ("gzip", gzip.compress(data))]:
            with self.subTest(encoding=encoding):
                value = response(**{"Content-Encoding": encoding})
                value._content = False
                value._content_consumed = False
                value.raw = Raw(body)
                adapter = PublicAdapter(LiveImporter())
                request = requests.Request("GET", "https://example.com/robots.txt").prepare()
                with patch.object(HTTPAdapter, "send", return_value=value):
                    with self.assertRaisesRegex(DraftError, "size limit"):
                        adapter.send(request)
                adapter.close()


class ImageTests(unittest.TestCase):
    cover = "https://images.example/private-looking-cover?signature=do-not-log"

    def setUp(self):
        self.sleep = patch("live_sources.time.sleep")
        self.sleep.start()
        self.addCleanup(self.sleep.stop)
        with Image.new("RGB", (40, 60), "green") as image, io.BytesIO() as output:
            image.save(output, "PNG")
            self.png = output.getvalue()

    @staticmethod
    def upstream(status=200, body=b"", content_type="text/plain", **headers):
        class Raw:
            def __init__(self, value):
                self.stream = io.BytesIO(value)

            def read1(self, size, decode_content=False):
                return self.stream.read(size)

            def close(self):
                self.stream.close()

        value = response(status, body, **{"Content-Type": content_type, **headers})
        value.raw = Raw(body.encode() if isinstance(body, str) else body)
        value._content = False
        value._content_consumed = False
        return value

    def probe(self, importer=None, candidates=None):
        return probe_thumbnail(importer or LiveImporter(),
                               [self.cover] if candidates is None else candidates,
                               SOURCE, time.monotonic() + 60, 10)

    def test_robots_403_404_and_410_are_not_image_403(self):
        for policy_status in (403, 404, 410):
            with self.subTest(policy_status=policy_status), patch.object(HTTPAdapter, "send", side_effect=[
                    self.upstream(policy_status), self.upstream(body=self.png, content_type="image/png")]) as send:
                result = self.probe()
            self.assertEqual("downloaded", result["status"])
            self.assertEqual([policy_status, 200], [event["status"] for event in result["http"]])
            self.assertEqual(["policy", "image"], [event["role"] for event in result["http"]])
            self.assertNotIn("signature", json.dumps(result))
            self.assertNotIn("private-looking", json.dumps(result))
            self.assertTrue(result["preview"].startswith("data:image/jpeg;base64,"))
            self.assertEqual(2, send.call_count)

    def test_explicit_robots_exclusion_never_requests_image(self):
        with patch.object(HTTPAdapter, "send", return_value=self.upstream(
                body="User-agent: *\nDisallow: /")) as send:
            result = self.probe()
        self.assertEqual("policy", result["failed_stage"])
        self.assertIn("excludes", result["reason"])
        self.assertEqual(1, send.call_count)

    def test_unsupported_robots_patterns_fail_closed(self):
        with patch.object(HTTPAdapter, "send", return_value=self.upstream(
                body="User-agent: *\nDisallow: /*")) as send:
            result = self.probe()
        self.assertEqual("policy", result["failed_stage"])
        self.assertIn("pattern rules", result["reason"])
        self.assertEqual(1, send.call_count)

    def test_actual_image_refusal_stops_host_and_does_not_try_another_cover(self):
        importer = LiveImporter()
        with patch.object(HTTPAdapter, "send", side_effect=[
                self.upstream(404), self.upstream(403)]) as send:
            result = self.probe(importer, [self.cover, "https://other.example/cover"])
        self.assertEqual("image_request", result["failed_stage"])
        self.assertIn("HTTP 403", result["reason"])
        self.assertIn("images.example", importer.stopped)
        self.assertEqual(2, send.call_count)

    def test_an_image_named_robots_is_still_a_resource_not_a_policy(self):
        importer = LiveImporter()
        with patch.object(HTTPAdapter, "send", side_effect=[self.upstream(404), self.upstream(403)]):
            result = self.probe(importer, ["https://images.example/robots.txt"])
        self.assertEqual("image_request", result["failed_stage"])
        self.assertIn("images.example", importer.stopped)
        self.assertEqual("image", result["http"][-1]["role"])

    def test_unreachable_authenticated_and_rate_limited_policy_still_stops(self):
        for status in (401, 429, 503):
            with self.subTest(status=status), patch.object(
                    HTTPAdapter, "send", return_value=self.upstream(status)) as send:
                result = self.probe()
            self.assertEqual("policy", result["failed_stage"])
            self.assertEqual(1, send.call_count)

    def test_image_policy_exception_is_not_reused_for_website_policy(self):
        importer = LiveImporter()
        with patch.object(HTTPAdapter, "send", side_effect=[
                self.upstream(403), self.upstream(body=self.png, content_type="image/png")]):
            self.assertEqual("downloaded", self.probe(importer)["status"])
        adapter = PublicAdapter(importer)
        self.addCleanup(adapter.close)
        with patch.object(HTTPAdapter, "send", return_value=self.upstream(403)) as send:
            with requests.Session() as session:
                session.mount("https://", adapter)
                with self.assertRaisesRegex(DraftError, "HTTP 403"):
                    importer.robots_for(self.cover, session, adapter)
        self.assertEqual(1, send.call_count)

    def test_policy_redirect_remains_a_policy_request(self):
        with patch.object(HTTPAdapter, "send", side_effect=[
                self.upstream(302, Location="/policy/current"),
                self.upstream(403), self.upstream(body=self.png, content_type="image/png")]):
            result = self.probe()
        self.assertEqual("downloaded", result["status"])
        self.assertEqual(["policy", "policy", "image"], [event["role"] for event in result["http"]])
        self.assertEqual("/[policy]", result["http"][1]["path"])

    def test_missing_metadata_and_exhausted_budget_never_make_image_requests(self):
        with patch.object(HTTPAdapter, "send") as send:
            self.assertEqual("missing", self.probe(candidates=[])["status"])
            result = probe_thumbnail(LiveImporter(), [self.cover], SOURCE, time.monotonic() + 60, 0)
        self.assertEqual("unavailable", result["status"])
        send.assert_not_called()

    def test_corrupt_mislabeled_and_oversized_images_are_rejected(self):
        for body, media in [(b"not an image", "image/jpeg"), (self.png, "text/html"),
                            (b"x" * 6_000_001, "image/jpeg")]:
            with self.subTest(media=media, size=len(body)), patch.object(HTTPAdapter, "send", side_effect=[
                    self.upstream(404), self.upstream(body=body, content_type=media)]):
                result = self.probe()
            self.assertEqual("unavailable", result["status"])
            self.assertNotIn("preview", result)

    def test_image_is_decoded_scaled_and_reencoded_without_source_metadata(self):
        with Image.new("RGB", (1800, 900)) as image, io.BytesIO() as output:
            image.save(output, "PNG")
            jpeg, width, height = preview_jpeg(output.getvalue())
        self.assertEqual((1600, 800), (width, height))
        with Image.open(io.BytesIO(jpeg)) as image:
            self.assertEqual("JPEG", image.format)
            self.assertEqual({}, image.getexif())

    def test_excessive_dimensions_are_rejected_before_full_decode(self):
        with Image.new("RGB", (8193, 1)) as image, io.BytesIO() as output:
            image.save(output, "PNG")
            with self.assertRaisesRegex(DraftError, "dimensions"):
                preview_jpeg(output.getvalue())

    def test_network_errors_do_not_expose_signed_image_urls(self):
        with patch("requests.Session.get", side_effect=requests.ConnectionError(self.cover)):
            result = self.probe()
        self.assertEqual("unavailable", result["status"])
        self.assertNotIn("do-not-log", json.dumps(result))

    def test_invalid_optional_image_metadata_is_reported_as_a_photo_failure(self):
        with patch.object(HTTPAdapter, "send", side_effect=[
                self.upstream(404), self.upstream(body=self.png, content_type="image/png")]), \
                patch("source_images.preview_jpeg", side_effect=ValueError("Invalid image metadata")):
            result = self.probe()
        self.assertEqual("decode", result["failed_stage"])
        self.assertEqual("unavailable", result["status"])

    def test_photo_metadata_failure_keeps_successful_instagram_caption(self):
        class Post:
            shortcode = "NEW_123"
            caption = CAPTION

            @property
            def url(self):
                raise DraftError("HTTP 429")

        with patch("instaloader.Post.from_shortcode", return_value=Post()):
            result = LiveImporter().build("https://www.instagram.com/reel/NEW_123/", include_image=True)
        self.assertEqual(2, len(result["draft"]["Ingredients"]))
        self.assertEqual("metadata", result["image"]["failed_stage"])

    def test_matched_social_metadata_is_forwarded_without_exposing_signed_urls(self):
        url = "https://www.tiktok.com/@creator/video/123"
        item = {"id": "123", "desc": CAPTION, "video": {"cover": self.cover, "originCover": "bad"}}
        data = {"__DEFAULT_SCOPE__": {"webapp.video-detail": {
            "statusCode": 0, "itemInfo": {"itemStruct": item}}}}
        html = '<script id="__UNIVERSAL_DATA_FOR_REHYDRATION__">' + json.dumps(data) + "</script>"
        with patch.object(PublicAdapter, "send", side_effect=[response(404), response(body=html)]), \
                patch("source_images.probe_thumbnail", return_value={"status": "missing"}) as probe:
            result = LiveImporter().build(url, include_image=True)
        self.assertEqual([self.cover, "bad"], probe.call_args.args[1])
        self.assertNotIn("do-not-log", json.dumps(result))


if __name__ == "__main__":
    unittest.main()
