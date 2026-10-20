"""Nạp cấu hình riêng cho tiến trình backend; không thực thi nội dung .env."""
import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECTS = {"api": "ServiceDesk.Api", "worker": "ServiceDesk.Worker", "database": "ServiceDesk.Database"}
ALIASES = {
    "DATABASE_CONNECTION_STRING": "Database__ConnectionString",
    "JWT_SECRET_KEY": "Jwt__SecretKey",
    "JWT_ISSUER": "Jwt__Issuer",
    "JWT_AUDIENCE": "Jwt__Audience",
    "JWT_EXPIRY_MINUTES": "Jwt__ExpiryMinutes",
    "MINIO_ENDPOINT": "Minio__Endpoint",
    "MINIO_ROOT_USER": "Minio__AccessKey",
    "MINIO_ROOT_PASSWORD": "Minio__SecretKey",
    "MINIO_BUCKET": "Minio__Bucket",
    "MINIO_USE_SSL": "Minio__UseSsl",
}
CANONICAL = {key.casefold(): key for key in (*ALIASES.values(), "DOTNET_ENVIRONMENT",
    "ASPNETCORE_ENVIRONMENT", "ASPNETCORE_URLS")}


def normalize(values):
    # .NET và biến môi trường Windows không phân biệt hoa/thường.
    result = {CANONICAL.get(key.casefold(), key): value for key, value in values.items()}
    folded = {key.casefold(): value for key, value in values.items()}
    for alias, canonical in ALIASES.items():
        if canonical not in result and alias.casefold() in folded:
            result[canonical] = folded[alias.casefold()]
    return result


def load_environment(path, inherited):
    values = {}
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        key, separator, value = line.partition("=")
        key, value = key.strip(), value.strip()
        if not separator or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", key):
            raise ValueError(f"Dòng cấu hình không hợp lệ: {number}")
        if value.startswith(("\"", "'")):
            if len(value) < 2 or value[-1] != value[0]:
                raise ValueError(f"Dấu nháy không hợp lệ ở dòng: {number}")
            value = value[1:-1]
        values[key] = value
    # Môi trường tiến trình thắng tệp, kể cả khi dùng tên alias cũ.
    return {**normalize(values), **normalize(inherited)}


def configuration_errors(component, environment):
    required = ["Database__ConnectionString"]
    if component == "api":
        required += ["Jwt__SecretKey"]
    errors = []
    for key in required:
        value = environment.get(key, "").strip()
        if not value or "SET_LOCALLY" in value or "CHANGE_ME" in value:
            errors.append(f"Thiếu hoặc còn giá trị mẫu: {key}.")
    if component == "api":
        key = environment.get("Jwt__SecretKey", "")
        if key.strip() and len(key.encode("utf-8")) < 32:
            errors.append("Jwt__SecretKey phải có ít nhất 32 byte UTF-8.")
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description="Chạy backend với .env; không thay cấu hình toàn máy.")
    parser.add_argument("--env-file", type=Path, help="Mặc định đọc .env tại gốc repo nếu có.")
    parser.add_argument("--no-build", action="store_true", help="Chỉ dùng sau build Release thành công.")
    parser.add_argument("component", choices=PROJECTS)
    parser.add_argument("arguments", nargs=argparse.REMAINDER, help="Tham số ứng dụng, ví dụ: migrate")
    args = parser.parse_args(argv)
    try:
        path = args.env_file.resolve() if args.env_file else ROOT / ".env"
        if args.env_file or path.exists():
            environment = load_environment(path, dict(os.environ))
        else:
            environment = normalize(dict(os.environ))
        dotnet_environment = environment.get("DOTNET_ENVIRONMENT")
        aspnet_environment = environment.get("ASPNETCORE_ENVIRONMENT")
        if dotnet_environment and aspnet_environment and dotnet_environment != aspnet_environment:
            print("DOTNET_ENVIRONMENT và ASPNETCORE_ENVIRONMENT phải khớp nhau.", file=sys.stderr)
            return 2
        selected = dotnet_environment or aspnet_environment or "Development"
        environment.setdefault("DOTNET_ENVIRONMENT", selected)
        environment.setdefault("ASPNETCORE_ENVIRONMENT", selected)
        if args.component == "api":
            environment.setdefault("ASPNETCORE_URLS", "http://localhost:5000")
        errors = configuration_errors(args.component, environment)
        if errors:
            print("\n".join(errors), file=sys.stderr)
            print("Điền .env từ .env.example hoặc cấp biến môi trường cho lần chạy này; không gửi bí mật trong chat.",
                  file=sys.stderr)
            return 2
        command = ["dotnet", "run", "--project", str(ROOT / "src/backend" / PROJECTS[args.component]),
                   "-c", "Release", "--no-launch-profile"]
        if args.no_build:
            command.append("--no-build")
        command.extend(["--", *args.arguments])
        return subprocess.call(command, cwd=ROOT, env=environment)
    except FileNotFoundError:
        print("Không tìm thấy tệp --env-file hoặc công cụ dotnet; kiểm tra đường dẫn và .NET SDK.", file=sys.stderr)
        return 1
    except (OSError, ValueError, UnicodeError):
        print("Không đọc được cấu hình .env: kiểm tra KEY=value, dấu nháy và UTF-8; không in nội dung bí mật.",
              file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        return 130


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
