"""Loopback-only recipe review prototype. Explicit live lookups; no recipe storage."""

import argparse
import collections
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import logging
from pathlib import Path
import urllib.parse

from drafts import Catalog, DraftError, caption_draft, validate_export
from live_sources import LiveImporter


ROOT = Path(__file__).resolve().parent
LOG = logging.getLogger("draft-prototype")


class Handler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        LOG.info(format, *args)

    def allowed(self):
        origins = {f"http://127.0.0.1:{self.server.server_port}",
                   f"http://localhost:{self.server.server_port}"}
        return ("http://" + self.headers.get("Host", "") in origins
                and (not self.headers.get("Origin") or self.headers["Origin"] in origins))

    def respond(self, status, body, content_type="application/json; charset=utf-8"):
        raw = json.dumps(body, ensure_ascii=False).encode("utf-8") if isinstance(body, dict) else body
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(raw)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy",
                         "default-src 'self'; script-src 'self'; style-src 'self'; "
                         "img-src 'none'; connect-src 'self'; base-uri 'none'; "
                         "form-action 'none'; frame-ancestors 'none'")
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self):
        if not self.allowed():
            return self.respond(403, {"error": "Only local, same-origin access is allowed."})
        path = urllib.parse.urlsplit(self.path).path
        if path == "/api/health":
            return self.respond(200, {"service": "nibbles-draft-prototype", "offline": False,
                                      "live_sources": ["website", "instagram", "tiktok"],
                                      "captures": len(self.server.catalog.captures)})
        if path == "/api/samples":
            return self.respond(200, {"samples": self.server.catalog.samples()})
        if path == "/favicon.ico":
            return self.respond(204, b"")
        static = {"/": ("index.html", "text/html; charset=utf-8"),
                  "/app.js": ("app.js", "text/javascript; charset=utf-8"),
                  "/styles.css": ("styles.css", "text/css; charset=utf-8")}
        if path not in static:
            return self.respond(404, {"error": "Not found."})
        name, content_type = static[path]
        self.respond(200, (ROOT / name).read_bytes(), content_type)

    def do_POST(self):
        if not self.allowed():
            return self.respond(403, {"error": "Only local, same-origin access is allowed."})
        if self.path not in {"/api/draft", "/api/export"}:
            return self.respond(404, {"error": "Not found."})
        if self.headers.get("Content-Type", "").split(";")[0] != "application/json":
            return self.respond(415, {"error": "Send application/json."})
        try:
            size = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            return self.respond(400, {"error": "Invalid request length."})
        if not 0 < size <= 100_000:
            return self.respond(413, {"error": "Request must be between 1 and 100000 bytes."})
        try:
            data = json.loads(self.rfile.read(size))
            if not isinstance(data, dict):
                raise DraftError("Expected a JSON object.")
            if self.path == "/api/export":
                if data.get("reviewed") is not True:
                    raise DraftError("Confirm that you reviewed the draft before exporting.")
                draft = validate_export(data.get("draft"))
                return self.respond(200, {"draft": draft})
            source = data.get("source", "")
            caption = data.get("caption", "")
            if not isinstance(source, str) or not isinstance(caption, str):
                raise DraftError("Source URL and caption must be text.")
            if data.get("mode") == "paste":
                if source:
                    from drafts import source_key
                    source_key(source)
                result = caption_draft(caption, source)
                result["evidence"].update(captured_source=False,
                                          network="User-supplied text; no live source request")
            elif data.get("mode") == "capture":
                result = self.server.catalog.build(source)
            elif data.get("mode") == "live":
                result = self.server.live_importer.build(source)
            else:
                raise DraftError("Choose a live lookup, local capture or pasted text.")
            self.respond(200, result)
        except (DraftError, json.JSONDecodeError, UnicodeDecodeError) as error:
            self.respond(422, {"error": str(error)})
        except OSError:
            LOG.exception("Could not read the selected retained source")
            self.respond(500, {"error": "The retained source could not be read. Check the server log."})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--research", type=Path, default=ROOT.parent)
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--instagram-captions", type=Path, required=True)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--evaluate", type=Path)
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    catalog = Catalog(args.research, args.cache, args.instagram_captions)
    if args.evaluate:
        rows = []
        for sample in catalog.samples():
            try:
                result = catalog.build(sample["url"])
                draft = result["draft"]
                rows.append({**sample, "ingredients": len(draft["Ingredients"]),
                             "steps": len(draft["Steps"]), "groups": len(draft["IngredientGroups"]),
                             "missing": [field for field in ["Title", "Ingredients", "Steps"] if not draft[field]],
                             "warnings": result["warnings"]})
            except DraftError as error:
                rows.append({**sample, "error": str(error)})
        with args.evaluate.open("x", encoding="utf-8") as stream:
            json.dump({"scope": "Offline extraction diagnostics, not semantic accuracy",
                       "sources": rows}, stream, indent=2, ensure_ascii=True)
            stream.write("\n")
        print(json.dumps({"evaluated": len(rows),
                          "by_platform": dict(collections.Counter(row["platform"] for row in rows)),
                          "errors": sum("error" in row for row in rows)}))
        return
    with ThreadingHTTPServer(("127.0.0.1", args.port), Handler) as server:
        server.catalog = catalog
        server.live_importer = LiveImporter()
        print(f"Draft review: http://127.0.0.1:{server.server_port}", flush=True)
        print(f"{len(catalog.captures)} local captures plus explicit live lookups; no repository writes.", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
