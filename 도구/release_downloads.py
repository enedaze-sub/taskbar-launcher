import re

def windows_only_archive(data, repo, tag):
    import io, posixpath, zipfile
    from urllib.parse import quote
    source = zipfile.ZipFile(io.BytesIO(data))
    assert "실행파일/DeckLauncher.dll" in source.namelist(), "Unexpected Windows archive layout"
    output = io.BytesIO()
    removed, changed_docs = [], []
    roots = ("macOS", "리눅스")
    blob = "https://github.com/" + repo + "/blob/" + quote(tag, safe="") + "/"
    raw = "https://raw.githubusercontent.com/" + repo + "/" + quote(tag, safe="") + "/"
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as target:
        for item in source.infolist():
            name = item.filename.replace("\\", "/")
            assert not name.startswith("/") and ".." not in name.split("/"), "Invalid archive path"
            if name.split("/", 1)[0] in roots:
                removed.append(name)
                continue
            payload = source.read(item)
            if name.lower().endswith(".md"):
                text = payload.decode("utf-8-sig")
                def link(match):
                    value = match.group(1)
                    if value.startswith(("https://", "http://", "#")): return match.group(0)
                    resolved = posixpath.normpath(posixpath.join(posixpath.dirname(name), value))
                    if resolved.split("/", 1)[0] not in roots: return match.group(0)
                    base = raw if resolved.lower().endswith((".png", ".jpg", ".jpeg", ".gif", ".svg")) else blob
                    return "](" + base + quote(resolved) + ")"
                updated = re.sub(r"\]\(([^)]+)\)", link, text)
                if updated != text:
                    payload = updated.encode("utf-8")
                    changed_docs.append(name)
            target.writestr(item, payload)
    if not removed and not changed_docs:
        return data, {"RemovedPaths": [], "ChangedDocuments": []}
    result = output.getvalue()
    with zipfile.ZipFile(io.BytesIO(result)) as check:
        assert not any(n.split("/", 1)[0] in roots for n in check.namelist())
        assert not any("Data" in n.split("/") for n in check.namelist())
        for n in source.namelist():
            if n.lower().endswith((".dll", ".exe")):
                assert check.read(n) == source.read(n), "Windows executable changed"
        for n in ("README.md", "README.en.md", "README.ja.md", "README.zh-CN.md"):
            assert n in check.namelist()
    return result, {"RemovedPaths": removed, "ChangedDocuments": changed_docs}

def organize_release():
    import fnmatch, hashlib, json, os, re, urllib.request
    from urllib.parse import quote
    repo, tag = os.environ["REPOSITORY"], os.environ["RELEASE_TAG"]
    base = "https://api.github.com/repos/" + repo
    headers = {"Authorization": "Bearer " + os.environ["GH_TOKEN"], "Accept": "application/vnd.github+json", "Content-Type": "application/json", "X-GitHub-Api-Version": "2022-11-28"}
    def api(path, data=None, method=None):
        request = urllib.request.Request(base + path, headers=headers, data=None if data is None else json.dumps(data).encode(), method=method or ("GET" if data is None else "PATCH"))
        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read()
            return json.loads(raw) if raw else None
    release = api("/releases/tags/" + quote(tag, safe=""))
    assert not release["draft"], "Only published releases are organized"
    assets = release["assets"]
    
    # Validate, stage and verify the Windows-only archive before replacing its asset.
    windows = next((a for a in assets if fnmatch.fnmatchcase(a["name"], "taskbar-launcher-v*.zip")), None)
    if windows:
        with urllib.request.urlopen(windows["browser_download_url"], timeout=90) as response:
            original = response.read()
        assert "sha256:" + hashlib.sha256(original).hexdigest() == windows["digest"], "Existing ZIP checksum mismatch"
        revised, report = windows_only_archive(original, repo, tag)
        if revised != original:
            pending_name = windows["name"] + ".pending"
            assert not any(a["name"] == pending_name for a in assets), "A pending archive already exists; inspect it before retrying"
            def upload(name, payload):
                url = release["upload_url"].split("{", 1)[0] + "?name=" + quote(name, safe="")
                upload_headers = dict(headers, **{"Content-Type": "application/zip"})
                request = urllib.request.Request(url, headers=upload_headers, data=payload, method="POST")
                with urllib.request.urlopen(request, timeout=120) as response:
                    return json.load(response)
            staged = upload(pending_name, revised)
            assert staged["size"] == len(revised) and staged["digest"] == "sha256:" + hashlib.sha256(revised).hexdigest(), "Staged ZIP verification failed"
            removed_original = False
            try:
                api("/releases/assets/" + str(windows["id"]), method="DELETE")
                removed_original = True
                replacement = api("/releases/assets/" + str(staged["id"]), {"name": windows["name"], "label": ""})
                assert replacement["digest"] == staged["digest"]
            except Exception:
                if removed_original:
                    # Keep the former archive downloadable if the final rename fails.
                    current = api("/releases/tags/" + quote(tag, safe=""))
                    if not any(a["name"] == windows["name"] for a in current["assets"]):
                        upload(windows["name"], original)
                raise
            print("Windows archive refreshed: " + json.dumps(report, ensure_ascii=False))
            release = api("/releases/tags/" + quote(tag, safe=""))
            assets = release["assets"]
    # Check the two public source downloads without sending credentials to archive CDNs.
    source_links = []
    for suffix, signature, title in ((".zip", b"PK", "Source code (zip)"), (".tar.gz", b"\x1f\x8b", "Source code (tar.gz)")):
        url = "https://github.com/" + repo + "/archive/refs/tags/" + quote(tag, safe="") + suffix
        with urllib.request.urlopen(url, timeout=90) as response:
            prefix = response.read(4)
            assert prefix.startswith(signature), "Source archive is unavailable: " + title
        source_links.append((title, url))
        print("Source download verified: " + title)
    
    package_patterns = ("taskbar-launcher-v*.zip", "DeckLauncher-Linux*-v*.tar.gz", "DeckLauncher-macOS-v*.tar.gz")
    extra_patterns = ("linux-install-v*.md", "macos-install-v*.md", "usage-v*.md", "themes-v*.png", "windows-transparency-v*.png", "SHA256SUMS-v*.txt")
    matches = lambda name, patterns: any(fnmatch.fnmatchcase(name, p) for p in patterns)
    packages = [a for a in assets if matches(a["name"], package_patterns)]
    extras = [a for a in assets if matches(a["name"], extra_patterns)]
    unknown = [a for a in assets if a not in packages and a not in extras]
    assert packages, "No recognized installation package; ask the maintainer before changing assets"
    assert all(re.fullmatch(r"sha256:[0-9a-f]{64}", a.get("digest") or "") for a in packages), "Package checksums are missing; keep checksum assets until verified"
    snapshot = {a["id"]: {k: a.get(k) for k in ("name", "size", "digest", "browser_download_url")} for a in packages + unknown}
    web = "https://github.com/" + repo
    blob = web + "/blob/" + quote(tag, safe="") + "/"
    rawbase = "https://raw.githubusercontent.com/" + repo + "/" + quote(tag, safe="") + "/"
    def usage(a):
        name = a["name"]
        return "Windows ZIP" if name.startswith("taskbar-") else "Linux installer (preview)" if "-Linux" in name else "macOS app kit (preview)"
    rows = [ "| 설치 패키지 · 바로 다운로드 | 대상 |", "|---|---|"]
    for a in sorted(packages, key=lambda a: (not a["name"].startswith("taskbar-"), a["name"])):
        rows.append("| [" + a["name"] + "](" + a["browser_download_url"] + ") | " + usage(a) + " |")
    for title, url in source_links:
        rows.append("| [" + title + "](" + url + ") | 소스 코드 |")
    marker, endmarker = "<!-- asset-downloads:start -->", "<!-- asset-downloads:end -->"
    start, end = "<!-- package-checksums:start -->", "<!-- package-checksums:end -->"
    body = release.get("body") or ""
    for first,last in ((marker,endmarker),(start,end)):
        if first in body:
            assert last in body, "Incomplete generated section"
            body = body[:body.index(first)] + body[body.index(last)+len(last):]
    block = marker + "\n<details>\n<summary>다운로드 · SHA256</summary>\n\n" + "\n".join(rows)
    block += "\n\n### SHA256\n\n```text\n" + "\n".join(a["digest"].split(":",1)[1]+"  "+a["name"] for a in packages) + "\n```\n\n</details>\n" + endmarker
    # Replace links before removing the corresponding attachments.
    for a in extras:
        name = a["name"]
        destination = blob + "README.md"
        if name.startswith("linux-install"): destination = blob + quote("리눅스/README.md")
        elif name.startswith("macos-install"): destination = blob + "macOS/README.md"
        elif name.startswith("themes-"): destination = rawbase + quote("미리보기/themes/designs.png")
        elif name.startswith("windows-transparency-"): destination = rawbase + quote("미리보기/독립-투명도.png")
        elif name.startswith("SHA256SUMS-"): destination = "#sha256"
        body = body.replace(a["browser_download_url"], destination)
    body = body.strip()+"\n\n"+block+"\n"
    if body != release.get("body"): api("/releases/" + str(release["id"]), {"body": body})
    # Confirm all installer hashes are present before deleting known duplicates.
    confirmed = api("/releases/tags/" + quote(tag, safe=""))
    assert all(a["digest"].split(":", 1)[1] in confirmed["body"] for a in packages)
    for a in packages:
        if a.get("label"): api("/releases/assets/" + str(a["id"]), {"label": ""})
    for a in extras:
        api("/releases/assets/" + str(a["id"]), method="DELETE")
        print("Removed duplicate guide/preview/checksum: " + a["name"])
    for a in unknown:
        print("::warning::Keep unrecognized asset and ask the maintainer before changing policy: " + a["name"])
    updated = api("/releases/tags/" + quote(tag, safe=""))
    assert {a["id"] for a in updated["assets"]} == set(snapshot), "Unexpected assets were changed"
    for a in updated["assets"]:
        assert {k: a.get(k) for k in snapshot[a["id"]]} == snapshot[a["id"]], "Installer or unknown file changed"
    print("Verified: three installation packages and two source download links are available.")
    

if __name__ == "__main__":
    organize_release()
