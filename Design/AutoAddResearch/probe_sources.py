"""Bounded public-source research, not a production importer.

No authentication, cookies, browser impersonation, proxying, media downloads, or
access-control bypass. Honors robots rules and stops on access/rate-limit errors.
Full fetched HTML and recipe comparisons stay in the supplied private cache.
"""

import argparse
import collections
import datetime
import hashlib
import html
from html.parser import HTMLParser
import json
import pathlib
import re
import time
import urllib.error
import urllib.parse
import urllib.request
import urllib.robotparser

AGENT = "NibblesSourceResearch"
HEADERS = {"User-Agent": AGENT + "/1.0 (read-only personal recipe feasibility study)",
           "Accept": "text/html,application/json,text/plain;q=0.8"}


class PublicRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        # A redirect needs a separate robots/access review, not an automatic retry.
        return None


OPENER = urllib.request.build_opener(PublicRedirects())


def fetch(url, limit=8_000_000):
    request = urllib.request.Request(url, headers=HEADERS)
    with OPENER.open(request, timeout=30) as response:
        raw = response.read(limit + 1)
        if len(raw) > limit:
            raise ValueError("Response exceeds research size bound")
        charset = response.headers.get_content_charset() or "utf-8"
        return response.status, response.url, raw, raw.decode(charset, errors="replace")


class Page(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.scripts = []
        self.capture = None
        self.metadata = {}
        self.title = ""
        self.in_title = False

    def handle_starttag(self, tag, attributes):
        attributes = dict(attributes)
        if tag == "script" and attributes.get("type", "").lower() == "application/ld+json":
            self.capture = []
        if tag == "title":
            self.in_title = True
        if tag == "meta":
            key = attributes.get("property") or attributes.get("name")
            if key:
                self.metadata[key] = attributes.get("content", "")

    def handle_endtag(self, tag):
        if tag == "script" and self.capture is not None:
            self.scripts.append("".join(self.capture))
            self.capture = None
        if tag == "title":
            self.in_title = False

    def handle_data(self, data):
        if self.capture is not None:
            self.capture.append(data)
        if self.in_title:
            self.title += data


def objects(value):
    if isinstance(value, dict):
        yield value
        for item in value.values():
            yield from objects(item)
    elif isinstance(value, list):
        for item in value:
            yield from objects(item)


def has_type(value, expected):
    types = value.get("@type", [])
    return expected in ([types] if isinstance(types, str) else types)


def text(value):
    return html.unescape(re.sub(r"<[^>]*>", " ", str(value))).strip()


def normalized(value):
    return re.sub(r"\s+", " ", text(value)).strip().casefold()


def instructions(value):
    if isinstance(value, str):
        return [text(value)] if text(value) else []
    if isinstance(value, list):
        return [step for item in value for step in instructions(item)]
    if isinstance(value, dict):
        if "itemListElement" in value:
            return instructions(value["itemListElement"])
        if "text" in value:
            return instructions(value["text"])
        if "item" in value:
            return instructions(value["item"])
    return []


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--private-cache", required=True, type=pathlib.Path)
    args = parser.parse_args()
    output = pathlib.Path(__file__).parent
    corpus = json.loads((args.private_cache / "recipes.json").read_text(encoding="utf-8"))
    recipes = {recipe["Id"]: recipe for recipe in corpus["recipes"]}
    import csv
    with (output / "source-inventory.csv").open(encoding="utf-8-sig", newline="") as file:
        rows = [row for row in csv.DictReader(file) if row["source_url"]]
    evidence = []
    comparisons = []
    robots = {}
    host_stops = {}
    for row in rows:
        url = row["source_url"]
        parsed = urllib.parse.urlsplit(url)
        origin = parsed.scheme + "://" + parsed.netloc
        result = {
            "recipe_id": row["recipe_id"], "title": row["title"], "source_url": url,
            "platform": row["platform"],
            "checked_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "method": "Unauthenticated HTTPS GET after robots check",
            "outcome": None, "http_status": None,
        }
        try:
            if origin not in robots:
                robot_url = origin + "/robots.txt"
                try:
                    status, final, raw, robot_text = fetch(robot_url, 1_000_000)
                    robot = urllib.robotparser.RobotFileParser()
                    robot.parse(robot_text.splitlines())
                    robots[origin] = {
                        "parser": robot, "http_status": status,
                        "sha256": hashlib.sha256(raw).hexdigest(),
                    }
                except urllib.error.HTTPError as error:
                    if error.code == 404:
                        robots[origin] = {"parser": None, "http_status": error.code, "rule": "No robots file"}
                    else:
                        robots[origin] = {"parser": None, "http_status": error.code,
                                          "error": "Robots unavailable; conservatively did not crawl"}
                except (urllib.error.URLError, TimeoutError, ValueError, OSError) as error:
                    robots[origin] = {"parser": None, "http_status": None,
                                      "error": "Robots unavailable: " + type(error).__name__}
            policy = robots[origin]
            result["robots_http_status"] = policy["http_status"]
            if "error" in policy:
                result["outcome"] = "robots_unavailable_not_crawled"
                result["detail"] = policy["error"]
            elif policy["parser"] is not None and not policy["parser"].can_fetch(AGENT, url):
                result["outcome"] = "robots_disallowed_not_crawled"
            elif origin in host_stops:
                result["outcome"] = "host_access_stop_not_crawled"
                result["detail"] = host_stops[origin]
            else:
                time.sleep(1.0)
                try:
                    status, final, raw, page_text = fetch(url)
                except urllib.error.HTTPError as error:
                    result["http_status"] = error.code
                    result["outcome"] = "redirect_not_followed" if 300 <= error.code < 400 else "http_error"
                    if 300 <= error.code < 400:
                        result["redirect_location"] = error.headers.get("Location")
                    if error.code in {401, 403, 429}:
                        host_stops[origin] = "Prior HTTP " + str(error.code) + "; no further page probes on host"
                else:
                    result.update({
                        "http_status": status, "final_url": final,
                        "html_sha256": hashlib.sha256(raw).hexdigest(), "response_bytes": len(raw),
                    })
                    page = Page()
                    page.feed(page_text)
                    result["document_title"] = text(page.title)
                    result["jsonld_blocks"] = len(page.scripts)
                    result["open_graph_title"] = page.metadata.get("og:title")
                    result["has_open_graph_image"] = bool(page.metadata.get("og:image"))
                    recipe_objects = []
                    errors = 0
                    for script in page.scripts:
                        try:
                            data = json.loads(script)
                        except json.JSONDecodeError:
                            errors += 1
                            continue
                        recipe_objects.extend(obj for obj in objects(data) if has_type(obj, "Recipe"))
                    result["jsonld_parse_errors"] = errors
                    result["recipe_objects"] = len(recipe_objects)
                    result["html_recipe_card_markers"] = [
                        marker for marker in ["wprm-recipe", "tasty-recipes", 'itemtype="https://schema.org/Recipe"']
                        if marker in page_text
                    ]
                    (args.private_cache / (row["recipe_id"] + ".html")).write_bytes(raw)
                    if recipe_objects:
                        # The probe reports ambiguity instead of silently picking a different recipe.
                        summaries = []
                        saved = recipes[row["recipe_id"]]
                        saved_ingredients = [item.get("FQIngrediantDescription") or "" for item in saved["Ingredients"]]
                        saved_steps = [item.get("Instructions") or "" for item in saved["Steps"]]
                        for item in recipe_objects:
                            ingredients = item.get("recipeIngredient") or []
                            if isinstance(ingredients, str):
                                ingredients = [ingredients]
                            steps = instructions(item.get("recipeInstructions"))
                            summary = {
                                "name": item.get("name"), "ingredient_lines": len(ingredients), "steps": len(steps),
                                "image_available": bool(item.get("image")),
                                "title_normalized_equal": normalized(item.get("name") or "") == normalized(saved["Title"]),
                                "ingredients_normalized_equal": [normalized(i) for i in ingredients] == [
                                    normalized(i) for i in saved_ingredients],
                                "steps_normalized_equal": [normalized(i) for i in steps] == [
                                    normalized(i) for i in saved_steps],
                                "ingredient_exact_line_overlap": sum((collections.Counter(map(normalized, ingredients)) &
                                                                     collections.Counter(map(normalized, saved_ingredients))).values()),
                                "step_exact_line_overlap": sum((collections.Counter(map(normalized, steps)) &
                                                               collections.Counter(map(normalized, saved_steps))).values()),
                                "saved_ingredient_lines": len(saved_ingredients), "saved_steps": len(saved_steps),
                                "saved_named_groups": len(saved.get("IngredientGroups") or []),
                            }
                            summaries.append(summary)
                            comparisons.append({"recipe_id": row["recipe_id"], "source_recipe": item,
                                                "source_steps_flattened": steps, "summary": summary})
                        result["recipe_summaries"] = summaries
                        result["outcome"] = "structured_recipe_found"
                    else:
                        result["outcome"] = "public_response_no_recipe_jsonld"
        except (urllib.error.URLError, TimeoutError, ValueError, OSError) as error:
            result["outcome"] = "transport_or_parse_error"
            result["detail"] = type(error).__name__ + ": " + str(error)[:200]
        evidence.append(result)
        print(json.dumps({"title": row["title"], "outcome": result["outcome"],
                          "http_status": result["http_status"],
                          "recipe_summaries": result.get("recipe_summaries")}, ensure_ascii=True), flush=True)
        (output / "source-probes.json").write_text(json.dumps({
            "snapshot_commit": corpus["commit"], "probe_agent": AGENT, "results": evidence,
            "robots": {key: {k: v for k, v in value.items() if k != "parser"} for key, value in robots.items()},
            "limitations": [
                "Robots exclusion/unavailability and host-level access stops are not per-page retrieval attempts.",
                "HTTP 200 without Recipe JSON-LD is not proof that recipe text is absent.",
                "No browser, login, APIs, social embeds, AI, OCR, transcription, or video were exercised by this probe.",
                "Normalized comparisons only decode HTML, collapse whitespace and ignore case; not semantic equivalence.",
                "Private HTML and source recipe text are retained outside the research deliverable folder.",
            ],
        }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (args.private_cache / "comparisons.json").write_text(
        json.dumps(comparisons, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print("OUTCOMES", dict(collections.Counter(item["outcome"] for item in evidence)))


if __name__ == "__main__":
    main()
