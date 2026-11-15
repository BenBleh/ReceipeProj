"""Research-only website parsing and TikTok caption validation.

Uses verified cached pages plus a small, robots-checked public HTTP sample.
No accounts, browser sessions, challenge solving, media or recipe writes.
"""

import argparse
import collections
import datetime
import hashlib
import importlib.metadata
import json
import pathlib
import re
import time
import urllib.error
import urllib.parse
import urllib.robotparser
from unittest.mock import patch

from recipe_scrapers import scrape_html
from recipe_scrapers._exceptions import RecipeScrapersExceptions
from yt_dlp import YoutubeDL
from yt_dlp.extractor.tiktok import TikTokIE

from probe_sources import AGENT, Page, fetch, has_type, instructions, normalized, objects


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def field_comparison(fields, reference):
    return {
        key: ([normalized(value) for value in fields[key]]
              == [normalized(value) for value in reference[key]])
        if isinstance(fields[key], list)
        else normalized(fields[key]) == normalized(reference[key])
        for key in ["title", "ingredients", "steps"]
    }


def website_fields(html, url, saved):
    try:
        scraper = scrape_html(html, org_url=url, online=False, supported_only=False)
        fields = {
            "title": scraper.title(),
            "ingredients": scraper.ingredients(),
            "steps": scraper.instructions_list(),
        }
        if (not isinstance(fields["title"], str) or not fields["title"].strip()
                or any(not isinstance(fields[key], list) or not fields[key]
                       or any(not isinstance(item, str) or not item.strip()
                              for item in fields[key])
                       for key in ["ingredients", "steps"])):
            raise ValueError("Missing or invalid required recipe fields")
    except (RecipeScrapersExceptions, NotImplementedError, ValueError) as error:
        return {"outcome": "parser_error", "error_type": type(error).__name__}

    page = Page()
    page.feed(html)
    candidates = []
    for script in page.scripts:
        data = json.loads(script)
        candidates.extend(item for item in objects(data) if has_type(item, "Recipe"))
    result = {
        "outcome": "nonempty_recipe_fields_extracted",
        "scraper_class": type(scraper).__name__,
        "extracted_title": fields["title"],
        "ingredient_lines": len(fields["ingredients"]),
        "steps": len(fields["steps"]),
        "fields_sha256": digest(json.dumps(fields, ensure_ascii=False).encode("utf-8")),
        "independent_jsonld_candidates": len(candidates),
        "saved_counts": {"ingredients": len(saved["Ingredients"]), "steps": len(saved["Steps"])},
        "normalized_equal_to_saved": field_comparison(fields, {
            "title": saved["Title"],
            "ingredients": [item.get("FQIngrediantDescription") or "" for item in saved["Ingredients"]],
            "steps": [item.get("Instructions") or "" for item in saved["Steps"]],
        }),
    }
    if len(candidates) == 1:
        source = candidates[0]
        result["normalized_equal_to_source_jsonld"] = field_comparison(fields, {
            "title": source.get("name") or "",
            "ingredients": source.get("recipeIngredient") or [],
            "steps": instructions(source.get("recipeInstructions")),
        })
    else:
        result["source_selection_requires_review"] = True
    try:
        groups = scraper.ingredient_groups()
        grouped = [ingredient for group in groups for ingredient in group.ingredients]
        result["ingredient_groups"] = {
            "named_groups": [group.purpose for group in groups if group.purpose],
            "total_groups": len(groups),
            "covers_exact_ingredient_multiset": (
                collections.Counter(map(normalized, grouped))
                == collections.Counter(map(normalized, fields["ingredients"]))
            ),
        }
    except (RecipeScrapersExceptions, NotImplementedError, ValueError) as error:
        result["ingredient_groups"] = {"error_type": type(error).__name__}
    try:
        result["image_metadata_available"] = bool(scraper.image())
    except (RecipeScrapersExceptions, NotImplementedError, ValueError) as error:
        result["image_metadata_error_type"] = type(error).__name__
    return result


def tiktok_fields(html, url, extractor):
    expected = re.search(r"/video/(\d+)", url)
    if expected is None:
        return {"outcome": "requested_video_identity_missing"}
    match = re.search(
        r'<script[^>]+id="__UNIVERSAL_DATA_FOR_REHYDRATION__"[^>]*>(.*?)</script>',
        html, re.S,
    )
    if match is None:
        return {"outcome": "requested_post_data_unavailable"}
    detail = json.loads(match[1]).get("__DEFAULT_SCOPE__", {}).get("webapp.video-detail", {})
    item = detail.get("itemInfo", {}).get("itemStruct", {})
    if detail.get("statusCode") != 0 or item.get("id") != expected[1]:
        return {"outcome": "post_status_or_identity_rejected",
                "post_status": detail.get("statusCode"), "returned_id": item.get("id")}
    caption = item.get("desc")
    if not isinstance(caption, str) or not caption.strip():
        return {"outcome": "requested_post_has_no_caption"}
    # Exercise only metadata parsing: no native yt-dlp transport, formats or subtitles.
    mapped = extractor._parse_aweme_video_web(item, url, expected[1], extract_flat=True)
    return {
        "outcome": "requested_caption_returned",
        "requested_id": expected[1],
        "returned_id": item["id"],
        "caption_characters": len(caption),
        "caption_sha256": digest(caption.encode("utf-8")),
        "yt_dlp_description_matches_source": mapped.get("description") == caption,
        "yt_dlp_title_characters": len(mapped.get("title") or ""),
        "yt_dlp_title_is_full_caption": mapped.get("title") == caption,
    }


class PublicSample:
    def __init__(self):
        self.robots = {}
        self.trace = []
        self.host_stops = {"https://simplehomeedit.com": "Prior HTTP 403; not retried"}

    def get(self, url):
        parsed = urllib.parse.urlsplit(url)
        origin = parsed.scheme + "://" + parsed.netloc
        if parsed.scheme != "https" or parsed.username or parsed.password:
            raise ValueError("Only ordinary HTTPS source URLs are allowed")
        if origin in self.host_stops:
            return {"outcome": "host_stop_not_requested", "reason": self.host_stops[origin]}, None
        if origin not in self.robots:
            policy = {"origin": origin}
            try:
                status, _, raw, text = fetch(origin + "/robots.txt", 1_000_000)
                robot = urllib.robotparser.RobotFileParser()
                robot.parse(text.splitlines())
                policy.update(http_status=status, sha256=digest(raw))
            except urllib.error.HTTPError as error:
                policy["http_status"] = error.code
                robot = None
                if error.code != 404:
                    self.host_stops[origin] = "Robots unavailable: HTTP " + str(error.code)
            except (urllib.error.URLError, TimeoutError, OSError, ValueError) as error:
                policy["error_type"] = type(error).__name__
                robot = None
                self.host_stops[origin] = "Robots unavailable"
            self.robots[origin] = (robot, policy)
        robot, policy = self.robots[origin]
        if origin in self.host_stops:
            return {"outcome": "robots_unavailable_not_requested"}, None
        if robot is not None and not robot.can_fetch(AGENT, url):
            return {"outcome": "robots_disallowed_not_requested"}, None
        delay = (robot.crawl_delay(AGENT) or robot.crawl_delay("*") or 1) if robot else 1
        time.sleep(max(1, delay))
        event = {"source_url": url}
        self.trace.append(event)
        try:
            status, _, raw, html = fetch(url)
        except urllib.error.HTTPError as error:
            event["http_status"] = error.code
            if error.code in {401, 403, 429}:
                self.host_stops[origin] = "HTTP " + str(error.code)
            if 300 <= error.code < 400:
                target = urllib.parse.urlsplit(urllib.parse.urljoin(
                    url, error.headers.get("Location", "")))
                target_url = urllib.parse.urlunsplit(
                    (target.scheme, target.netloc, target.path, "", ""))
                event.update(outcome="redirect_not_followed", target_url=target_url,
                             identifies_video=bool(re.search(r"/video/\d+", target.path)))
            else:
                event["outcome"] = "http_error"
            return dict(event), None
        except (urllib.error.URLError, TimeoutError, OSError, ValueError) as error:
            event.update(outcome="transport_error", error_type=type(error).__name__)
            self.host_stops[origin] = "Transport failure; not retried"
            return dict(event), None
        event.update(outcome="public_html_returned", http_status=status,
                     response_bytes=len(raw), html_sha256=digest(raw))
        return dict(event), (raw, html)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--private-cache", type=pathlib.Path, required=True)
    parser.add_argument("--trial-cache", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    if args.output.exists() or args.trial_cache.exists():
        parser.error("Choose new output/cache paths; existing evidence is not overwritten")
    packages = {name: importlib.metadata.version(name) for name in
                ["recipe-scrapers", "yt-dlp", "beautifulsoup4", "extruct"]}
    if packages["recipe-scrapers"] != "15.13.0" or packages["yt-dlp"] != "2026.10.26":
        parser.error("Requires the inspected recipe-scrapers 15.13.0 and yt-dlp 2026.10.26")
    root = pathlib.Path(__file__).parent
    initial = json.loads((root / "source-probes.json").read_text(encoding="utf-8"))
    redirects = json.loads((root / "redirect-probes.json").read_text(encoding="utf-8"))
    assessment = json.loads((root / "extraction-assessment.json").read_text(encoding="utf-8"))
    corpus = json.loads((args.private_cache / "recipes.json").read_text(encoding="utf-8"))
    saved = {recipe["Id"]: recipe for recipe in corpus["recipes"]}
    first = {row["recipe_id"]: row for row in initial["results"]}
    follow = {row["recipe_id"]: row for row in redirects["results"]}
    websites = [row for row in initial["results"]
                if row["platform"] == "Website" and row["outcome"] == "structured_recipe_found"]
    report = {
        "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "snapshot_commit": initial["snapshot_commit"],
        "packages": packages,
        "policy": "Cached parsing plus bounded public HTML; no login, media, challenge solving or AI",
        "yt_dlp_scope": "Offline metadata parser only; native downloader/challenge behavior not invoked",
        "website_cached": [], "tiktok_cached": [], "website_live": [], "tiktok_live": [],
    }
    for row in websites:
        raw = (args.private_cache / (row["recipe_id"] + ".html")).read_bytes()
        if digest(raw) != row["html_sha256"]:
            raise RuntimeError("Cached website hash mismatch: " + row["recipe_id"])
        result = website_fields(raw.decode("utf-8"), row["source_url"], saved[row["recipe_id"]])
        report["website_cached"].append({
            "recipe_id": row["recipe_id"], "saved_title": row["title"],
            "source_url": row["source_url"], "html_sha256": digest(raw), **result,
        })

    sample = PublicSample()
    args.trial_cache.mkdir(parents=True)
    with YoutubeDL({"quiet": True, "cachedir": False}, auto_init=False) as downloader:
        with patch.object(downloader, "urlopen", side_effect=RuntimeError(
                "Network is forbidden in the yt-dlp parser-only experiment")):
            extractor = TikTokIE(downloader)
            captions = assessment["caption_evidence"]
            for entry in captions:
                key = entry["recipe_id"]
                redirected = key in follow and follow[key]["outcome"] == "public_response"
                row = follow[key] if redirected else first[key]
                suffix = "-redirect.html" if redirected else ".html"
                raw = (args.private_cache / (key + suffix)).read_bytes()
                if digest(raw) != row["html_sha256"]:
                    raise RuntimeError("Cached TikTok hash mismatch: " + key)
                url = row["target_url"] if redirected else row["source_url"]
                result = tiktok_fields(raw.decode("utf-8"), url, extractor)
                if result.get("caption_sha256") != entry["caption_sha256"]:
                    raise RuntimeError("Cached TikTok caption mismatch: " + key)
                report["tiktok_cached"].append({
                    "recipe_id": key, "source_url": url, **result,
                    "previous_manual_assessment": entry["assessment"],
                })

            # First Tasty Recipes, first WP Recipe Maker, then the non-card-marked site.
            live_websites = [websites[0], websites[1],
                             next(row for row in websites if not row["html_recipe_card_markers"])]
            for row in live_websites:
                result, payload = sample.get(row["source_url"])
                if payload is not None:
                    raw, html = payload
                    (args.trial_cache / (row["recipe_id"] + ".html")).write_bytes(raw)
                    result.update(website_fields(html, row["source_url"], saved[row["recipe_id"]]))
                    result["same_html_as_original"] = digest(raw) == row["html_sha256"]
                report["website_live"].append({
                    "recipe_id": row["recipe_id"], "saved_title": row["title"], **result,
                })

            # Deliberately cover a complete recipe, ingredient-only text, a sparse caption,
            # and a previously non-identifying short link rather than selecting only successes.
            live_ids = ["cf82c7f7-daea-476d-9224-60b94024f4df",
                        "a06fb70b-34da-4ed2-88ec-c1f2b592d2ea",
                        "6305afc1-03aa-49ec-bc96-1099f667aa93",
                        "17cf9e38-8707-4870-80af-eefb6037a6e7"]
            for key in live_ids:
                row = first[key]
                result, payload = sample.get(row["source_url"])
                if payload is not None:
                    raw, html = payload
                    (args.trial_cache / (key + ".html")).write_bytes(raw)
                    result.update(tiktok_fields(html, row["source_url"], extractor))
                    prior = next((entry for entry in captions if entry["recipe_id"] == key), None)
                    if prior:
                        result["same_caption_as_original"] = (
                            result.get("caption_sha256") == prior["caption_sha256"])
                        result["previous_manual_assessment"] = prior["assessment"]
                report["tiktok_live"].append({
                    "recipe_id": key, "saved_title": row["title"], **result,
                })
    report["live_http"] = sample.trace
    report["robots_checks"] = [policy for _, policy in sample.robots.values()]
    report["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2, ensure_ascii=True)
        stream.write("\n")
    print(json.dumps({
        section: dict(collections.Counter(row["outcome"] for row in report[section]))
        for section in ["website_cached", "tiktok_cached", "website_live", "tiktok_live"]
    }, indent=2))


if __name__ == "__main__":
    main()
