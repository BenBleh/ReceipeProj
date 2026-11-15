"""Research-only follow-up: check identifiable public redirect targets once.

Does not follow TikTok homepage fallbacks, preserve sharing identifiers, fetch
media, or retry blocked hosts. Not an application component.
"""

import argparse
import datetime
import hashlib
import json
import pathlib
import re
import time
import urllib.error
import urllib.parse
import urllib.robotparser

from probe_sources import AGENT, fetch


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--private-cache", required=True, type=pathlib.Path)
    args = parser.parse_args()
    output = pathlib.Path(__file__).parent
    data = json.loads((output / "source-probes.json").read_text(encoding="utf-8"))
    policies = {}
    results = []
    for initial in data["results"]:
        location = initial.get("redirect_location")
        if not location:
            continue
        parsed = urllib.parse.urlsplit(location)
        video = re.search(r"/video/(\d+)", parsed.path)
        youtube_id = dict(urllib.parse.parse_qsl(parsed.query)).get("v")
        if parsed.hostname == "www.tiktok.com" and video:
            target = "https://www.tiktok.com/@/video/" + video[1]
        elif parsed.hostname == "www.youtube.com" and youtube_id:
            target = "https://www.youtube.com/watch?v=" + urllib.parse.quote(youtube_id)
        elif parsed.hostname == "www.tiktok.com" and parsed.path == "/":
            initial["redirect_location"] = "https://www.tiktok.com/"
            initial["redirect_kind"] = "homepage_fallback_no_post_identity"
            continue
        else:
            initial["redirect_location"] = urllib.parse.urlunsplit((
                parsed.scheme, parsed.netloc, parsed.path, "", ""
            ))
            initial["redirect_kind"] = "unreviewed_target_not_followed"
            continue
        initial["redirect_location"] = target
        initial["redirect_kind"] = "identifiable_video_target"
        result = {
            "recipe_id": initial["recipe_id"], "title": initial["title"],
            "source_url": initial["source_url"], "target_url": target,
            "checked_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "method": "One unauthenticated GET of identifiable target after separate robots check",
        }
        try:
            origin = "https://" + urllib.parse.urlsplit(target).netloc
            if origin not in policies:
                status, final, raw, body = fetch(origin + "/robots.txt", 1_000_000)
                robot = urllib.robotparser.RobotFileParser()
                robot.parse(body.splitlines())
                policies[origin] = robot
            if not policies[origin].can_fetch(AGENT, target):
                result["outcome"] = "robots_disallowed_not_crawled"
            else:
                time.sleep(1)
                status, final, raw, body = fetch(target)
                result.update({"outcome": "public_response", "http_status": status,
                               "response_bytes": len(raw), "html_sha256": hashlib.sha256(raw).hexdigest()})
                (args.private_cache / (initial["recipe_id"] + "-redirect.html")).write_bytes(raw)
        except (urllib.error.HTTPError, urllib.error.URLError, OSError, ValueError) as error:
            result["outcome"] = "access_or_transport_error"
            result["http_status"] = getattr(error, "code", None)
            result["detail"] = type(error).__name__
            if getattr(error, "code", None) in {401, 403, 429}:
                # All remaining identifiable targets on this host must stop too.
                policies[origin] = urllib.robotparser.RobotFileParser()
                policies[origin].parse(["User-agent: *", "Disallow: /"])
        results.append(result)
        print(json.dumps(result, ensure_ascii=True), flush=True)
    (output / "source-probes.json").write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (output / "redirect-probes.json").write_text(json.dumps({
        "snapshot_commit": data["snapshot_commit"], "results": results,
        "limitations": [
            "Sharing/tracking queries were removed; canonical target identity was retained.",
            "TikTok homepage fallbacks are not proof a post has been deleted.",
            "No redirect chains, access-control bypass, media downloads, or source text extraction service used.",
        ],
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
