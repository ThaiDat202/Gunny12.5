#!/usr/bin/env python3
"""Chup screenshot GunnyPanel qua Chrome headless + CDP (khong can Playwright).

Cach dung:
  python _tools/screenshot-gunnypanel.py <url-path> <out.png> [--width 1440] [--user admin] [--pass Admin@123]
Vi du:
  python _tools/screenshot-gunnypanel.py "/FlashRip?job=<id>" data/rip/_verify/flashrip-result.png
"""
import base64
import http.cookiejar
import json
import os
import socket
import struct
import subprocess
import sys
import time
import urllib.parse
import urllib.request

BASE = "http://127.0.0.1:91"
CDP_PORT = 9223
CHROME_CANDIDATES = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]


# ------------------------------------------------------------------ login
def login(user, password):
    jar = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    html = opener.open(BASE + "/Login", timeout=15).read().decode("utf-8", "replace")
    import re
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    if not m:
        raise RuntimeError("khong thay antiforgery token")
    data = urllib.parse.urlencode({
        "UserName": user,
        "Password": password,
        "__RequestVerificationToken": m.group(1),
    }).encode()
    opener.open(BASE + "/Login", data=data, timeout=15).read()
    for c in jar:
        if c.name == "GunnyPanel.Auth":
            return c.value
    raise RuntimeError("login that bai (khong co cookie GunnyPanel.Auth)")


# ------------------------------------------------------------------ websocket
class WebSocket:
    def __init__(self, url):
        u = urllib.parse.urlparse(url)
        self.sock = socket.create_connection((u.hostname, u.port), timeout=20)
        key = base64.b64encode(os.urandom(16)).decode()
        path = u.path + (("?" + u.query) if u.query else "")
        req = (
            f"GET {path} HTTP/1.1\r\nHost: {u.hostname}:{u.port}\r\n"
            "Upgrade: websocket\r\nConnection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n"
        )
        self.sock.sendall(req.encode())
        buf = b""
        while b"\r\n\r\n" not in buf:
            buf += self.sock.recv(4096)
        if b" 101 " not in buf.split(b"\r\n")[0]:
            raise RuntimeError("websocket handshake failed: " + buf[:200].decode("latin-1"))

    def _read_exact(self, n):
        buf = b""
        while len(buf) < n:
            chunk = self.sock.recv(n - len(buf))
            if not chunk:
                raise EOFError("socket closed")
            buf += chunk
        return buf

    def _send_frame(self, opcode, payload):
        header = bytearray([0x80 | opcode])
        mask = os.urandom(4)
        n = len(payload)
        if n < 126:
            header.append(0x80 | n)
        elif n < 65536:
            header.append(0x80 | 126)
            header += struct.pack(">H", n)
        else:
            header.append(0x80 | 127)
            header += struct.pack(">Q", n)
        header += mask
        masked = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
        self.sock.sendall(bytes(header) + masked)

    def send(self, obj):
        self._send_frame(0x1, json.dumps(obj).encode())

    def recv(self):
        chunks = []
        while True:
            h = self._read_exact(2)
            fin = h[0] & 0x80
            opcode = h[0] & 0x0F
            b2 = h[1]
            n = b2 & 0x7F
            masked = b2 & 0x80
            if n == 126:
                n = struct.unpack(">H", self._read_exact(2))[0]
            elif n == 127:
                n = struct.unpack(">Q", self._read_exact(8))[0]
            mask = self._read_exact(4) if masked else None
            payload = self._read_exact(n)
            if mask:
                payload = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
            if opcode == 8:
                raise EOFError("websocket closed")
            if opcode == 9:
                self._send_frame(0xA, payload)
                continue
            chunks.append(payload)
            if fin:
                return b"".join(chunks)

    def close(self):
        try:
            self.sock.close()
        except OSError:
            pass


class CDP:
    def __init__(self, ws):
        self.ws = ws
        self.next_id = 0

    def call(self, method, params=None, timeout=40):
        self.next_id += 1
        mid = self.next_id
        self.ws.send({"id": mid, "method": method, "params": params or {}})
        end = time.time() + timeout
        while time.time() < end:
            msg = json.loads(self.ws.recv())
            if msg.get("id") == mid:
                if "error" in msg:
                    raise RuntimeError(f"{method}: {msg['error']}")
                return msg.get("result", {})
        raise TimeoutError(method)


# ------------------------------------------------------------------ main
def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    path = sys.argv[1]
    out = sys.argv[2]
    width = 1440
    user, password = "admin", "Admin@123"
    eval_js = None
    wait_ms = 2500
    fullpage = True
    if "--width" in sys.argv:
        width = int(sys.argv[sys.argv.index("--width") + 1])
    if "--user" in sys.argv:
        user = sys.argv[sys.argv.index("--user") + 1]
    if "--pass" in sys.argv:
        password = sys.argv[sys.argv.index("--pass") + 1]
    if "--eval" in sys.argv:
        eval_js = sys.argv[sys.argv.index("--eval") + 1]
    if "--wait-ms" in sys.argv:
        wait_ms = int(sys.argv[sys.argv.index("--wait-ms") + 1])
    if "--no-fullpage" in sys.argv:
        fullpage = False

    cookie = login(user, password)
    chrome = next((p for p in CHROME_CANDIDATES if os.path.isfile(p)), None)
    if not chrome:
        raise RuntimeError("khong tim thay Chrome/Edge")
    profile = os.path.join(os.environ.get("TEMP", "."), "gp-shot-profile")
    os.makedirs(profile, exist_ok=True)
    proc = subprocess.Popen(
        [
            chrome, "--headless=new", f"--remote-debugging-port={CDP_PORT}",
            f"--user-data-dir={profile}", "--no-first-run", "--no-default-browser-check",
            "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
            "--window-size=%d,1000" % width, "about:blank",
        ],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    try:
        version = None
        for _ in range(60):
            try:
                version = json.load(urllib.request.urlopen(f"http://127.0.0.1:{CDP_PORT}/json/version", timeout=2))
                break
            except Exception:
                time.sleep(0.4)
        if not version:
            raise RuntimeError("Chrome CDP khong len")
        targets = json.load(urllib.request.urlopen(f"http://127.0.0.1:{CDP_PORT}/json", timeout=5))
        page = next(t for t in targets if t.get("type") == "page")
        ws = WebSocket(page["webSocketDebuggerUrl"])
        cdp = CDP(ws)
        cdp.call("Network.enable")
        cdp.call("Page.enable")
        cdp.call("Network.setCookie", {"name": "GunnyPanel.Auth", "value": cookie, "url": BASE + "/", "path": "/"})
        cdp.call("Emulation.setDeviceMetricsOverride",
                 {"width": width, "height": 1000, "deviceScaleFactor": 1, "mobile": False})
        url = BASE + path
        cdp.call("Page.navigate", {"url": url})
        for _ in range(60):
            state = cdp.call("Runtime.evaluate", {"expression": "document.readyState", "returnByValue": True})
            if state.get("result", {}).get("value") == "complete":
                break
            time.sleep(0.3)
        time.sleep(1.2)
        if eval_js:
            ev = cdp.call("Runtime.evaluate", {"expression": eval_js, "returnByValue": True})
            print("eval =>", json.dumps(ev.get("result", {}).get("value"), ensure_ascii=True))
            time.sleep(max(0.3, wait_ms / 1000.0))
        if not fullpage:
            shot = cdp.call("Page.captureScreenshot", {"format": "png"}, timeout=60)
            data = base64.b64decode(shot["data"])
            os.makedirs(os.path.dirname(os.path.abspath(out)) or ".", exist_ok=True)
            with open(out, "wb") as f:
                f.write(data)
            print(f"saved {out} ({len(data)} bytes, {width}x1000 viewport) url={url}")
            ws.close()
            return 0
        metrics = cdp.call("Page.getLayoutMetrics")
        height = int(metrics["cssContentSize"]["height"]) if "cssContentSize" in metrics else int(metrics["contentSize"]["height"])
        height = max(800, min(height + 20, 9000))
        cdp.call("Emulation.setDeviceMetricsOverride",
                 {"width": width, "height": height, "deviceScaleFactor": 1, "mobile": False})
        time.sleep(0.8)
        shot = cdp.call("Page.captureScreenshot", {"format": "png", "captureBeyondViewport": False}, timeout=60)
        data = base64.b64decode(shot["data"])
        os.makedirs(os.path.dirname(os.path.abspath(out)) or ".", exist_ok=True)
        with open(out, "wb") as f:
            f.write(data)
        print(f"saved {out} ({len(data)} bytes, {width}x{height}) url={url}")
        ws.close()
        return 0
    finally:
        try:
            proc.terminate()
        except Exception:
            pass


if __name__ == "__main__":
    sys.exit(main())
