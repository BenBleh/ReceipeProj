"""Offline, source-only draft extraction. Never reads saved recipe contents."""

import collections
import csv
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import re
import sys
import urllib.parse
import uuid

from recipe_scrapers import scrape_html
from recipe_scrapers._exceptions import RecipeScrapersExceptions

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from probe_sources import Page, has_type, instructions, normalized, objects, text


class DraftError(ValueError):
    pass


def source_key(url):
    try:
        parsed = urllib.parse.urlsplit(url.strip())
    except ValueError as error:
        raise DraftError("The source URL is malformed.") from error
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password:
        raise DraftError("Use an HTTPS source URL without embedded credentials.")
    host = parsed.hostname.lower().removeprefix("www.")
    if host == "instagram.com":
        match = re.fullmatch(r"/(?:p|reel)/([A-Za-z0-9_-]+)/?", parsed.path)
        if match:
            return "instagram:" + match[1]
    if host == "tiktok.com":
        match = re.search(r"/video/(\d+)(?:/|$)", parsed.path)
        if match:
            return "tiktok:" + match[1]
    return urllib.parse.urlunsplit(("https", parsed.netloc.lower(), parsed.path.rstrip("/"),
                                   parsed.query, ""))


def empty_draft(source):
    return {
        "Id": str(uuid.uuid4()), "Title": "", "TimeToComplete": "",
        "Notes": "", "Source": source, "ImageData": None,
        "Ingredients": [], "IngredientGroups": [], "Steps": [],
    }


def add_group(draft, name, ingredients):
    if not ingredients:
        return
    group_id = None
    if name:
        group_id = str(uuid.uuid4())
        draft["IngredientGroups"].append({
            "Id": group_id, "Name": name.strip(), "Order": len(draft["IngredientGroups"]),
        })
    for ingredient in ingredients:
        draft["Ingredients"].append({
            "FQIngrediantDescription": ingredient.strip(), "GroupId": group_id,
        })


def warnings_for(draft):
    warnings = []
    for field, message in [
        ("Title", "No reliable title was found. Add one before exporting."),
        ("Ingredients", "No ingredient list was identified. Supply it from the source."),
        ("Steps", "No cooking method was identified. Do not invent missing instructions."),
    ]:
        if not draft[field]:
            warnings.append(message)
    if not draft["Source"]:
        warnings.append("No source URL supplied; add attribution if available.")
    if draft["Steps"] and draft["Ingredients"]:
        method = " ".join(step["Instructions"] for step in draft["Steps"])
        listed = " ".join(i["FQIngrediantDescription"] for i in draft["Ingredients"])
        if (re.search(r"\b(?:\d+[¼½¾]?|[¼½¾])\s*(?:cups?|ml|litres?)\s+"
                      r"(?:(?:lukewarm|warm|boiling|cold)\s+)?water\b", method, re.I)
                and not re.search(r"\bwater\b", listed, re.I)):
            warnings.append("Quantified water appears in the method but not the ingredient list. Review it.")
    return warnings


def website_draft(html, source):
    page = Page()
    page.feed(html)
    candidates = []
    for script in page.scripts:
        try:
            data = json.loads(script)
        except json.JSONDecodeError as error:
            raise DraftError("Recipe metadata contains invalid JSON; paste the recipe text instead.") from error
        candidates.extend(item for item in objects(data) if has_type(item, "Recipe"))
    if len(candidates) != 1:
        raise DraftError("Expected one unambiguous Recipe object; use pasted text instead.")
    recipe = candidates[0]
    ingredients = recipe.get("recipeIngredient")
    if not isinstance(ingredients, list) or not all(isinstance(i, str) for i in ingredients):
        raise DraftError("The source ingredient list has an unsupported structure.")
    draft = empty_draft(source)
    draft["Title"] = text(recipe.get("name") or "")
    total = recipe.get("totalTime")
    draft["TimeToComplete"] = total if isinstance(total, str) else ""
    ingredient_text = [text(i).strip() for i in ingredients if text(i).strip()]
    warnings = []

    def group_key(value):
        return normalized(value).replace("((", "(").replace("))", ")")

    groups = None
    try:
        scraper = scrape_html(html, org_url=source, online=False, supported_only=False)
        proposed = scraper.ingredient_groups()
        if (collections.Counter(group_key(i) for group in proposed for i in group.ingredients)
                == collections.Counter(map(group_key, ingredient_text))):
            remaining = collections.defaultdict(collections.deque)
            for value in ingredient_text:
                remaining[group_key(value)].append(value)
            groups = [(group.purpose or "", [remaining[group_key(i)].popleft()
                                            for i in group.ingredients]) for group in proposed]
        else:
            warnings.append("Source grouping could not be matched without changing ingredients; left ungrouped.")
    except (RecipeScrapersExceptions, NotImplementedError, ValueError) as error:
        warnings.append("Grouping unavailable (" + type(error).__name__ + "); ingredients remain ungrouped.")
    for name, items in groups if groups is not None else [("", ingredient_text)]:
        add_group(draft, name, items)
    draft["Steps"] = [{"Num": n, "Instructions": value, "ImageData": None}
                      for n, value in enumerate(instructions(recipe.get("recipeInstructions")))
                      if value.strip()]
    sections = [text(item.get("name") or "") for item in objects(recipe.get("recipeInstructions"))
                if has_type(item, "HowToSection") and item.get("name")]
    if sections:
        warnings.append("Method sections retained in source evidence, not inserted as cooking steps.")
    return {
        "draft": draft, "warnings": warnings + warnings_for(draft),
        "raw": json.dumps(recipe, ensure_ascii=False, indent=2),
        "evidence": {"method": "Recipe JSON-LD + verified DOM ingredient grouping",
                     "method_sections": sections, "images": "Not downloaded"},
        "unassigned": "",
    }


def tiktok_draft(content, source, video_id):
    match = re.search(r'<script[^>]+id="__UNIVERSAL_DATA_FOR_REHYDRATION__"[^>]*>'
                      r"(.*?)</script>", content, re.S)
    if not match:
        raise DraftError("The page has no requested-post data. Paste the expanded caption instead.")
    try:
        data = json.loads(match[1])
        scope = data.get("__DEFAULT_SCOPE__", {}) if isinstance(data, dict) else {}
        detail = scope.get("webapp.video-detail", {}) if isinstance(scope, dict) else {}
        info = detail.get("itemInfo", {}) if isinstance(detail, dict) else {}
        item = info.get("itemStruct", {}) if isinstance(info, dict) else {}
    except json.JSONDecodeError as error:
        raise DraftError("The requested-post data is not valid JSON.") from error
    if (not isinstance(detail, dict) or not isinstance(item, dict)
            or detail.get("statusCode") != 0 or item.get("id") != video_id):
        raise DraftError("The video identity/status does not match the requested source.")
    caption = item.get("desc")
    if not isinstance(caption, str) or not caption.strip():
        raise DraftError("The requested video has no caption. Paste source text instead.")
    return caption_draft(caption, source)


HEADINGS = re.compile(r"^(ingredients?(?:\s+for\s+\d+)?|method|instructions|directions):?$", re.I)
METHOD = re.compile(r"^(method|instructions|directions):?$", re.I)
NUMBER = r"(?:\d+\s+(?:&\s*)?\d+/\d+|\d+/\d+|\d+(?:\.\d+)?[¼½¾]?|[¼½¾⅓⅔⅛])"
QUANTITY = NUMBER + r"(?:\s*[-–]\s*" + NUMBER + r")?"
INLINE_ITEM = re.compile(
    r"(?<!\S)" + QUANTITY + r"\s*(?:kg|grams?|g|ml|litres?|cups?|tbsp|tbs|tsp|"
    r"tablespoons?|teaspoons?|cloves?|slices?|cans?|large|medium|small|eggs?|onions?)\b", re.I)
TAIL = re.compile(r"^(?:#|follow\b|save (?:this|for)\b|nutrition\b|macros?\b|enjoy[!. ]*$)", re.I)
COOKING = re.compile(r"^(?:preheat|heat|whisk|mix|stir|combine|cook|bake|boil|fry|"
                     r"blend|place|pour|transfer|slice|chop|cut|melt|remove|return|"
                     r"brush|cover|toss|drizzle|marinate)\b", re.I)


def caption_title(prefix):
    for line in prefix:
        capitals = re.match(r"^([A-Z][A-Z ,&'-]{5,110})(?=\s+[A-Z][a-z])", line)
        if capitals:
            return capitals[1].strip()
    for line in prefix:
        value = line.strip()
        words = re.findall(r"[A-Za-z]+", value)
        if (2 <= len(words) <= 16 and len(value) <= 120
                and not re.search(r"[$£€]|\b(?:kcals?|calories|protein|serves?|replying|"
                                  r"back with|follow|save for|ingredients?|method)\b", value, re.I)
                and not re.search(r"[.!?]", value)):
            return value
    return ""


def split_inline_ingredients(value):
    matches = [match for match in INLINE_ITEM.finditer(value)
               if value[:match.start()].count("(") == value[:match.start()].count(")")]
    if len(matches) < 2:
        return [value], False
    boundaries = [0] + [match.start() for match in matches if match.start() > 0] + [len(value)]
    return [value[a:b].strip(" ,;") for a, b in zip(boundaries, boundaries[1:])
            if value[a:b].strip(" ,;")], True


def caption_draft(caption, source=""):
    if not isinstance(caption, str) or not caption.strip():
        raise DraftError("Paste some source text first.")
    draft = empty_draft(source)
    raw = caption
    caption = "\n".join(caption.replace("\r\n", "\n").splitlines())
    caption = re.sub(r"\b(ingredients?(?:\s+for\s+\d+)?|method|instructions|directions)\s*:",
                     r"\n\1:\n", caption, flags=re.I)
    caption = re.sub(r"\b(INGREDIENTS|METHOD|INSTRUCTIONS|DIRECTIONS)\b"
                     r"(?=\s*(?:\n|[-*•]|\d+[.)]))", r"\n\1\n", caption)
    caption = re.sub(r"\s{2,}([A-Za-z][A-Za-z &/-]{1,40}:?)(?=\s+[-*•])",
                     r"\n\1\n", caption)
    caption = re.sub(r"(?<!\S)[-*•▪]\s*(?:\[\s*\]\s*)?", "\n- ", caption)
    caption = re.sub(r"(?<!\S)(\d{1,2}[.)])\s+(?=[A-Za-z])", r"\n\1 ", caption)
    lines = [line.strip() for line in caption.splitlines() if line.strip()]
    prefix, ingredients, steps, unassigned = [], [], [], []
    mode, group, bucket = "intro", "", []
    warnings, used_inline = [], False

    def flush():
        nonlocal bucket
        if bucket:
            ingredients.append((group, bucket))
            bucket = []

    for index, line in enumerate(lines):
        if (re.match(r"^(?:serves?|servings?|makes?)\b", line, re.I)
                or re.search(r"\b\d+(?:\.\d+)?\s*(?:kcals?|calories)\b", line, re.I)
                or len(re.findall(r"\b(?:PRO|CHO|FAT)\b", line)) >= 2):
            unassigned.append(line)
            continue
        if HEADINGS.fullmatch(line):
            if METHOD.fullmatch(line):
                flush()
                mode = "method"
            else:
                mode = "ingredients"
            continue
        if TAIL.match(line):
            unassigned.append(line)
            continue
        numbered = re.match(r"^\d+[.)]\s+(.+)", line)
        bullet = line.startswith("- ")
        content = line[2:].strip() if bullet else numbered[1] if numbered else line
        if TAIL.match(content):
            unassigned.append(line)
            continue
        tag = re.search(r"(?<!\S)#\w", content)
        if tag and mode != "intro":
            unassigned.append(content[tag.start():])
            content = content[:tag.start()].strip()
            if not content:
                continue
        next_bullet = index + 1 < len(lines) and lines[index + 1].startswith("- ")
        if (mode != "method" and not bullet and not numbered and next_bullet
                and len(content) < 50 and re.fullmatch(r"[A-Za-z][A-Za-z &/-]*:?", content)
                and (prefix or mode == "ingredients")):
            flush()
            group = content.rstrip(":")
            mode = "ingredients"
            continue
        if (mode == "ingredients" and (bucket or ingredients)
                and COOKING.match(content) and len(content.split()) >= 4):
            flush()
            mode = "method"
            warnings.append("A method boundary was inferred from cooking language. Check the original.")
        if mode == "method":
            if numbered or bullet:
                steps.append(content)
            elif steps:
                steps[-1] += "\n" + content
            else:
                steps.append(content)
        elif mode == "ingredients" or bullet:
            mode = "ingredients"
            if (not bullet and not numbered and len(content) < 55
                    and content.endswith(":") and not content.lower().startswith("optional:")):
                flush()
                group = content.rstrip(":").strip()
            elif not bullet and not numbered and not re.search(r"\d|[¼½¾]", content) and not bucket:
                # Ambiguous unquantified text is evidence, not an invented ingredient.
                unassigned.append(content)
            else:
                values, split = split_inline_ingredients(content) if not bullet else ([content], False)
                bucket.extend(values)
                used_inline |= split
        else:
            prefix.append(content)
    flush()
    draft["Title"] = caption_title(prefix)
    for name, values in ingredients:
        add_group(draft, name, values)
    draft["Steps"] = [{"Num": n, "Instructions": value, "ImageData": None}
                      for n, value in enumerate(steps) if value.strip()]
    if used_inline:
        warnings.append("Inline ingredient splitting is heuristic. Check boundaries and unquantified items.")
    warnings.append("Caption parsing is experimental. Check every quantity, group and step against the source.")
    if unassigned:
        warnings.append("Some source text was not assigned; inspect the evidence panel before exporting.")
    return {"draft": draft, "warnings": warnings + warnings_for(draft), "raw": raw,
            "unassigned": "\n".join(unassigned),
            "evidence": {"method": "Deterministic caption rules; no AI or video processing"}}


def validate_export(draft):
    if not isinstance(draft, dict):
        raise DraftError("Expected a recipe draft.")
    try:
        if str(uuid.UUID(draft.get("Id", ""))) != draft.get("Id", "").lower():
            raise ValueError()
    except (ValueError, AttributeError, TypeError) as error:
        raise DraftError("Recipe ID must be a UUID.") from error
    for field in ["Title", "TimeToComplete", "Notes", "Source"]:
        if not isinstance(draft.get(field), str):
            raise DraftError(field + " must be text.")
    if not draft["Title"].strip() or any(c in draft["Title"] for c in "\r\n"):
        raise DraftError("Give the draft a single-line title.")
    if draft.get("ImageData") is not None and not isinstance(draft["ImageData"], str):
        raise DraftError("ImageData must be text or null.")
    if draft["Source"]:
        source_key(draft["Source"])
    groups = draft.get("IngredientGroups")
    ingredients = draft.get("Ingredients")
    steps = draft.get("Steps")
    if not all(isinstance(value, list) for value in [groups, ingredients, steps]):
        raise DraftError("Groups, ingredients and steps must be lists.")
    ids = set()
    for n, group in enumerate(groups):
        if (not isinstance(group, dict) or not isinstance(group.get("Id"), str)
                or not group["Id"] or group["Id"] in ids
                or not isinstance(group.get("Name"), str) or not group["Name"].strip()
                or type(group.get("Order")) is not int or group["Order"] != n):
            raise DraftError("Every group needs a unique ID, name and sequential order.")
        ids.add(group["Id"])
    if not ingredients or not steps:
        raise DraftError("Add at least one ingredient and one method step before exporting.")
    for item in ingredients:
        if (not isinstance(item, dict) or not isinstance(item.get("FQIngrediantDescription"), str)
                or not item["FQIngrediantDescription"].strip()
                or (item.get("GroupId") is not None
                    and (not isinstance(item["GroupId"], str) or item["GroupId"] not in ids))):
            raise DraftError("An ingredient is empty or refers to an unknown group.")
    for n, step in enumerate(steps):
        if (not isinstance(step, dict) or not isinstance(step.get("Instructions"), str)
                or not step["Instructions"].strip()
                or type(step.get("Num")) is not int or step["Num"] != n
                or (step.get("ImageData") is not None and not isinstance(step["ImageData"], str))):
            raise DraftError("Complete each step and keep its zero-based order.")
    return draft


@dataclass(frozen=True)
class Capture:
    url: str
    label: str
    kind: str
    path: Path
    sha256: str
    video_id: str = ""


class Catalog:
    def __init__(self, research, cache, instagram_captions):
        self.captures = {}
        self.aliases = {}
        with (research / "source-inventory.csv").open(encoding="utf-8-sig", newline="") as stream:
            inventory = {row["recipe_id"]: row for row in csv.DictReader(stream)}
        initial = json.loads((research / "source-probes.json").read_text(encoding="utf-8"))
        follow = {row["recipe_id"]: row for row in json.loads(
            (research / "redirect-probes.json").read_text(encoding="utf-8"))["results"]}
        for row in initial["results"]:
            key = row["recipe_id"]
            if row["platform"] == "Website" and row["outcome"] == "structured_recipe_found":
                self.add(Capture(row["source_url"], row["title"], "website",
                                 cache / (key + ".html"), row["html_sha256"]))
            elif row["platform"] == "TikTok":
                resolved = follow.get(key, row)
                url = resolved.get("target_url", row["source_url"])
                match = re.search(r"/video/(\d+)", url)
                path = cache / (key + ("-redirect.html" if key in follow else ".html"))
                if match and path.is_file() and resolved.get("html_sha256"):
                    self.add(Capture(url, row["title"], "tiktok", path,
                                     resolved["html_sha256"], match[1]), row["source_url"])
        instagram = json.loads((research / "instagram-instaloader-trial.json").read_text())
        for row in instagram["results"]:
            if row["outcome"] == "caption_returned":
                self.add(Capture(row["source_url"], inventory[row["recipe_id"]]["title"], "instagram",
                                 instagram_captions / (row["recipe_id"] + ".txt"),
                                 row["caption_sha256"]))

    def add(self, capture, alias=None):
        key = source_key(capture.url)
        self.captures.setdefault(key, capture)
        if alias:
            self.aliases[source_key(alias)] = key

    def samples(self):
        return [{"url": capture.url, "label": capture.label.replace("\n", " "),
                 "platform": capture.kind} for capture in self.captures.values()]

    def build(self, url):
        key = source_key(url)
        key = self.aliases.get(key, key)
        capture = self.captures.get(key)
        if capture is None:
            raise DraftError("This URL is not in the local captures. No live request was made. "
                             "Paste the caption/recipe text below instead.")
        raw = capture.path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != capture.sha256:
            raise DraftError("The retained source failed its integrity check; it was not parsed.")
        content = raw.decode("utf-8")
        if capture.kind == "website":
            result = website_draft(content, url)
        elif capture.kind == "instagram":
            result = caption_draft(content, url)
        else:
            result = tiktok_draft(content, url, capture.video_id)
        result["evidence"].update(platform=capture.kind, captured_source=True,
                                  source_sha256=capture.sha256,
                                  network="Offline capture replay; no live source request")
        return result
