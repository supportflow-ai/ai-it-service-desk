"""Tình huống hồi quy cho chốt chặn tài liệu; không cần repo hoặc mạng thật."""

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from validate_project_docs import markdown_links, validate


class ProjectDocsTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.files = {"README.md", "docs/current.md"}
        self.manifest = {"documents": {"README.md": {"kind": "current"}}, "groups": {"docs/": "current"}}
        self.write("README.md", "[Hiện hành](docs/current.md#chi-tiet)\n")
        self.write("docs/current.md", "# Hiện hành\n")

    def write(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def check(self):
        return validate(self.root, self.files, self.manifest)[0]

    def test_existing_file_is_not_enough_if_ignored_or_unpublished(self):
        self.write("README.md", "[Local](private-context.md)")
        self.write("private-context.md", "Không được công bố")
        self.assertTrue(any("không được quản lý" in e for e in self.check()))

    def test_missing_file_and_unknown_document_are_rejected(self):
        (self.root / "docs/current.md").unlink()
        self.files.add("unknown.md")
        self.write("unknown.md", "Nội dung chưa phân loại")
        errors = self.check()
        self.assertTrue(any("chưa phân loại" in e for e in errors))
        self.assertTrue(any("đích không tồn tại" in e for e in errors))

    def test_notice_and_replacement_must_be_in_preamble(self):
        self.manifest["documents"]["README.md"] = {
            "kind": "historical", "notice": "Lịch sử", "replacement": "docs/current.md"
        }
        self.assertTrue(any("Thiếu chú thích" in e for e in self.check()))
        self.write("README.md", "Lịch sử\n[Hiện hành](docs/current.md)")
        self.assertEqual([], self.check())
        self.write("README.md", "Lịch sử\n" + "Nội dung\n" * 12 + "[Hiện hành](docs/current.md)")
        self.assertTrue(any("Thiếu liên kết nguồn thay thế" in e for e in self.check()))

    def test_external_links_and_fenced_examples_are_not_files(self):
        self.write("README.md", "[Web](https://example.com)\n```md\n[x](missing.md)\n```\n~~~\n[x](missing.md)\n~~~\n")
        self.assertEqual([], self.check())

    def test_reference_links_and_encoded_spaces_resolve(self):
        self.files.add("docs/co dau.md")
        self.write("docs/co dau.md", "Nội dung")
        self.write("README.md", '[A](docs/co%20dau.md "Tiêu đề")\n[B][id]\n[id]: <docs/co dau.md>\n')
        self.assertEqual([], self.check())
        self.assertEqual(2, len(list(markdown_links((self.root / "README.md").read_text(encoding="utf-8")))))

    def test_directory_link_needs_published_content_and_cannot_escape_repo(self):
        (self.root / "empty").mkdir()
        self.write("README.md", "[Rỗng](empty/)\n[Ra ngoài](../outside.md)\n[Hợp lệ](docs/)\n[Gốc](./)\n")
        errors = self.check()
        self.assertEqual(2, len(errors))
        self.assertTrue(any("ra ngoài repo" in e for e in errors))


if __name__ == "__main__":
    unittest.main()
