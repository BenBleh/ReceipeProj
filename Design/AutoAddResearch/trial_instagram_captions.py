"""Bounded anonymous Instaloader experiment, not an application importer.

Requires instaloader==4.16. Uses the library's normal anonymous metadata path.
Does not load credentials, browser cookies, saved sessions or download media.
Stops the entire trial on an HTTP error, redirect or network failure.
"""

import argparse
import csv
import datetime
import hashlib
import importlib.metadata
import json
import pathlib
import re
import time
import urllib.parse
from unittest.mock import patch

import instaloader
import requests


class TrialStopped(Exception):
    pass


def source_identity(url):
    parsed = urllib.parse.urlsplit(url)
    if parsed.scheme != "https" or parsed.netloc != "www.instagram.com":
        raise ValueError("Expected an HTTPS www.instagram.com source URL")
    match = re.fullmatch(r"/(p|reel)/([A-Za-z0-9_-]+)/?", parsed.path)
    if match is None or parsed.query or parsed.fragment:
        raise ValueError("Expected a clean post/reel permalink")
    return match.group(2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--private-caption-dir", type=pathlib.Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Choose a new output file; previous evidence is not overwritten")
    if importlib.metadata.version("instaloader") != "4.16":
        parser.error("This experiment requires the inspected instaloader==4.16")

    with args.inventory.open(encoding="utf-8-sig", newline="") as stream:
        rows = [r for r in csv.DictReader(stream) if r["platform"] == "Instagram"]
    sample = ([r for r in rows if "/reel/" in r["source_url"]][:1]
              + [r for r in rows if "/p/" in r["source_url"]][:2])
    if len(sample) != 3:
        parser.error("Expected one reel and two post permalinks in the inventory")
    for row in sample:
        source_identity(row["source_url"])

    report = {
        "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "purpose": "Anonymous caption availability, not recipe-import accuracy",
        "packages": {name: importlib.metadata.version(name) for name in
                     ["instaloader", "requests", "urllib3", "certifi",
                      "charset-normalizer", "idna"]},
        "policy": {
            "authentication": False,
            "imported_cookies_or_sessions": False,
            "media_downloads": False,
            "library_default_user_agent": True,
            "tool_retries": 0,
            "http_request_budget": 12,
            "http_timeout_seconds": [10, 15],
            "stop_on_any_http_error_or_redirect": True,
            "method_differs_from_original_robots_gated_html_crawler": True,
        },
        "http": [],
        "results": [],
    }
    original_send = requests.Session.send
    stopped = None

    def bounded_send(session, request, **kwargs):
        nonlocal stopped
        if stopped:
            raise TrialStopped(stopped)
        destination = urllib.parse.urlsplit(request.url)
        if (destination.scheme != "https"
                or destination.netloc != "www.instagram.com"
                or len(report["http"]) >= 12):
            stopped = "request_scope_or_budget"
            raise TrialStopped(stopped)
        kwargs["allow_redirects"] = False
        kwargs["timeout"] = (10, 15)
        event = {
            "method": request.method,
            "role": "bootstrap" if destination.path == "/" else "metadata",
        }
        report["http"].append(event)
        try:
            response = original_send(session, request, **kwargs)
        except requests.RequestException as error:
            event["network_error_type"] = type(error).__name__
            stopped = "network_error"
            raise TrialStopped(stopped) from error
        event.update({
            "status": response.status_code,
            "body_bytes": len(response.content),
            "body_sha256": hashlib.sha256(response.content).hexdigest(),
            "content_type": response.headers.get("Content-Type"),
        })
        if response.status_code >= 300:
            stopped = "http_" + str(response.status_code)
            response.close()
            raise TrialStopped(stopped)
        return response

    with instaloader.Instaloader(
        quiet=True,
        download_pictures=False,
        download_videos=False,
        download_video_thumbnails=False,
        download_comments=False,
        download_geotags=False,
        save_metadata=False,
        iphone_support=False,
        max_connection_attempts=1,
        request_timeout=15,
        fatal_status_codes=[401, 403, 429],
    ) as loader, patch.object(requests.Session, "send", bounded_send):
        for row in sample:
            result = {
                "recipe_id": row["recipe_id"],
                "source_url": row["source_url"],
                "shortcode": source_identity(row["source_url"]),
            }
            report["results"].append(result)
            if stopped:
                result.update(outcome="not_attempted_after_stop", reason=stopped)
                continue
            start = time.monotonic()
            try:
                post = instaloader.Post.from_shortcode(loader.context, result["shortcode"])
                if post.shortcode != result["shortcode"]:
                    stopped = "returned_identity_mismatch"
                    raise TrialStopped(stopped)
                caption = post.caption
                if caption:
                    args.private_caption_dir.mkdir(parents=True, exist_ok=True)
                    raw = caption.encode("utf-8")
                    caption_path = args.private_caption_dir / (row["recipe_id"] + ".txt")
                    with caption_path.open("xb") as stream:
                        stream.write(raw)
                    result.update(
                        outcome="caption_returned",
                        caption_characters=len(caption),
                        caption_sha256=hashlib.sha256(raw).hexdigest(),
                        recipe_completeness="not_assessed",
                    )
                else:
                    result["outcome"] = "metadata_returned_without_caption"
            except TrialStopped as error:
                result.update(outcome="trial_stopped", reason=str(error))
            except instaloader.InstaloaderException as error:
                stopped = "extractor_error"
                result.update(outcome="extractor_error", error_type=type(error).__name__)
            result["elapsed_seconds"] = round(time.monotonic() - start, 3)
            if not stopped:
                time.sleep(2)

    report["stop_reason"] = stopped
    report["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2, ensure_ascii=True)
        stream.write("\n")
    print(json.dumps({
        "output": str(args.output),
        "http_requests": len(report["http"]),
        "stop_reason": stopped,
        "outcomes": [result["outcome"] for result in report["results"]],
    }, indent=2))


if __name__ == "__main__":
    main()
