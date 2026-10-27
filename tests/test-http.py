#!/usr/bin/env python3
"""Smoke test of a published app: health, auth, settings and all six controls.
Usage: python3 tests/test-http.py /absolute/publish/path [dotnet executable]
"""
import http.cookiejar
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

publish = Path(sys.argv[1]).resolve()
dotnet = sys.argv[2] if len(sys.argv) > 2 else "dotnet"
with tempfile.TemporaryDirectory(prefix="xtream-http-") as temp:
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    env = dict(os.environ, CONFIG_FILE=f"{temp}/config/config.json", OUTPUT_ROOT=f"{temp}/media", ASPNETCORE_URLS=base)
    jar = http.cookiejar.MozillaCookieJar(f"{temp}/cookies.txt")
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(jar))
    def request(path, data=None):
        req = urllib.request.Request(base+path, None if data is None else json.dumps(data).encode(), headers={"Content-Type":"application/json"})
        with opener.open(req, timeout=10) as response:
            text = response.read().decode()
            return json.loads(text) if "json" in response.headers.get("Content-Type","") else text
    with open(f"{temp}/server.log", "w+") as log:
        process = subprocess.Popen([dotnet, str(publish/"XtreamStrmOrganizer.dll")], cwd=publish, env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            for attempt in range(100):
                try:
                    assert request("/health")["version"] == "1.8.2"
                    break
                except urllib.error.URLError:
                    if process.poll() is not None: raise RuntimeError("Application exited")
                    time.sleep(.1)
            else: raise TimeoutError("Startup timeout")
            request("/api/auth/setup", {"username":"testadmin","password":"test-admin-123"})
            try: request("/api/profiles"); raise AssertionError("Unauthenticated request accepted")
            except urllib.error.HTTPError as error: assert error.code == 401
            try: request("/api/warnings"); raise AssertionError("Unauthenticated log download")
            except urllib.error.HTTPError as error: assert error.code == 401
            request("/api/auth/login", {"username":"testadmin","password":"test-admin-123"})
            assert next(c for c in jar if c.name == "xtream_strm_session").expires is None, "Default is session-only"
            assert "Nenhum aviso" in request("/api/warnings")
            log_file = Path(temp)/"config/log_avisos.txt"
            log_file.write_text("Aviso: a capa do filme \"Teste\" não foi baixada. Motivo: HTTP 404.\n", encoding="utf-8")
            assert request("/api/warnings") == log_file.read_text()
            options = ["liveImages","liveNfo","movieImages","movieNfo","seriesImages","seriesNfo"]
            auto_options = ["auto"+key[0].upper()+key[1:] for key in options]
            html = request("/")
            for key in options+auto_options: assert html.count(f'id="{key}"') == 1, key
            assert "1.8.2" in html
            settings = dict(name="Teste",url="http://fixture.invalid",username="user",password="secret",live=True,movies=True,series=True,seriesConcurrency=8,intervalHours=24)
            settings.update(dict.fromkeys(options, True))
            settings.update(dict.fromkeys(auto_options, False))
            settings["autoMovieNfo"] = True
            saved = request("/api/settings",settings)
            settings["id"] = saved["id"]
            settings["password"] = ""
            settings["movieNfo"] = False
            request("/api/settings",settings)
            profiles = request("/api/profiles")
            assert len(profiles) == 1 and profiles[0]["passwordSaved"]
            for key in options: assert profiles[0][key] == (key != "movieNfo"), key
            for key in auto_options: assert profiles[0][key] == (key == "autoMovieNfo"), key
            assert "password" not in profiles[0]
            request("/api/auth/logout", {})
            try: request("/api/profiles"); raise AssertionError("Logout ineffective")
            except urllib.error.HTTPError as error: assert error.code == 401
            request("/api/auth/login", {"username":"testadmin","password":"test-admin-123","rememberMe":True})
            cookie = next(c for c in jar if c.name == "xtream_strm_session")
            assert 29*86400 < cookie.expires-time.time() <= 30*86400
            assert any(key.lower() == "httponly" for key in cookie._rest), "Cookie must be HttpOnly"
            jar.save()
            # Simulate a closed/reopened browser: load only cookies with persistent expiry.
            jar = http.cookiejar.MozillaCookieJar(f"{temp}/cookies.txt")
            jar.load()
            opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(jar))
            assert request("/api/auth/me")["username"] == "testadmin"
            process.terminate(); process.wait(timeout=10)
            process = subprocess.Popen([dotnet, str(publish/"XtreamStrmOrganizer.dll")], cwd=publish, env=env, stdout=log, stderr=subprocess.STDOUT)
            for attempt in range(100):
                try:
                    assert request("/api/auth/me")["username"] == "testadmin"
                    break
                except urllib.error.URLError: time.sleep(.1)
            else: raise TimeoutError("Session not retained after app restart")
            request("/api/auth/logout", {})
            assert not any(c.name == "xtream_strm_session" for c in jar)
            request("/api/auth/login", {"username":"testadmin","password":"test-admin-123","rememberMe":False})
            assert next(c for c in jar if c.name == "xtream_strm_session").expires is None
            print("PASS: 30-day persistent cookie, browser reopen, server restart, logout, session-only opt-out.")
            print("PASS: HTTP startup, health 1.8.2, setup/login/logout, access control, six controls, independent persistence and password preservation.")
        except Exception:
            log.seek(0)
            print(log.read())
            raise
        finally:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill(); process.wait()
