#!/bin/bash
# Apply the Obsidian OS 1.0 overlay to the proven base image and verify the result.
#
# Runs inside the WSL build distribution (Alpine/BusyBox based), as root.
# Never touches the source image, the console or the physical cards: it only
# reads the base image and writes a new output file.
#
# Environment (set by tools/build-os-image.ps1):
#   BASE_IMG      proven base image (read only)
#   OVERLAY_TAR   tar with top-level "p1/" and "p2/" trees (modes preserved)
#   OUT_IMG       output image to create (must not exist)
#   FORCE=1       allow overwriting an existing OUT_IMG
#   OVERLAY_JSON  overlay metadata produced by tools/stage-overlay.py
set -eu

SECTOR=512
P1_START=262144      # 128 MiB reserved before p1 (bootloader / kernel area)
P1_SECTORS=786432    # 384 MiB ext4 rootfs
P2_START=1048576
P2_SECTORS=1048576   # 512 MiB FAT32 home
BASE_SHA256_EXPECTED=a113b3c53a0d2c2c319f4ada65a694ccb6b16dbed053458a32669ba47b7ae7c5

: "${BASE_IMG:?BASE_IMG is required}"
: "${OVERLAY_TAR:?OVERLAY_TAR is required}"
: "${OUT_IMG:?OUT_IMG is required}"

fail() { echo "BUILD_FAILED: $*" >&2; exit 1; }
note() { echo "[build] $*"; }

[ -f "$BASE_IMG" ] || fail "base image not found: $BASE_IMG"
[ -f "$OVERLAY_TAR" ] || fail "overlay tar not found: $OVERLAY_TAR"

base_size=$(stat -c %s "$BASE_IMG")
[ "$base_size" = "1073741824" ] || fail "base image must be exactly 1073741824 bytes, found $base_size"

note "checking base image SHA-256 (this takes a moment)"
base_hash=$(sha256sum "$BASE_IMG" | cut -d' ' -f1)
[ "$base_hash" = "$BASE_SHA256_EXPECTED" ] || fail "base image hash mismatch: $base_hash"
note "base image hash OK"

if [ -e "$OUT_IMG" ]; then
    if [ "${FORCE:-0}" = "1" ]; then
        rm -f "$OUT_IMG"
    else
        fail "output already exists (set FORCE=1 to replace): $OUT_IMG"
    fi
fi

note "copying base image to output"
mkdir -p "$(dirname "$OUT_IMG")"
cp -f "$BASE_IMG" "$OUT_IMG"

L1=""
L2=""
cleanup() {
    sync
    mountpoint -q /mnt/img/p1 2>/dev/null && umount /mnt/img/p1
    mountpoint -q /mnt/img/p2 2>/dev/null && umount /mnt/img/p2
    [ -n "$L1" ] && losetup -d "$L1" 2>/dev/null
    [ -n "$L2" ] && losetup -d "$L2" 2>/dev/null
}
trap cleanup EXIT

L1=$(losetup -f); losetup -o $((P1_START*SECTOR)) "$L1" "$OUT_IMG"
L2=$(losetup -f); losetup -o $((P2_START*SECTOR)) "$L2" "$OUT_IMG"
mkdir -p /mnt/img/p1 /mnt/img/p2
mount -t ext4 "$L1" /mnt/img/p1 || fail "cannot mount rootfs loop"
mount -t vfat -o iocharset=utf8 "$L2" /mnt/img/p2 || fail "cannot mount home loop"

note "applying p1 (rootfs) overlay"
tar -C /mnt/img/p1 -xf "$OVERLAY_TAR" --strip-components=1 p1 || fail "p1 tar extract failed"

note "applying p2 (home) overlay"
tar -C /mnt/img/p2 -xf "$OVERLAY_TAR" --strip-components=1 p2 || fail "p2 tar extract failed"

# The USB proof script belongs to the laboratory and must not ship.
rm -f /mnt/img/p1/etc/init.d/S98obsisidian-usb-proof

# Permissions: tar preserves them, but vfat has none. Re-apply the executable
# bit set expected on the device (vfat mounts with umask=022 -> 0755 anyway).
chmod 0644 /mnt/img/p1/etc/os-release /mnt/img/p1/etc/obsidian-release 2>/dev/null || true
chmod 0755 /mnt/img/p1/usr/local/bin/appctl 2>/dev/null || true
chmod 0755 /mnt/img/p1/etc/init.d/rcS /mnt/img/p1/etc/init.d/S00gameman-quiet \
           /mnt/img/p1/etc/init.d/S12gameman-splash /mnt/img/p1/etc/init.d/S29obsidian-data \
           /mnt/img/p1/etc/init.d/S99appd 2>/dev/null || true
chmod 0755 /mnt/img/p1/usr/local/bin/brightstep.sh 2>/dev/null || true

sync
note "unmounting"
cleanup
trap - EXIT

note "filesystem checks"
# e2fsck needs a partition device, so open read-only loops for checking
C1=$(losetup -f); losetup -r -o $((P1_START*SECTOR)) "$C1" "$OUT_IMG"
C2=$(losetup -f); losetup -r -o $((P2_START*SECTOR)) "$C2" "$OUT_IMG"
set +e
e2fsck -fn "$C1" >/tmp/e2fsck.out 2>&1; e2=$?
fsck.vfat -n "$C2" >/tmp/fsckvfat.out 2>&1; vf=$?
set -e
losetup -d "$C1" 2>/dev/null || true
losetup -d "$C2" 2>/dev/null || true
note "e2fsck exit=$e2 fsck.vfat exit=$vf"
[ "$e2" -le 1 ] || { cat /tmp/e2fsck.out; fail "rootfs filesystem check failed"; }
[ "$vf" -le 1 ] || { cat /tmp/fsckvfat.out; fail "home filesystem check failed"; }

note "structural verification"
M1=$(losetup -f); losetup -r -o $((P1_START*SECTOR)) "$M1" "$OUT_IMG"
M2=$(losetup -f); losetup -r -o $((P2_START*SECTOR)) "$M2" "$OUT_IMG"
mkdir -p /mnt/verify/p1 /mnt/verify/p2
mount -t ext4 -o ro "$M1" /mnt/verify/p1
mount -t vfat -o ro,iocharset=utf8 "$M2" /mnt/verify/p2
verify_fail=0
require() { if [ ! -e "$1" ]; then echo "MISSING: $1"; verify_fail=1; fi; }
require /mnt/verify/p1/etc/os-release
require /mnt/verify/p1/etc/obsidian-release
require /mnt/verify/p1/usr/local/bin/appctl
require /mnt/verify/p1/etc/init.d/S00gameman-quiet
require /mnt/verify/p1/etc/init.d/S12gameman-splash
require /mnt/verify/p1/etc/init.d/S29obsidian-data
require /mnt/verify/p1/etc/init.d/S99appd
require /mnt/verify/p1/usr/bin/retroarch
require /mnt/verify/p1/usr/bin/puppy
require /mnt/verify/p1/usr/local/bin/brightstep.sh
require /mnt/verify/p1/etc/triggerhappy/triggers.d/fn.conf
require /mnt/verify/p1/etc/triggerhappy/triggers.d/power.conf
require /mnt/verify/p2/gameman-launcher
require /mnt/verify/p2/.local/launcher.sh
require /mnt/verify/p2/gameman-assets/fonts/Sora.ttf
require /mnt/verify/p2/gameman-assets/library/shelf.png
require /mnt/verify/p2/gameman-assets/video/ffplay-obsidian
require /mnt/verify/p2/emulators/drastic/drastic
require /mnt/verify/p2/fbneo_libretro.so
if [ -e /mnt/verify/p1/etc/init.d/S98obsisidian-usb-proof ]; then
    echo "PRESENT BUT MUST NOT SHIP: S98obsisidian-usb-proof"; verify_fail=1
fi

launcher_hash=$(sha256sum /mnt/verify/p2/gameman-launcher | cut -d' ' -f1)
note "launcher in image: $launcher_hash"

# Every init script and helper must be executable: an image whose services lose
# the +x bit receives "Permission denied" in rcS and never initialises
# (defect found through /home/player/boot-rc.log on revision r2).
non_exec=$(find /mnt/verify/p1/etc/init.d /mnt/verify/p1/usr/local/bin -type f -perm -u+x 2>/dev/null | wc -l)
total_scripts=$(find /mnt/verify/p1/etc/init.d /mnt/verify/p1/usr/local/bin -type f 2>/dev/null | wc -l)
note "executáveis em etc/init.d + usr/local/bin: ${non_exec}/${total_scripts}"
if [ "$non_exec" -ne "$total_scripts" ]; then
    echo "NON-EXECUTABLE SERVICE FILES:"
    find /mnt/verify/p1/etc/init.d /mnt/verify/p1/usr/local/bin -type f ! -perm -u+x 2>/dev/null | head -20
    verify_fail=1
fi

# No personal content, no laboratory backups, no books/saves/ROMs.
for pattern in 'gameman-backup*' '*before-*' 'n1.epub' 'n2.epub' 'diagnostico-hardware.txt' '*.sav' '*.state'; do
    found=$(find /mnt/verify/p2 -maxdepth 2 -name "$pattern" 2>/dev/null | head -3)
    if [ -n "$found" ]; then echo "PERSONAL/LAB FILE PRESENT: $found"; verify_fail=1; fi
done

p1_free=$(df -k /mnt/verify/p1 | tail -1 | awk '{print $4}')
p2_free=$(df -k /mnt/verify/p2 | tail -1 | awk '{print $4}')
p1_used=$(df -k /mnt/verify/p1 | tail -1 | awk '{print $3}')
p2_used=$(df -k /mnt/verify/p2 | tail -1 | awk '{print $3}')
note "rootfs used=${p1_used}K free=${p1_free}K ; home used=${p2_used}K free=${p2_free}K"
[ "$p1_free" -gt 20480 ] || { echo "rootfs has less than 20 MiB free"; verify_fail=1; }
[ "$p2_free" -gt 102400 ] || { echo "home has less than 100 MiB free"; verify_fail=1; }

if [ -n "${OVERLAY_JSON:-}" ] && [ -f "$OVERLAY_JSON" ]; then
    note "verifying overlay component hashes"
    if ! python3 - "$OVERLAY_JSON" /mnt/verify/p1 /mnt/verify/p2 <<'PY'
import hashlib, json, os, sys
manifest, p1, p2 = sys.argv[1], sys.argv[2], sys.argv[3]
data = json.load(open(manifest, encoding='utf-8'))
bad = 0
for comp in data.get('components', []):
    target = os.path.join(p1 if comp['partition'] == 'p1' else p2, comp['path'])
    if not os.path.exists(target):
        print('MISSING(manifest):', comp['path']); bad += 1; continue
    h = hashlib.sha256()
    with open(target, 'rb') as fh:
        for block in iter(lambda: fh.read(1 << 20), b''):
            h.update(block)
    if h.hexdigest() != comp['sha256']:
        print('HASH MISMATCH:', comp['path']); bad += 1
print(f'manifest components verified: {len(data.get("components", [])) - bad}/{len(data.get("components", []))}')
sys.exit(1 if bad else 0)
PY
    then
        verify_fail=1
    fi
fi

umount /mnt/verify/p1
umount /mnt/verify/p2
losetup -d "$M1"
losetup -d "$M2"

image_size=$(stat -c %s "$OUT_IMG")
image_hash=$(sha256sum "$OUT_IMG" | cut -d' ' -f1)
note "image: $OUT_IMG"
note "size: $image_size bytes"
note "sha256: $image_hash"

if [ "$verify_fail" -ne 0 ]; then
    echo "IMAGE_VERIFY_FAILED"
    exit 3
fi

cat <<JSON
{
  "status": "ok",
  "image": "$OUT_IMG",
  "size_bytes": $image_size,
  "sha256": "$image_hash",
  "base_image_sha256": "$base_hash",
  "launcher_sha256": "$launcher_hash",
  "p1": { "start_sector": $P1_START, "sectors": $P1_SECTORS, "filesystem": "ext4", "mount": "/" },
  "p2": { "start_sector": $P2_START, "sectors": $P2_SECTORS, "filesystem": "fat32", "mount": "/home/player" },
  "free_kib": { "rootfs": $p1_free, "home": $p2_free },
  "used_kib": { "rootfs": $p1_used, "home": $p2_used }
}
JSON
echo "IMAGE_BUILD_OK"
