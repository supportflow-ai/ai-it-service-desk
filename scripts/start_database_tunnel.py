"""Mở SSH tunnel theo metadata trong .env; không truyền credential database cho SSH."""

import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

from run_backend import ROOT, load_environment


def run_tunnel(command):
    retries = 0
    permanent_errors = (
        "permission denied", "host key verification failed", "remote host identification has changed",
        "address already in use", "bad permissions", "unprotected private key file",
    )
    while True:
        started = time.monotonic()
        result = subprocess.run(command, capture_output=True, text=True)
        if result.stderr:
            print(result.stderr.rstrip(), file=sys.stderr, flush=True)
        if result.returncode != 255 or any(error in result.stderr.lower() for error in permanent_errors):
            return result.returncode
        # Phiên đã ổn định được một phút không tính là lỗi kết nối liên tiếp.
        if time.monotonic() - started >= 60:
            retries = 0
        if retries >= 5:
            print("Không khôi phục được tunnel sau 5 lần thử lại; kiểm tra mạng/VPS.", file=sys.stderr)
            return result.returncode
        delay = min(2 ** retries, 30)
        retries += 1
        print(f"SSH bị gián đoạn; thử kết nối lại lần {retries}/5 sau {delay} giây.", flush=True)
        time.sleep(delay)


def main():
    parser = argparse.ArgumentParser(description="Mở SSH tunnel cho database dùng chung; Ctrl+C để dừng.")
    parser.add_argument("--env-file", type=Path, default=ROOT / ".env")
    args = parser.parse_args()
    try:
        config = load_environment(args.env_file.resolve(), dict(os.environ))
        host = config["DatabaseTunnel__Host"]
        user = config["DatabaseTunnel__SshUser"]
        ssh_port = int(config.get("DatabaseTunnel__SshPort", "22"))
        local_port = int(config.get("DatabaseTunnel__LocalPort", "15432"))
        remote_port = int(config.get("DatabaseTunnel__RemotePort", "5432"))
        key = Path(os.path.expandvars(config["DatabaseTunnel__PrivateKeyPath"])).expanduser().resolve()
        if (not re.fullmatch(r"[A-Za-z0-9.-]+", host)
                or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_-]*", user)
                or not all(1 <= value <= 65535 for value in (ssh_port, local_port, remote_port))
                or not key.is_file()):
            raise ValueError("invalid tunnel metadata")
        command = ["ssh", "-N", "-T", "-p", str(ssh_port), "-i", str(key),
                   "-o", "BatchMode=yes", "-o", "ExitOnForwardFailure=yes",
                   "-o", "StrictHostKeyChecking=yes", "-o", "ServerAliveInterval=30",
                   "-o", "ServerAliveCountMax=3",
                   "-L", f"127.0.0.1:{local_port}:127.0.0.1:{remote_port}", f"{user}@{host}"]
        print(f"Tunnel database: 127.0.0.1:{local_port} qua {host}. Ctrl+C để dừng.", flush=True)
        return run_tunnel(command)
    except KeyboardInterrupt:
        return 130
    except (OSError, ValueError, KeyError) as error:
        print(f"Không mở được tunnel ({type(error).__name__}); kiểm tra metadata/key/SSH, không in bí mật.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
