"""Offline analysis of this study's retained evidence; no network or app writes."""

import argparse
import collections
import csv
import hashlib
import json
import pathlib
import re
import urllib.parse

from probe_sources import normalized

# These are human-reviewed assessments of delivered caption text, not model
# confidence scores or evidence that a complete end-to-end importer succeeded.
CAPTION_REVIEW = {
    "7374209506705788168": ("ingredients_and_method_text", "19 saved ingredient lines; caption has 12 numbered steps versus 5 saved. Source mushroom quantity lacks a unit."),
    "7538351446840904967": ("ingredients_and_method_text", "Same video used by two saved recipes with different titles, ingredient counts and groups; original caption has five method bullets."),
    "7637689006695664903": ("ingredients_and_method_text", "Several component lists and nine numbered actions; stored method combines these into six steps."),
    "7504610442497559815": ("ingredients_and_method_text", "Caption has explicit ingredient components and seven method steps. Review source quantity/seasoning ambiguities."),
    "7248145454528007425": ("ingredients_only", "Caption supplies an ingredient/seasoning checklist, not the seven saved cooking steps. Need permitted video/transcript or user text."),
    "7197319424364268801": ("sparse_caption", "Caption describes dumplings but supplies neither ingredient quantities nor a full method."),
    "7556875692584029458": ("ingredients_and_method_text", "Multiple component lists and six method bullets versus five saved steps; source spelling/ingredients used only in method need review."),
    "7488691206520737032": ("ingredients_and_method_text", "Ingredient lists and five method bullets versus six saved steps; listed vegetables lack an explicit cooking instruction."),
    "7515206556741307656": ("ingredients_and_method_text", "Caption has components and six method bullets versus five saved steps; noodles are listed twice and broth miso is not explicitly added in method."),
    "7677145858697202964": ("recipe_in_comments", "Caption discusses several meals and says recipes/nutrition are in comments; the requested lasagne recipe is not in the caption."),
    "7517925451868671250": ("ingredients_and_method_text", "Ingredient components and seven method bullets versus four saved steps; caption contains typographical errors."),
    "7409926301655633169": ("ingredients_and_method_text", "Ingredient/dressing lists and cooking directions are interleaved rather than cleanly separated into recipe fields."),
    "7458194709282852128": ("sparse_caption", "Caption is a short reaction plus hashtags; no ingredients or cooking method."),
    "7228471503459765506": ("ingredients_only", "Caption is a beef-stroganoff ingredient checklist without the six saved cooking steps."),
    "7359501650945379617": ("ingredients_and_method_text", "Explicit Ingredients and Method sections with two method bullets; remove nutrition/marketing text without altering quantities."),
    "7251915348591660306": ("ingredients_only", "Caption has a dozen ingredient quantities, some comma-separated; the six saved cooking steps are not supplied."),
    "7358380418778926356": ("ingredients_and_method_text", "Pudding and sauce ingredients plus eight method actions; final ingredient and first instruction share the same bullet convention."),
    "7371381861379067144": ("ingredients_and_method_text", "Same video used by two saved recipes with 3 versus 5 steps and different titles. Current author handle differs from an older source URL."),
    "7149191656145718533": ("partial_ratio_and_timing", "Caption gives equal-volume flour/egg/milk and oven timings, not a fully specified ingredient list and cooking method."),
    "7514530797588827400": ("ingredients_and_method_text", "Nine ingredient entries and nine numbered caption actions versus seven saved steps; garnish/enjoy actions affect step segmentation."),
    "7288237970178493702": ("sparse_caption", "Caption attributes the recipe to another creator but does not provide ingredients or method."),
    "7453903491971468576": ("ingredients_and_method_text", "Ingredient list and heart-prefixed cooking paragraphs; nutrition and cookbook promotion must not become recipe steps."),
    "7458765503172021505": ("ingredients_and_method_text", "Six ingredients and five numbered directions versus a single combined saved step."),
    "7463390082100120864": ("ingredients_and_method_text", "Ingredient list and prose directions; oil spray and a listed herb complicate matching the saved eleven ingredient rows."),
    "7440642016637095186": ("ingredients_and_method_text", "Separate salad/dressing lists and six numbered directions; optional herbs need faithful preservation."),
}


def canonical(url):
    parsed = urllib.parse.urlsplit(url)
    if "tiktok.com" in (parsed.hostname or "") or "instagram.com" in (parsed.hostname or ""):
        return urllib.parse.urlunsplit((parsed.scheme, parsed.netloc, parsed.path, "", ""))
    return url


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--private-cache", required=True, type=pathlib.Path)
    args = parser.parse_args()
    root = pathlib.Path(__file__).parent
    with (root / "source-inventory.csv").open(encoding="utf-8-sig", newline="") as file:
        inventory = list(csv.DictReader(file))
    for row in inventory:
        row["source_url"] = canonical(row["source_url"])
    with (root / "source-inventory.csv").open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=list(inventory[0]))
        writer.writeheader()
        writer.writerows(inventory)
    initial = json.loads((root / "source-probes.json").read_text(encoding="utf-8"))
    redirects = json.loads((root / "redirect-probes.json").read_text(encoding="utf-8"))
    for collection in (initial["results"], redirects["results"]):
        for item in collection:
            item["source_url"] = canonical(item["source_url"])
    for filename, value in (("source-probes.json", initial), ("redirect-probes.json", redirects)):
        (root / filename).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    first = {item["recipe_id"]: item for item in initial["results"]}
    follow = {item["recipe_id"]: item for item in redirects["results"]}
    corpus = json.loads((args.private_cache / "recipes.json").read_text(encoding="utf-8"))
    saved = {recipe["Id"]: recipe for recipe in corpus["recipes"]}
    corpus_summary = json.loads((root / "corpus-summary.json").read_text(encoding="utf-8"))
    corpus_summary["unique_stored_source_urls"] = len({
        url for recipe in corpus["recipes"]
        for url in re.findall(r"https?://[^\s<>\"']+", recipe.get("Source") or "")
    })
    corpus_summary["unique_source_urls"] = len({row["source_url"] for row in inventory if row["source_url"]})
    (root / "corpus-summary.json").write_text(json.dumps(corpus_summary, indent=2) + "\n", encoding="utf-8")
    matrix = []
    details = []
    identities = collections.defaultdict(list)
    for row in inventory:
        key = row["recipe_id"]
        result = first.get(key)
        row = dict(row)
        row.update({
            "title": re.sub(r"\s+", " ", row["title"]).strip(),
            "observed_outcome": result["outcome"] if result else "no_source_url",
            "source_identity": canonical(row["source_url"]), "source_ingredient_lines": "",
            "source_steps": "", "caption_characters": "", "image_candidate": "Not assessed",
            "url_only_route": "", "ai_role": "", "fidelity_notes": "",
        })
        if row["platform"] == "Website" and result.get("recipe_summaries"):
            summary = result["recipe_summaries"][0]
            row.update({
                "observed_outcome": "recipe_jsonld_available",
                "source_ingredient_lines": summary["ingredient_lines"], "source_steps": summary["steps"],
                "image_candidate": "Recipe image metadata" if summary["image_available"] else "None observed",
                "url_only_route": "Public HTML -> Recipe JSON-LD -> editable draft; card DOM for groups/notes",
                "ai_role": "Not needed for source-field extraction; optional separately reviewed editorial assistance",
                "fidelity_notes": "Normalized title/ingredient/step equality: " + "/".join(
                    str(summary[name]) for name in [
                        "title_normalized_equal", "ingredients_normalized_equal", "steps_normalized_equal"
                    ]) + ". Stored edits/formatting prevent assuming exact reconstruction.",
            })
        elif row["platform"] == "Website":
            row.update({
                "url_only_route": "Not demonstrated from this environment; user-supplied permitted text/HTML or creator alternative",
                "ai_role": "Cannot solve blocked retrieval; can structure text the user supplies",
                "fidelity_notes": "One Simple Home Edit URL returned 403; the other two were deliberately not retried on that host.",
            })
        elif row["platform"] == "Instagram":
            row.update({
                "url_only_route": "Not demonstrated; generic automated crawl excluded by observed robots rules",
                "ai_role": "Optional on user-supplied caption/screenshots/permitted media, not a retrieval bypass",
                "fidelity_notes": "No post page fetched. Caption content, account eligibility and video-only gaps remain unknown for this URL.",
            })
        elif row["platform"] == "TikTok":
            path = args.private_cache / (key + ".html")
            if key in follow and follow[key]["outcome"] == "public_response":
                path = args.private_cache / (key + "-redirect.html")
            if path.exists():
                raw = path.read_text(encoding="utf-8", errors="replace")
                match = re.search(r'<script[^>]+id="__UNIVERSAL_DATA_FOR_REHYDRATION__"[^>]*>(.*?)</script>', raw, re.S)
                if match:
                    hydration = json.loads(match[1])
                    detail = hydration.get("__DEFAULT_SCOPE__", {}).get("webapp.video-detail", {})
                    item = detail.get("itemInfo", {}).get("itemStruct", {})
                    video_id = item.get("id")
                    expected = re.search(r"/video/(\d+)", follow.get(key, {}).get("target_url", row["source_url"]))
                    if not video_id or not expected or expected[1] != video_id or detail.get("statusCode") != 0:
                        raise RuntimeError("Requested TikTok post identity not established: " + key)
                    caption = item.get("desc") or ""
                    assessment, notes = CAPTION_REVIEW[video_id]
                    row.update({
                        "observed_outcome": assessment, "caption_characters": len(caption),
                        "source_identity": "tiktok:video:" + video_id,
                        "image_candidate": "Video cover metadata; no image downloaded" if item.get("video", {}).get("cover") else "None observed",
                        "url_only_route": "Public delivered hydration -> exact requested-post caption; unsupported/fragile adapter, policy review required",
                        "ai_role": "Optional caption structuring; OCR/ASR/permitted media or user input if fields are absent",
                        "fidelity_notes": notes,
                    })
                    if assessment not in {"ingredients_and_method_text"}:
                        row["url_only_route"] += "; caption alone is insufficient for the full saved recipe"
                    recipe = saved[key]
                    details.append({
                        "recipe_id": key, "requested_video_id": expected[1], "returned_video_id": video_id,
                        "post_status": detail.get("statusCode"), "caption_characters": len(caption),
                        "caption_sha256": hashlib.sha256(caption.encode()).hexdigest(),
                        "assessment": assessment, "notes": notes,
                        "saved_ingredient_lines_literally_present": sum(
                            normalized(i.get("FQIngrediantDescription") or "") in normalized(caption)
                            for i in recipe["Ingredients"] if (i.get("FQIngrediantDescription") or "").strip()
                        ),
                        "saved_steps_literally_present": sum(
                            normalized(step.get("Instructions") or "") in normalized(caption)
                            for step in recipe["Steps"] if (step.get("Instructions") or "").strip()
                        ),
                        "subtitle_tracks_advertised_not_downloaded": len(item.get("video", {}).get("subtitleInfos") or []),
                    })
            if result.get("redirect_kind") == "homepage_fallback_no_post_identity":
                row.update({
                    "observed_outcome": "short_link_homepage_fallback",
                    "source_identity": "unresolved:" + row["source_url"],
                    "url_only_route": "Not demonstrated; request a fresh direct post URL or user-supplied source material",
                    "ai_role": "Cannot recover post identity or private edits from a homepage redirect",
                    "fidelity_notes": "Short link returned TikTok homepage without a post ID. Not proof the original post was deleted.",
                })
        elif row["platform"] == "YouTube":
            row.update({
                "observed_outcome": "description_with_creator_recipe_link",
                "source_identity": "youtube:video:byz07dJaXqI",
                "image_candidate": "Video/linked-recipe image metadata; no image downloaded",
                "url_only_route": "Public description -> explicit creator recipe URL -> Recipe JSON-LD, with component review",
                "ai_role": "Not needed to follow explicit link/extract structured fields; selection of plain crepe component needs review",
                "fidelity_notes": "Video title Blender Crepes; linked page Nutella Crepes has 11 ingredient lines/12 steps versus saved plain Crepes 6/2. One caption track advertised, not downloaded.",
                "source_ingredient_lines": 11, "source_steps": 12,
            })
        else:
            row.update({
                "url_only_route": "Outside URL-only scope: no source URL stored",
                "ai_role": "Can assist only after the user supplies source material; must not invent its provenance",
                "fidelity_notes": "Six entries have no source text and two have only a non-URL source label.",
            })
        if row["platform"] == "TikTok" and not row["fidelity_notes"]:
            raise RuntimeError("Unassessed TikTok reference: " + key)
        identities[row["source_identity"]].append(key)
        matrix.append(row)
    for row in matrix:
        peers = identities[row["source_identity"]] if row["source_identity"] else []
        row["same_source_recipe_ids"] = ";".join(key for key in peers if key != row["recipe_id"])
    with (root / "per-recipe-feasibility.csv").open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=list(matrix[0]))
        writer.writeheader()
        writer.writerows(matrix)
    website = [item["recipe_summaries"][0] for item in first.values() if item.get("recipe_summaries")]
    summary = {
        "snapshot_commit": corpus["commit"], "inventory_recipe_rows": len(matrix),
        "source_url_recipe_rows": sum(bool(row["source_url"]) for row in matrix),
        "observed_outcomes": dict(collections.Counter(row["observed_outcome"] for row in matrix)),
        "website_normalized_equal_counts": {
            "out_of": len(website),
            "title": sum(item["title_normalized_equal"] for item in website),
            "ingredients": sum(item["ingredients_normalized_equal"] for item in website),
            "steps": sum(item["steps_normalized_equal"] for item in website),
            "all_three": sum(item["title_normalized_equal"] and item["ingredients_normalized_equal"] and
                             item["steps_normalized_equal"] for item in website),
        },
        "tiktok_references_with_requested_caption": len(details),
        "tiktok_unique_requested_video_ids": len({item["returned_video_id"] for item in details}),
        "tiktok_caption_assessment_counts": dict(collections.Counter(item["assessment"] for item in details)),
        "confirmed_duplicate_source_groups": [values for identity, values in identities.items()
                                             if identity.startswith("tiktok:") and len(values) > 1],
        "limitations": [
            "Availability of recipe-like source text is not a tested end-to-end import or semantic accuracy score.",
            "Caption classifications are manual evidence reviews, not generative-AI import runs.",
            "Literal presence counts are diagnostics only: punctuation, grouping and edits affect them.",
            "Website equality decodes HTML, collapses whitespace and ignores case; it is not semantic equivalence.",
        ],
        "caption_evidence": details,
    }
    (root / "extraction-assessment.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in summary.items() if key != "caption_evidence"}, indent=2))


if __name__ == "__main__":
    main()
