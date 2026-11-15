"""Read-only research inventory. Never writes to GitHub or changes the application."""

import argparse
import csv
import datetime
import hashlib
import io
import json
import os
import pathlib
import re
import urllib.parse
import urllib.request
import zipfile
from collections import Counter


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--private-cache", required=True, type=pathlib.Path)
    args = parser.parse_args()
    output = pathlib.Path(__file__).parent
    args.private_cache.mkdir(parents=True, exist_ok=True)
    token = pathlib.Path(os.environ["NIBBLES_GITHUB_TOKEN_FILE"]).read_text(encoding="utf-8-sig").strip()

    def github(suffix):
        request = urllib.request.Request(
            "https://api.github.com/repos/BenBleh/rfiles/" + suffix,
            headers={"Authorization": "Bearer " + token, "User-Agent": "Nibbles-source-research",
                     "Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"},
        )
        with urllib.request.urlopen(request, timeout=90) as response:
            data = response.read()
        return data if suffix.startswith("zipball/") else json.loads(data)

    head = github("git/ref/heads/main")["object"]["sha"]
    commit = github("git/commits/" + head)
    tree = github("git/trees/" + commit["tree"]["sha"] + "?recursive=1")
    if tree["truncated"]:
        raise RuntimeError("Cannot inventory a truncated tree.")
    files = {item["path"]: item for item in tree["tree"] if item["type"] == "blob"}
    archive_raw = github("zipball/" + head)
    (args.private_cache / "rfiles-snapshot.zip").write_bytes(archive_raw)
    archive = zipfile.ZipFile(io.BytesIO(archive_raw))
    prefix = archive.namelist()[0].split("/")[0] + "/"
    recipes = []
    for path, item in files.items():
        if not path.endswith(".json"):
            continue
        raw = archive.read(prefix + path)
        digest = hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
        if digest != item["sha"]:
            raise RuntimeError("Snapshot hash mismatch: " + path)
        value = json.loads(raw)
        if isinstance(value, dict) and "Ingredients" in value and "Steps" in value:
            recipes.append(value)
    recipes.sort(key=lambda recipe: (recipe.get("Title") or "").casefold())
    (args.private_cache / "recipes.json").write_text(
        json.dumps({"commit": head, "recipes": recipes}, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    rows = []
    for recipe in recipes:
        source = (recipe.get("Source") or "").strip()
        urls = re.findall(r"https?://[^\s<>\"']+", source)
        if not urls:
            urls = [""]
        for raw_url in urls:
            parsed = urllib.parse.urlsplit(raw_url)
            host = (parsed.hostname or "").lower().removeprefix("www.")
            if host.endswith("instagram.com"):
                platform = "Instagram"
            elif host.endswith("tiktok.com"):
                platform = "TikTok"
            elif host in {"youtube.com", "youtu.be", "m.youtube.com"}:
                platform = "YouTube"
            elif not raw_url:
                platform = "No URL" if not source else "Source text only"
            else:
                platform = "Website"
            query = urllib.parse.parse_qsl(parsed.query, keep_blank_values=True)
            query = [(key, value) for key, value in query if not (
                key.lower().startswith("utm_") or key.lower() in {"igsh", "igshid", "fbclid", "si", "share_id"}
            )]
            if platform in {"Instagram", "TikTok"}:
                query = []
            elif platform == "YouTube":
                query = [(key, value) for key, value in query if key == "v"]
            canonical = urllib.parse.urlunsplit((
                parsed.scheme, parsed.netloc, parsed.path, urllib.parse.urlencode(query), ""
            )) if raw_url else ""
            rows.append({
                "recipe_id": recipe["Id"], "title": recipe.get("Title") or "",
                "platform": platform, "domain": host, "source_url": canonical,
                "ingredient_lines": len(recipe.get("Ingredients") or []),
                "steps": len(recipe.get("Steps") or []),
                "named_groups": len(recipe.get("IngredientGroups") or []),
                "has_source_text": bool(source),
            })
    with (output / "source-inventory.csv").open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    summary = {
        "snapshot_commit": head,
        "checked_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "recipes": len(recipes),
        "recipes_with_source_text": sum(bool((recipe.get("Source") or "").strip()) for recipe in recipes),
        "recipes_with_source_urls": len({row["recipe_id"] for row in rows if row["source_url"]}),
        "unique_stored_source_urls": len({
            url for recipe in recipes
            for url in re.findall(r"https?://[^\s<>\"']+", recipe.get("Source") or "")
        }),
        "unique_source_urls": len({row["source_url"] for row in rows if row["source_url"]}),
        "platform_recipe_counts": dict(Counter(row["platform"] for row in rows)),
        "domain_recipe_counts": dict(Counter(row["domain"] for row in rows if row["domain"])),
        "notes": ["Tracking-only query parameters removed from exported URLs.",
                  "Full recipe snapshot is retained only in the separately specified private cache.",
                  "No GitHub writes; counts describe this pinned snapshot, not future changes."],
    }
    (output / "corpus-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))
    for row in rows:
        if row["source_url"]:
            print(json.dumps(row, ensure_ascii=True))


if __name__ == "__main__":
    main()
