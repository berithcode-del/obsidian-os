#!/usr/bin/env python3
"""Pull a curated overlay from the console over the USB serial shell.

The console serial console is a plain BusyBox shell on a USB CDC-ACM link. It
reaches several MiB/s (much faster than MTP on this device) and never writes to
the console's storage: the tar is built in RAM (/dev/shm), streamed
base64-encoded in verified 4 MiB chunks with automatic retry, and the whole file
is compared against the SHA-256 reported by the console.

Protocol notes learned from the device:
  * long command lines get truncated, so commands are short and run one at a time;
  * line endings are CRLF, so markers are matched with an optional \\r;
  * `stat` is not available (BusyBox build), `wc -c < file` is;
  * single-shot large streams can drop a few bytes, so chunks are verified
    and retried instead of trusting one long transfer.

Usage:
    python tools/console-pull.py --out D:\\dsh-temp\\pull\\obsidian-1.0-overlay.tar
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import os
import re
import time

PULL_SETS = [
    "home/player/gameman-assets",
    "home/player/emulators/drastic",
    "home/player/.local",
    "home/player/.config",
    "home/player/gameman-launcher",
    "etc/init.d",
    "etc/triggerhappy/triggers.d",
    "etc/umtprd",
    "etc/fstab",
    "etc/os-release",
    "etc/hostname",
    "usr/local/bin/appctl",
    "usr/local/bin/brightstep.sh",
]
TAR_EXCLUDES = ["--exclude=test_rom", "--exclude=input_record", "--exclude=backup"]
BLOCK = 4096
CHUNK_BLOCKS = 1024          # 4 MiB per verified chunk
CHUNK_RETRIES = 4


def log(*args) -> None:
    print(*args, flush=True)


class Console:
    def __init__(self, port: str, baud: int) -> None:
        import serial

        self.ser = serial.Serial(port, baud, timeout=1, write_timeout=20)
        self._resync()

    def _resync(self) -> None:
        for _ in range(3):
            self.ser.write(b"\x03")
            time.sleep(0.3)
        self.ser.reset_input_buffer()
        time.sleep(0.4)
        text = self.ser.read(self.ser.in_waiting or 1).decode("utf-8", "replace")
        if "login:" in text or "Password:" in text:
            self.ser.write(b"root\n")
            time.sleep(0.6)
            self.ser.read(self.ser.in_waiting or 1)
            self.ser.write(b"root\n")
            time.sleep(0.9)
            self.ser.read(self.ser.in_waiting or 1)
        self.ser.write(b"stty -echo\n")
        time.sleep(0.3)
        self.ser.reset_input_buffer()
        self.cmd("echo shell-ready", timeout=10)

    def cmd(self, text: str, timeout: float = 60.0, marker: str = "::DONE::") -> str:
        """Run one short command and return everything printed before the marker."""
        assert len(text) < 480, "command too long for the console shell"
        tag = marker.encode()
        self.ser.reset_input_buffer()
        self.ser.write(f"{text}; echo {marker}\n".encode())
        buf = bytearray()
        end = time.time() + timeout
        while time.time() < end:
            chunk = self.ser.read(65536)
            if chunk:
                buf += chunk
                if tag in buf:
                    break
        if tag not in buf:
            raise TimeoutError(f"no marker for command: {text[:60]!r}")
        head = bytes(buf).split(tag)[0]
        return head.replace(b"\r", b"").decode("utf-8", "replace").strip()

    def stream_until_colon(self, text: str) -> bytes:
        """Run a base64 producer and return decoded bytes (stops at the ':' marker)."""
        self.ser.reset_input_buffer()
        self.ser.write((text + "\n").encode())
        raw = bytearray()
        carry = b""
        b64 = b""
        started = time.time()
        while True:
            chunk = self.ser.read(262144)
            if not chunk:
                if time.time() - started > 300:
                    raise TimeoutError("chunk stream timed out")
                continue
            data = carry + chunk
            idx = data.find(b":")
            if idx >= 0:
                payload, stop = data[:idx], True
            else:
                payload, stop = data[:-2], False
                carry = data[-2:]
            if payload:
                cleaned = re.sub(rb"[\r\n\t ]", b"", payload)
                if cleaned:
                    buffer = b64 + cleaned
                    usable = len(buffer) - (len(buffer) % 4)
                    if usable:
                        raw += base64.b64decode(buffer[:usable])
                    b64 = buffer[usable:]
            if stop:
                return bytes(raw)

    def close(self) -> None:
        self.ser.close()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", default="COM3")
    ap.add_argument("--baud", type=int, default=115200)
    ap.add_argument("--out", required=True)
    ap.add_argument("--remote-tar", default="/dev/shm/obsidian-pull.tar")
    ap.add_argument("--keep-remote", action="store_true")
    args = ap.parse_args()

    out = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    con = Console(args.port, args.baud)
    tar = args.remote_tar
    try:
        log("cleaning the RAM staging file on the console")
        con.cmd(f"rm -f {tar}", timeout=30)

        log("building the tar on the console")
        first = True
        for path in PULL_SETS:
            flag = "-cf" if first else "-rf"
            excludes = " ".join(TAR_EXCLUDES) if "drastic" in path else ""
            con.cmd(f"tar {flag} {tar} {excludes} {path}".strip(), timeout=600)
            log(f"  + {path}")
            first = False

        size_txt = con.cmd(f"wc -c < {tar}", timeout=30)
        m = re.search(r"(\d+)", size_txt)
        if not m:
            raise RuntimeError(f"could not read remote tar size: {size_txt!r}")
        remote_size = int(m.group(1))
        remote_hash = con.cmd(f"sha256sum {tar}", timeout=300).split()[0]
        log(f"console tar: {remote_size} bytes  sha256={remote_hash}")

        total_blocks = (remote_size + BLOCK - 1) // BLOCK
        chunks = [(i, min(CHUNK_BLOCKS, total_blocks - i)) for i in range(0, total_blocks, CHUNK_BLOCKS)]
        log(f"streaming {len(chunks)} verified chunks ({(CHUNK_BLOCKS*BLOCK)//2**20} MiB each)")

        digest = hashlib.sha256()
        written = 0
        started = time.time()
        with open(out, "wb") as fh:
            for number, (skip, count) in enumerate(chunks, start=1):
                expected_hash = con.cmd(
                    f"dd if={tar} bs={BLOCK} skip={skip} count={count} 2>/dev/null | sha256sum", timeout=120
                ).split()[0]
                data = None
                for attempt in range(1, CHUNK_RETRIES + 1):
                    blob = con.stream_until_colon(
                        f"dd if={tar} bs={BLOCK} skip={skip} count={count} 2>/dev/null | base64; echo ::CH::"
                    )
                    if hashlib.sha256(blob).hexdigest() == expected_hash and blob:
                        data = blob
                        break
                    log(f"  chunk {number}/{len(chunks)} attempt {attempt} mismatch ({len(blob)} bytes), retrying")
                    time.sleep(0.2)
                if data is None:
                    raise RuntimeError(f"chunk {number} could not be transferred intact")
                fh.write(data)
                digest.update(data)
                written += len(data)
                if number % 5 == 0 or number == len(chunks):
                    elapsed = time.time() - started
                    log(
                        f"  {written/2**20:7.1f} MiB  {written/elapsed/1024:6.1f} KiB/s"
                        f"  ({100.0*written/max(remote_size,1):5.1f}%)"
                    )

        local_hash = digest.hexdigest()
        log(f"received {written} bytes; local sha256={local_hash}")
        ok = written == remote_size and local_hash == remote_hash
        log("VERIFY:", "OK" if ok else "MISMATCH")
        if not ok:
            return 2
        with open(out + ".sha256", "w", encoding="ascii") as fh:
            fh.write(f"{local_hash}  {os.path.basename(out)}\n")
        return 0
    finally:
        if not args.keep_remote:
            try:
                con.cmd(f"rm -f {tar}", timeout=30)
            except Exception:  # noqa: BLE001
                pass
        con.close()


if __name__ == "__main__":
    raise SystemExit(main())
