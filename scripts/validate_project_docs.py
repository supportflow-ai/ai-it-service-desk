"""Kiểm tra nguồn và liên kết tài liệu bằng thư viện chuẩn, không truy cập mạng."""

import json
import posixpath
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote, urlsplit

KINDS = {"current", "decision", "proposal", "historical", "evidence"}
INLINE_LINK = re.compile(r'!?\[[^\]\n]*\]\(\s*(<[^>]+>|[^\s)]+)(?:\s+["\'][^\n]*?["\'])?\s*\)')
REFERENCE_LINK = re.compile(r'^\s*\[[^\]]+\]:\s*(<[^>]+>|\S+)')


def git_files(root):
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=root, capture_output=True, check=True,
    )
    return {name.decode("utf-8") for name in result.stdout.split(b"\0") if name}


def markdown_links(text):
    """Bỏ khối mã; trả vị trí liên kết nội tuyến và định nghĩa liên kết tham chiếu."""
    fence = None
    for number, line in enumerate(text.splitlines(), 1):
        marker = re.match(r"^\s{0,3}(`{3,}|~{3,})", line)
        if marker:
            token = marker.group(1)
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
            continue
        if fence:
            continue
        for match in INLINE_LINK.finditer(line):
            yield number, match.group(1).strip("<>")
        match = REFERENCE_LINK.match(line)
        if match:
            yield number, match.group(1).strip("<>")


def validate(root, files, manifest):
    errors, link_count = [], 0
    documents = manifest["documents"]
    groups = manifest["groups"]
    for prefix, kind in groups.items():
        if not prefix.endswith("/") or kind not in KINDS:
            errors.append(f"Nhóm nguồn không hợp lệ: {prefix}")
    for name, entry in documents.items():
        if name not in files or not (root / name).is_file():
            errors.append(f"Nguồn khai báo không có trong tập tệp công bố: {name}")
        if entry.get("kind") not in KINDS:
            errors.append(f"Loại nguồn không hợp lệ: {name}")
        replacement = entry.get("replacement")
        if replacement and (replacement not in files or not (root / replacement).is_file()):
            errors.append(f"Nguồn thay thế không được quản lý trong repo: {name} -> {replacement}")

    for name in sorted(f for f in files if f.endswith(".md")):
        path = root / name
        if not path.is_file():
            errors.append(f"Tệp đã xóa nhưng còn trong danh sách Git: {name}")
            continue
        entry = documents.get(name)
        if entry is None:
            matches = sorted((p for p in groups if name.startswith(p)), key=len, reverse=True)
            entry = {"kind": groups[matches[0]]} if matches else None
        if entry is None:
            errors.append(f"Tài liệu chưa phân loại nguồn: {name}")
        text = path.read_text(encoding="utf-8-sig")
        if entry and entry.get("notice"):
            # Chỉ xét phần mở đầu, tránh chấp nhận nhãn nằm trong bản gốc lịch sử.
            preamble = "\n".join(text.splitlines()[:12])
            if entry["notice"] not in preamble:
                errors.append(f"Thiếu chú thích trạng thái ở đầu tệp: {name}")
            replacement = entry.get("replacement")
            if replacement:
                expected = posixpath.relpath(replacement, posixpath.dirname(name))
                if expected not in preamble:
                    errors.append(f"Thiếu liên kết nguồn thay thế ở đầu tệp: {name}")
        for line, destination in markdown_links(text):
            parsed = urlsplit(destination)
            if parsed.scheme or parsed.netloc or not parsed.path:
                continue
            link_count += 1
            target = posixpath.normpath(posixpath.join(posixpath.dirname(name), unquote(parsed.path)))
            if target == ".." or target.startswith("../") or target.startswith("/"):
                errors.append(f"{name}:{line}: liên kết ra ngoài repo: {destination}")
                continue
            local = root / target
            published = target in files or (
                local.is_dir() and (bool(files) if target == "." else
                                    any(f.startswith(target.rstrip("/") + "/") for f in files))
            )
            if not local.exists() or not published:
                errors.append(f"{name}:{line}: đích không tồn tại/không được quản lý trong repo: {destination}")
    return errors, link_count


def main():
    root = Path(__file__).resolve().parents[1]
    try:
        manifest = json.loads((root / "docs/context-sources.json").read_text(encoding="utf-8"))
        files = git_files(root)
        errors, links = validate(root, files, manifest)
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Không kiểm tra được tài liệu ({type(error).__name__}).", file=sys.stderr)
        return 1
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    count = sum(name.endswith(".md") for name in files)
    print(f"Đạt: {count} tài liệu, {links} liên kết cục bộ; nguồn và chú thích hợp lệ.")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
