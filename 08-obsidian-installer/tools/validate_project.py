"""Validate project files without accessing USB devices or physical disks.

This is a development inventory/contract check, not a production package signature
verifier, disk writer, or hardware compatibility detector.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path, PureWindowsPath
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def contained_path(root: Path, name: str) -> Path:
    """Resolve manifest-controlled paths; external assets use a separate policy."""
    if not isinstance(name, str) or not name or "\\" in name:
        raise ValueError("invalid relative path")
    if Path(name).is_absolute() or PureWindowsPath(name).drive or ":" in name:
        raise ValueError("absolute/drive path not allowed")
    resolved = (root / name).resolve()
    if not resolved.is_relative_to(root.resolve()) or resolved == root.resolve():
        raise ValueError("path escapes project")
    return resolved


def validate_package(package: dict, profiles: dict, assets: dict, root: Path) -> list[str]:
    errors = []
    payload = package["payload"]
    for profile_id in package["compatible_profile_ids"]:
        if profile_id not in profiles:
            errors.append(f"unknown profile: {profile_id}")
    if package["customer_installation_allowed"]:
        # No customer signing/verifying pipeline has been implemented in this skeleton.
        errors.append("customer release is not implemented; laboratory catalog must remain blocked")
    if payload["kind"] == "external_reference":
        asset = assets.get(payload["asset_id"])
        if asset is None:
            errors.append("unknown external asset")
        elif (payload["sha256"] != asset.get("sha256")
              or payload["image_bytes"] != asset.get("bytes")):
            errors.append("payload hash/size differs from external inventory")
    else:
        try:
            path = contained_path(root, payload["file"])
            if not path.is_file():
                errors.append("bundled payload missing")
            elif path.stat().st_size != payload["image_bytes"] or sha256(path) != payload["sha256"]:
                errors.append("bundled payload integrity mismatch")
        except ValueError as exc:
            errors.append(str(exc))
    sector_bytes = package["sector_bytes"]
    if payload["image_bytes"] % sector_bytes:
        errors.append("image size is not sector-aligned")
    previous_end = 0
    roles = set()
    for part in sorted(package["partitions"], key=lambda item: item["start_sector"]):
        start = part["start_sector"]
        end = start + part["sector_count"]
        if start < previous_end:
            errors.append("partitions overlap")
        if end * sector_bytes > payload["image_bytes"]:
            errors.append("partition exceeds image")
        if part["role"] in roles:
            errors.append("duplicate partition role")
        roles.add(part["role"])
        previous_end = end
    return errors


def validate_state_machine(machine: dict) -> list[str]:
    states = machine["states"]
    errors = []
    initial, success = machine["initial"], machine["success"]
    for name in [initial, success, *machine["terminal_states"]]:
        if name not in states:
            errors.append(f"unknown state: {name}")
    for source, destinations in states.items():
        for dest in destinations:
            if dest not in states:
                errors.append(f"unknown transition: {source} -> {dest}")
    for terminal in machine["terminal_states"]:
        if states.get(terminal):
            errors.append(f"terminal state has outgoing transitions: {terminal}")
    for state in machine["required_guards"]:
        if state not in states:
            errors.append(f"guards for unknown state: {state}")
    if errors:
        return errors

    def reachable(destination, blocked=None):
        pending, visited = [initial], set()
        while pending:
            current = pending.pop()
            if current == blocked or current in visited:
                continue
            visited.add(current)
            pending.extend(states[current])
        return destination in visited

    for state in states:
        if not reachable(state):
            errors.append(f"unreachable state: {state}")
    # These gates must occur on every path to the consequential state.
    gates = {
        "BootstrapLoading": ["ProfileVerified", "PackageVerified", "AwaitingBootstrapConsent"],
        "Writing": ["TargetVerified", "AwaitingEraseConsent", "AwaitingElevation"],
        "PreparingStorage": ["Writing", "ReadbackVerifying"],
        success: ["ReadbackVerifying", "PreparingStorage", "AwaitingPowerCycle", "BootVerifying"],
    }
    for destination, required in gates.items():
        for guard in required:
            if guard not in states or destination not in states or reachable(destination, guard):
                errors.append(f"state gate bypass: {destination} requires {guard}")
    expected_guards = {
        "Writing": {"erase_consent_matches_plan", "uac_accepted", "immutable_plan_verified_by_helper",
                    "target_identity_rechecked", "exclusive_volume_lock", "package_hash_rechecked"},
        "Succeeded": {"boot_report_matches_installation_id", "system_version_matches_package",
                      "requested_game_directories_confirmed"},
    }
    for state, required in expected_guards.items():
        if not required.issubset(machine["required_guards"].get(state, [])):
            errors.append(f"missing contract guards: {state}")
    return errors


def validate_project(root: Path = ROOT, hash_external: bool = False) -> dict:
    from jsonschema import Draft202012Validator, FormatChecker

    errors, checked = [], {"profiles": 0, "packages": 0, "trusted_package_keys": 0, "copied_files": 0,
                           "component_files": 0, "external_assets": 0, "markdown_files": 0}
    profiles, packages = {}, []
    for folder, schema_name, id_key in [
        ("profiles", "hardware-profile.schema.json", "profile_id"),
        ("catalog", "image-package.schema.json", "package_id"),
    ]:
        schema = read_json(root / "schemas" / schema_name)
        Draft202012Validator.check_schema(schema)
        validator = Draft202012Validator(schema, format_checker=FormatChecker())
        seen = set()
        for path in sorted((root / folder).glob("*.json")):
            item = read_json(path)
            failures = list(validator.iter_errors(item))
            errors.extend(f"{path.name}: {failure.message}" for failure in failures)
            if failures:
                continue
            identity = item[id_key]
            if identity in seen or path.stem != identity:
                errors.append(f"{path.name}: duplicate/mismatched identifier")
            seen.add(identity)
            if folder == "profiles":
                profiles[identity] = item
                checked["profiles"] += 1
                if item["customer_installation_allowed"]:
                    errors.append(f"{identity}: customer profile release not implemented")
                bootstrap = item["bootstrap"]
                try:
                    binary = contained_path(root, bootstrap["file"])
                    if (not binary.is_file() or binary.stat().st_size != bootstrap["bytes"]
                            or sha256(binary) != bootstrap["sha256"]):
                        errors.append(f"{identity}: bootstrap integrity mismatch")
                except ValueError as exc:
                    errors.append(f"{identity}: {exc}")
            else:
                packages.append(item)
                checked["packages"] += 1
    if not profiles or not packages:
        errors.append("profile/package catalog cannot be empty")

    trust_schema = read_json(root / "schemas/trusted-package-keys.schema.json")
    Draft202012Validator.check_schema(trust_schema)
    Draft202012Validator.check_schema(read_json(root / "schemas/package-signature.schema.json"))
    trust_catalog = read_json(root / "trust/trusted-package-keys.json")
    trust_validator = Draft202012Validator(trust_schema, format_checker=FormatChecker())
    errors.extend(f"trusted-package-keys.json: {failure.message}" for failure in trust_validator.iter_errors(trust_catalog))
    trust_keys = trust_catalog.get("keys", [])
    key_ids = [key.get("key_id") for key in trust_keys]
    if len(key_ids) != len(set(key_ids)):
        errors.append("trusted-package-keys.json: duplicate signing key id")
    if any("PRIVATE KEY" in key.get("public_key_pem", "") for key in trust_keys):
        errors.append("trusted-package-keys.json: private key material is forbidden")
    checked["trusted_package_keys"] = len(trust_keys)

    inventory = read_json(root / "artifacts/external-assets.json")
    assets = {item["asset_id"]: item for item in inventory["assets"]}
    if len(assets) != len(inventory["assets"]):
        errors.append("duplicate external asset id")
    for package in packages:
        errors.extend(f"{package['package_id']}: {error}" for error in validate_package(package, profiles, assets, root))
    for item in assets.values():
        path = Path(item["location"])
        if not path.is_absolute():
            path = root / path
        if not path.is_file() or path.stat().st_size != item["bytes"]:
            errors.append(f"external asset missing/wrong size: {item['asset_id']}")
        elif hash_external and item.get("sha256") and sha256(path) != item["sha256"]:
            errors.append(f"external asset hash mismatch: {item['asset_id']}")
        checked["external_assets"] += 1

    migration = read_json(root / "artifacts/migration-manifest.json")
    targets = set()
    for item in migration["entries"]:
        try:
            target = contained_path(root, item["target"])
            if item["target"] in targets:
                errors.append(f"duplicate migration target: {item['target']}")
            targets.add(item["target"])
            if (not target.is_file() or target.stat().st_size != item["bytes"]
                    or sha256(target) != item["sha256"]):
                errors.append(f"copied file integrity mismatch: {item['target']}")
            checked["copied_files"] += 1
        except ValueError as exc:
            errors.append(f"migration: {exc}")
    for item in read_json(root / "third_party/components.json")["components"]:
        if item.get("file"):
            try:
                path = contained_path(root, item["file"])
                if not path.is_file() or sha256(path) != item["sha256"]:
                    errors.append(f"component integrity mismatch: {item['id']}")
                checked["component_files"] += 1
            except ValueError as exc:
                errors.append(f"component: {exc}")

    errors.extend(validate_state_machine(read_json(root / "contracts/install-state-machine.json")))
    project = read_json(root / "project.json")
    if project["customer_release_available"] and not (project["gui_implemented"] and project.get("build_validated")):
        errors.append("a customer release requires an implemented and built GUI")
    if project.get("gui_implemented") and not (root / "src/ObsidianInstaller.App/ObsidianInstaller.App.csproj").is_file():
        errors.append("GUI source project is missing")

    # Markdown destinations are checked locally; web links are never fetched here.
    for path in root.rglob("*.md"):
        if any(part in {"private", "legacy-2026-10-29", "bootstrap-build", "__pycache__"} for part in path.parts):
            continue
        for target in re.findall(r"(?<!!)\[[^\]\n]+\]\(([^)]+)\)", path.read_text(encoding="utf-8-sig")):
            target = target.strip().strip("<>")
            if urlsplit(target).scheme or target.startswith("#"):
                continue
            local = (path.parent / unquote(target.split("#", 1)[0])).resolve()
            if not local.exists():
                errors.append(f"broken local link in {path.relative_to(root)}: {target}")
        checked["markdown_files"] += 1
    return {"checked_at_utc": datetime.now(timezone.utc).isoformat(),
            "result": "passed" if not errors else "failed", "checks": checked,
            "external_image_hashes_recomputed": hash_external,
            "factory_backup_hash_recomputed": False,
            "scope": "local structure, schemas, design contracts and file integrity; no hardware test",
            "errors": errors}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--hash-external", action="store_true", help="also reread/hash the two referenced images (about 2 GiB)")
    args = parser.parse_args()
    report = validate_project(hash_external=args.hash_external)
    output = ROOT / "artifacts/validation-report.json"
    temporary = output.with_suffix(".tmp")
    temporary.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    temporary.replace(output)
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 0 if report["result"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
