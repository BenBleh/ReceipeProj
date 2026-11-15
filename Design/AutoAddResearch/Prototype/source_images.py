"""Optional social-cover diagnostic. No login, video download or persisted media."""

import base64
import hashlib
import io
import time
import urllib.parse
import warnings

import requests
from PIL import Image, ImageOps

from drafts import DraftError
from live_sources import PublicAdapter, protect_session, public_url
from probe_sources import AGENT


def preview_jpeg(raw):
    with warnings.catch_warnings():
        warnings.simplefilter("error", Image.DecompressionBombWarning)
        with Image.open(io.BytesIO(raw)) as original:
            width, height = original.size
            if (original.format not in {"JPEG", "PNG", "WEBP"} or min(width, height) <= 0
                    or max(width, height) > 8192 or width * height > 16_000_000):
                raise DraftError("The thumbnail has unsupported format or dimensions.")
            with ImageOps.exif_transpose(original) as image:
                image.thumbnail((1600, 1600))
                with image.convert("RGB") as rgb, io.BytesIO() as output:
                    rgb.save(output, "JPEG", quality=85)
                    return output.getvalue(), rgb.width, rgb.height


def probe_thumbnail(importer, candidates, page, deadline, request_budget):
    result = {"status": "missing", "metadata_candidates": len(candidates), "http": [],
              "policy_mode": "RFC 9309; robots 403/404/410 unavailable, explicit exclusions still enforced"}
    if not candidates:
        result["reason"] = "No cover candidate was supplied by this matched social post."
        return result
    adapter = PublicAdapter(importer, image_fetch=True)
    adapter.deadline = min(deadline, time.monotonic() + 25)
    adapter.request_limit = min(6, request_budget)
    result["http"] = adapter.trace
    stage = "address"
    try:
        url = urllib.parse.urljoin(page, candidates[0])
        _, host = public_url(url)
        result["host"] = host
        with protect_session(requests.Session(), adapter) as session:
            session.headers.update({"User-Agent": AGENT + "/1.0 (local recipe draft prototype)",
                                    "Accept": "image/jpeg,image/png,image/webp;q=0.8"})
            for attempt in range(3):
                stage = "policy"
                adapter.policy_request = True
                try:
                    importer.robots_for(url, session, adapter, unavailable_statuses=(403, 404, 410))
                finally:
                    adapter.policy_request = False
                stage = "image_request"
                response = session.get(url, allow_redirects=False)
                if 300 <= response.status_code < 400:
                    location = response.headers.get("Location")
                    if not location or attempt == 2:
                        raise DraftError("The thumbnail redirect limit was reached.")
                    url = urllib.parse.urljoin(url, location)
                    public_url(url)
                    continue
                if response.status_code != 200:
                    raise DraftError(f"The thumbnail itself returned HTTP {response.status_code}.")
                stage = "decode"
                media = response.headers.get("Content-Type", "").split(";", 1)[0].strip().lower()
                if media not in {"image/jpeg", "image/png", "image/webp"}:
                    raise DraftError("The thumbnail returned an unsupported content type.")
                jpeg, width, height = preview_jpeg(response.content)
                result.update(status="downloaded", bytes=len(response.content), jpeg_bytes=len(jpeg),
                              width=width, height=height, sha256=hashlib.sha256(jpeg).hexdigest(),
                              preview="data:image/jpeg;base64," + base64.b64encode(jpeg).decode("ascii"))
                return result
    except requests.RequestException:
        result.update(status="unavailable", failed_stage=stage,
                      reason="The thumbnail network request failed; signed resource details were omitted.")
    except (ValueError, SyntaxError, OSError,
            Image.DecompressionBombError, Image.DecompressionBombWarning) as error:
        result.update(status="unavailable", failed_stage=stage, reason=str(error))
    finally:
        adapter.close()
    return result
