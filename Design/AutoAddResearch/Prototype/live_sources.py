"""Explicit, bounded public-source lookups. No login, media or recipe storage."""

import contextlib
import datetime
import hashlib
import ipaddress
import re
import socket
import threading
import time
import urllib.parse
import urllib.robotparser
import zlib

import instaloader
import requests
from requests.adapters import HTTPAdapter
from urllib3.exceptions import HTTPError as ResponseError

from drafts import DraftError, caption_draft, source_key, tiktok_draft, website_draft
from probe_sources import AGENT


BODY_LIMIT = 8_000_000
TIKTOK_HOSTS = {"tiktok.com", "www.tiktok.com", "m.tiktok.com",
                "vm.tiktok.com", "vt.tiktok.com"}


def public_url(url):
    source_key(url)
    if len(url) > 4096 or any(ord(char) < 32 or ord(char) == 127 for char in url):
        raise DraftError("The live source URL is too long or contains control characters.")
    parsed = urllib.parse.urlsplit(url.strip())
    try:
        if parsed.port not in {None, 443}:
            raise DraftError("Live lookups accept only public HTTPS URLs on port 443.")
        host = parsed.hostname.encode("idna").decode("ascii").lower().rstrip(".")
    except (ValueError, UnicodeError) as error:
        raise DraftError("The source hostname or port is invalid.") from error
    return parsed._replace(fragment=""), host


def public_address(host):
    addresses = {entry[4][0] for entry in socket.getaddrinfo(
        host, 443, type=socket.SOCK_STREAM)}
    if not addresses or any(not ipaddress.ip_address(ip).is_global for ip in addresses):
        raise DraftError("Live lookups cannot contact local, private or reserved network addresses.")
    return sorted(addresses, key=lambda ip: (ipaddress.ip_address(ip).version, ip))[0]


def host_key(host):
    return host.removeprefix("www.")


class PublicAdapter(HTTPAdapter):
    def __init__(self, owner, instagram=False):
        self.owner = owner
        self.instagram = instagram
        self.trace = []
        self.deadline = time.monotonic() + 60
        super().__init__(max_retries=0)

    def get_connection_with_tls_context(self, request, verify, proxies=None, cert=None):
        _, host = public_url(request.url)
        address = public_address(host)
        _, pool = self.build_connection_pool_key_attributes(request, True, None)
        # Pin the validated address, but keep the original TLS/SNI identity.
        pool.update(assert_hostname=host, server_hostname=host)
        return self.poolmanager.connection_from_host(address, 443, "https", pool_kwargs=pool)

    def send(self, request, **kwargs):
        parsed, host = public_url(request.url)
        key = host_key(host)
        if self.instagram and (host != "www.instagram.com"
                               or parsed.path not in {"/", "/graphql/query", "/api/graphql"}):
            raise DraftError("Instagram requested an unsupported endpoint; paste its caption instead.")
        if request.method not in ({"GET", "POST"} if self.instagram else {"GET"}):
            raise DraftError("Unsupported source request method.")
        if key in self.owner.stopped:
            raise DraftError(self.owner.stopped[key])
        budget = 4 if self.instagram else 10
        if len(self.trace) >= budget:
            raise DraftError("The live lookup reached its request limit; paste the source text instead.")
        delay = max(0, self.owner.next_request.get(key, 0) - time.monotonic())
        if delay > 10:
            raise DraftError("This host needs a longer wait before another request. Try later or paste text.")
        if delay:
            time.sleep(delay)
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            raise DraftError("The live lookup reached its time limit; paste the source text instead.")
        request.headers["Host"] = parsed.netloc
        request.headers["Accept-Encoding"] = "identity"
        kwargs.update(stream=True, timeout=(min(10, remaining), min(10, remaining)),
                      verify=True, cert=None, proxies={})
        event = {"method": request.method, "host": host, "path": parsed.path}
        self.trace.append(event)
        self.owner.next_request[key] = time.monotonic() + 1
        try:
            response = super().send(request, **kwargs)
            event["status"] = response.status_code
            with response:
                status = response.status_code
                if status in {401, 403, 429} or (self.instagram and status >= 300):
                    reason = f"This host returned HTTP {status}; further lookups are stopped for this server session."
                    self.owner.stopped[key] = reason
                    raise DraftError(reason)
                if status >= 400 and status != 404:
                    raise DraftError(f"The source returned HTTP {status}.")
                limit = 1_000_000 if parsed.path.rstrip("/") == "/robots.txt" else BODY_LIMIT
                encoding = response.headers.get("Content-Encoding", "").lower()
                if encoding not in {"", "identity", "gzip", "deflate"}:
                    raise DraftError("The source returned an unsupported content encoding.")
                decoder = (zlib.decompressobj(31 if encoding == "gzip" else 15)
                           if encoding in {"gzip", "deflate"} else None)
                body = bytearray()
                received = 0
                while True:
                    if time.monotonic() > self.deadline:
                        raise DraftError("The source response exceeded the lookup time limit.")
                    chunk = response.raw.read1(65536, decode_content=False)
                    if not chunk:
                        break
                    received += len(chunk)
                    if received > limit:
                        raise DraftError("The source response exceeded the size limit.")
                    if decoder:
                        chunk = decoder.decompress(chunk, limit + 1 - len(body))
                    body.extend(chunk)
                    if len(body) > limit:
                        raise DraftError("The decoded source exceeded the size limit.")
                if decoder and (not decoder.eof or decoder.unused_data):
                    raise DraftError("The source returned incomplete or unsupported compressed data.")
                response._content = bytes(body)
                response._content_consumed = True
                event["body_bytes"] = len(body)
            return response
        except (requests.RequestException, ResponseError, OSError, zlib.error) as error:
            self.owner.stopped[key] = "A source network/response failure stopped further lookups for this server session."
            raise DraftError(self.owner.stopped[key]) from error


def protect_session(session, adapter):
    session.trust_env = False
    for scheme in ["https://", "http://"]:
        session.adapters[scheme].close()
        session.mount(scheme, adapter)
    return session


class InstagramContext(instaloader.InstaloaderContext):
    def __init__(self, adapter):
        self.adapter = adapter
        super().__init__(sleep=False, quiet=True, max_connection_attempts=1,
                         request_timeout=10, iphone_support=False,
                         fatal_status_codes=[401, 403, 429])

    def get_anonymous_session(self):
        return protect_session(super().get_anonymous_session(), self.adapter)

    @contextlib.contextmanager
    def _graphql_session(self, *args, **kwargs):
        # Instaloader copies sessions for GraphQL; protect those copies as well.
        with super()._graphql_session(*args, **kwargs) as session:
            yield protect_session(session, self.adapter)


def tiktok_identity(url):
    parsed, host = public_url(url)
    if host not in TIKTOK_HOSTS:
        raise DraftError("A TikTok link resolved outside the supported TikTok hosts.")
    match = re.fullmatch(r"/(?:@[^/]*/)?video/(\d+)/?", parsed.path)
    if match:
        return match[1]
    if ((host in {"vm.tiktok.com", "vt.tiktok.com"}
         and re.fullmatch(r"/[A-Za-z0-9]+/?", parsed.path))
            or re.fullmatch(r"/t/[A-Za-z0-9]+/?", parsed.path)):
        return None
    raise DraftError("The TikTok link does not identify a video. Paste a direct post URL or its caption.")


class LiveImporter:
    def __init__(self):
        self.lock = threading.Lock()
        self.robots = {}
        self.next_request = {}
        self.stopped = {"simplehomeedit.com": "This host returned HTTP 403 in the research trial; live requests remain stopped."}

    def robots_for(self, url, session, adapter):
        parsed, host = public_url(url)
        origin = "https://" + parsed.netloc
        cached = self.robots.get(origin)
        if cached and time.monotonic() - cached[0] < 3600:
            robot = cached[1]
        else:
            robots_url = origin + "/robots.txt"
            for attempt in range(3):
                response = session.get(robots_url, allow_redirects=False)
                if 300 <= response.status_code < 400:
                    location = response.headers.get("Location")
                    if not location:
                        raise DraftError("The robots policy redirect has no destination; source not requested.")
                    robots_url = urllib.parse.urljoin(robots_url, location)
                    public_url(robots_url)
                    if attempt == 2:
                        raise DraftError("The robots policy redirected too many times; source not requested.")
                    continue
                if response.status_code == 404:
                    robot = None
                elif response.status_code == 200:
                    if re.search(r"<(?:!doctype|html)\b", response.text[:1000], re.I):
                        raise DraftError("The robots policy returned HTML; source not requested.")
                    robot = urllib.robotparser.RobotFileParser()
                    robot.parse(response.text.splitlines())
                else:
                    raise DraftError("The robots policy is unavailable; source not requested.")
                break
            self.robots[origin] = (time.monotonic(), robot)
        if robot is not None:
            if not robot.can_fetch(AGENT, url):
                raise DraftError("The site's robots policy excludes this URL; paste its recipe text instead.")
            rate = robot.request_rate(AGENT)
            delay = max(1, robot.crawl_delay(AGENT) or 0,
                        rate.seconds / rate.requests if rate and rate.requests else 0)
            if delay > 10:
                raise DraftError("The site's crawl delay exceeds this prototype's limit; paste its text instead.")
            self.next_request[host_key(host)] = max(
                self.next_request.get(host_key(host), 0), time.monotonic() + delay)

    def public_page(self, source, adapter, tiktok):
        expected = tiktok_identity(source) if tiktok else None
        with protect_session(requests.Session(), adapter) as session:
            session.headers.update({"User-Agent": AGENT + "/1.0 (local recipe draft prototype)"})
            url = source
            for attempt in range(4):
                self.robots_for(url, session, adapter)
                response = session.get(url, allow_redirects=False)
                if 300 <= response.status_code < 400:
                    location = response.headers.get("Location")
                    if not location or attempt == 3:
                        raise DraftError("The source redirect could not be resolved within the lookup limit.")
                    target = urllib.parse.urljoin(url, location)
                    _, host = public_url(target)
                    if tiktok:
                        actual = tiktok_identity(target)
                        if expected and actual != expected:
                            raise DraftError("The redirect changed or lost the requested TikTok video identity.")
                        if actual:
                            expected = actual
                    elif host in TIKTOK_HOSTS or host_key(host) == "instagram.com":
                        raise DraftError("This link redirects to a social post; supply its direct post URL instead.")
                    url = target
                    continue
                if response.status_code != 200:
                    raise DraftError(f"The source returned HTTP {response.status_code}. Paste its text instead.")
                if not any(value in response.headers.get("Content-Type", "").lower()
                           for value in ["text/html", "application/xhtml+xml"]):
                    raise DraftError("The source did not return an HTML recipe/post page.")
                return response, url
        raise DraftError("No source page was returned.")

    def build(self, source):
        parsed, host = public_url(source)
        if not self.lock.acquire(blocking=False):
            raise DraftError("Another live lookup is running. Wait for it to finish before trying another URL.")
        instagram = host_key(host) == "instagram.com"
        adapter = PublicAdapter(self, instagram=instagram)
        try:
            if instagram:
                key = source_key(source)
                if not key.startswith("instagram:"):
                    raise DraftError("Use an Instagram /p/ or /reel/ post URL, not a profile or story.")
                shortcode = key.split(":", 1)[1]
                url = "https://www.instagram.com/p/" + shortcode + "/"
                try:
                    with contextlib.closing(InstagramContext(adapter)) as context:
                        post = instaloader.Post.from_shortcode(context, shortcode)
                        if post.shortcode != shortcode:
                            raise DraftError("Instagram returned a different post identity.")
                        caption = post.caption
                except instaloader.InstaloaderException as error:
                    self.stopped["instagram.com"] = "Anonymous Instagram lookup failed; further lookups are stopped for this server session."
                    raise DraftError(self.stopped["instagram.com"]) from error
                if not isinstance(caption, str) or not caption.strip():
                    raise DraftError("Instagram returned no caption. Paste the expanded description instead.")
                result = caption_draft(caption, source)
                raw = caption.encode("utf-8")
                platform = "instagram"
            else:
                tiktok = host in TIKTOK_HOSTS
                response, url = self.public_page(source, adapter, tiktok)
                raw = response.content
                result = (tiktok_draft(response.text, url, tiktok_identity(url)) if tiktok
                          else website_draft(response.text, url))
                platform = "tiktok" if tiktok else "website"
            result["evidence"].update(
                platform=platform, captured_source=False, live_source=True,
                requested_source=source, resolved_source=url,
                retrieved_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                source_sha256=hashlib.sha256(raw).hexdigest(), http=adapter.trace,
                network="Fresh anonymous lookup; no saved captures used or written")
            return result
        finally:
            adapter.close()
            self.lock.release()
