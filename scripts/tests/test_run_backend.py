"""Hồi quy cấu hình và CLI; không khởi chạy host hoặc truy cập database."""
import contextlib
import importlib.util
import io
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("run_backend", Path(__file__).parents[1] / "run_backend.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class EnvironmentTests(unittest.TestCase):
    def load(self, contents, inherited=None):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / ".env"
            path.write_text(contents, encoding="utf-8-sig")
            return runner.load_environment(path, inherited or {})

    def test_quotes_comments_bom_and_equals(self):
        values = self.load("# comment\n\nKEY='a=b;c'\nOTHER=\"tiếng Việt\"\n")
        self.assertEqual(values, {"KEY": "a=b;c", "OTHER": "tiếng Việt"})

    def test_values_are_never_executed_or_interpolated(self):
        literal = "$(echo sentinel) " + "$" + "{HOME}"
        self.assertEqual(self.load("KEY=" + literal)["KEY"], literal)

    def test_canonical_key_wins_alias_in_same_file(self):
        values = self.load("JWT_SECRET_KEY=old\nJwt__SecretKey=new")
        self.assertEqual(values["Jwt__SecretKey"], "new")

    def test_inherited_canonical_wins_file(self):
        self.assertEqual(self.load("JWT_SECRET_KEY=file", {"Jwt__SecretKey": "env"})["Jwt__SecretKey"], "env")

    def test_inherited_alias_wins_file_canonical(self):
        self.assertEqual(self.load("Jwt__SecretKey=file", {"JWT_SECRET_KEY": "env"})["Jwt__SecretKey"], "env")

    def test_windows_case_insensitive_names(self):
        self.assertEqual(self.load("Jwt__SecretKey=file", {"JWT__SECRETKEY": "env"})["Jwt__SecretKey"], "env")

    def test_canonical_wins_alias_in_inherited_environment(self):
        self.assertEqual(self.load("", {"JWT_SECRET_KEY": "old", "JWT__SECRETKEY": "new"})["Jwt__SecretKey"], "new")

    def test_malformed_line_rejected_without_value(self):
        for text in ("BAD-KEY=secret", "secret-without-equals", "KEY='unclosed-secret"):
            with self.subTest(text=text), self.assertRaises(ValueError) as error:
                self.load(text)
            self.assertNotIn("secret", str(error.exception))

    def test_parent_environment_unchanged(self):
        parent = {"Jwt__SecretKey": "parent"}
        self.load("KEY=file", parent)
        self.assertEqual(parent, {"Jwt__SecretKey": "parent"})


class CommandTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.api = {
            "Database__ConnectionString": "Host=127.0.0.1;Password=sentinel-private",
            "Jwt__SecretKey": "a" * 43,
            "Minio__Endpoint": "localhost:9000",
            "Minio__AccessKey": "local-user",
            "Minio__SecretKey": "sentinel-private",
        }

    def invoke(self, arguments, environment, file_contents=None):
        if file_contents is not None:
            (self.root / ".env").write_text(file_contents, encoding="utf-8")
        output = io.StringIO()
        with patch.object(runner, "ROOT", self.root), patch.dict(os.environ, environment, clear=True), \
                patch.object(runner.subprocess, "call", return_value=0) as call, \
                contextlib.redirect_stderr(output):
            status = runner.main(arguments)
        self.assertNotIn("sentinel-private", output.getvalue())
        return status, call, output.getvalue()

    def test_environment_only_without_dotenv(self):
        status, call, _ = self.invoke(["--no-build", "api"], self.api)
        self.assertEqual(status, 0)
        self.assertEqual(call.call_args.kwargs["env"]["DOTNET_ENVIRONMENT"], "Development")
        self.assertEqual(call.call_args.kwargs["env"]["ASPNETCORE_URLS"], "http://localhost:5000")
        command = call.call_args.args[0]
        self.assertIn("--no-launch-profile", command)
        self.assertIn("--no-build", command)
        self.assertEqual(command[command.index("-c") + 1], "Release")

    def test_loads_default_file(self):
        status, call, _ = self.invoke(["api"], {}, "\n".join(f"{key}={value}" for key, value in self.api.items()))
        self.assertEqual(status, 0)
        self.assertEqual(call.call_args.kwargs["env"]["Jwt__SecretKey"], self.api["Jwt__SecretKey"])

    def test_explicit_missing_file_fails_even_with_environment(self):
        status, call, _ = self.invoke(["--env-file", str(self.root / "absent"), "api"], self.api)
        self.assertEqual(status, 1)
        call.assert_not_called()

    def test_missing_configuration_lists_names_without_starting(self):
        status, call, output = self.invoke(["api"], {"Minio__SecretKey": "sentinel-private"})
        self.assertEqual(status, 2)
        self.assertIn("Jwt__SecretKey", output)
        self.assertIn("Database__ConnectionString", output)
        call.assert_not_called()

    def test_sample_configuration_rejected(self):
        status, call, _ = self.invoke(["api"], {**self.api, "Jwt__SecretKey": "CHANGE_ME" + "a" * 32})
        self.assertEqual(status, 2)
        call.assert_not_called()

    def test_core_api_does_not_require_unused_storage(self):
        status, call, _ = self.invoke(["api"], {
            "Database__ConnectionString": self.api["Database__ConnectionString"],
            "Jwt__SecretKey": self.api["Jwt__SecretKey"],
        })
        self.assertEqual(status, 0)
        call.assert_called_once()

    def test_short_jwt_rejected(self):
        status, call, output = self.invoke(["api"], {**self.api, "Jwt__SecretKey": "short"})
        self.assertEqual(status, 2)
        self.assertIn("32 byte", output)
        call.assert_not_called()

    def test_jwt_length_is_utf8_bytes(self):
        status, _, _ = self.invoke(["api"], {**self.api, "Jwt__SecretKey": "á" * 16})
        self.assertEqual(status, 0)

    def test_database_only_needs_database_configuration(self):
        status, call, _ = self.invoke(["--no-build", "database", "migrate"],
                                     {"Database__ConnectionString": self.api["Database__ConnectionString"]})
        self.assertEqual(status, 0)
        self.assertEqual(call.call_args.args[0][-2:], ["--", "migrate"])

    def test_worker_only_needs_database_configuration(self):
        status, _, _ = self.invoke(["worker"], {"Database__ConnectionString": self.api["Database__ConnectionString"]})
        self.assertEqual(status, 0)

    def test_explicit_runtime_and_url_are_preserved(self):
        status, call, _ = self.invoke(["api"], {**self.api, "DOTNET_ENVIRONMENT": "Production",
                                               "ASPNETCORE_URLS": "http://127.0.0.1:5500"})
        self.assertEqual(status, 0)
        self.assertEqual(call.call_args.kwargs["env"]["ASPNETCORE_ENVIRONMENT"], "Production")
        self.assertEqual(call.call_args.kwargs["env"]["ASPNETCORE_URLS"], "http://127.0.0.1:5500")

    def test_conflicting_environments_rejected(self):
        status, call, _ = self.invoke(["api"], {**self.api, "DOTNET_ENVIRONMENT": "Production",
                                               "ASPNETCORE_ENVIRONMENT": "Development"})
        self.assertEqual(status, 2)
        call.assert_not_called()

    def test_child_exit_code_is_preserved(self):
        with patch.object(runner, "ROOT", self.root), patch.dict(os.environ, self.api, clear=True), \
                patch.object(runner.subprocess, "call", return_value=7):
            self.assertEqual(runner.main(["api"]), 7)


if __name__ == "__main__":
    unittest.main()
