"""Negative cases for metadata guards; never interact with attached storage."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from validate_project import contained_path, read_json, validate_package, validate_state_machine


class ProjectContractTests(unittest.TestCase):
    def setUp(self):
        self.package = read_json(ROOT / "catalog/goodluck-base-usb-proof-20261029.json")
        self.profile = read_json(ROOT / "profiles/ga36-a33-rhododendron-panel1-lab.json")
        self.profiles = {self.profile["profile_id"]: self.profile}
        self.assets = {item["asset_id"]: item for item in read_json(ROOT / "artifacts/external-assets.json")["assets"]}
        self.machine = read_json(ROOT / "contracts/install-state-machine.json")

    def errors(self):
        return validate_package(self.package, self.profiles, self.assets, ROOT)

    def test_current_contracts_are_consistent(self):
        self.assertEqual(self.errors(), [])
        self.assertEqual(validate_state_machine(self.machine), [])

    def test_changed_image_digest_rejected(self):
        self.package["payload"]["sha256"] = "0" * 64
        self.assertTrue(any("hash/size" in error for error in self.errors()))

    def test_unknown_profile_rejected(self):
        self.package["compatible_profile_ids"] = ["unrecognized-board"]
        self.assertTrue(any("unknown profile" in error for error in self.errors()))

    def test_lab_package_cannot_be_customer_release(self):
        self.package["customer_installation_allowed"] = True
        self.assertTrue(any("customer release" in error for error in self.errors()))

    def test_overlapping_or_out_of_bounds_partitions_rejected(self):
        original = copy.deepcopy(self.package)
        self.package["partitions"][1]["start_sector"] = 262145
        self.assertIn("partitions overlap", self.errors())
        self.package = original
        self.package["partitions"][1]["sector_count"] += 1
        self.assertIn("partition exceeds image", self.errors())

    def test_payload_path_cannot_escape_project(self):
        for path in ["../outside.img", "D:/outside.img", "C:outside.img", "/outside.img", "assets/../../outside.img", "assets\\outside.img"]:
            with self.subTest(path=path), self.assertRaises(ValueError):
                contained_path(ROOT, path)
        self.package["payload"] = {"kind": "bundled_file", "file": "../outside.img", "sha256": "0" * 64, "image_bytes": 1073741824}
        self.assertTrue(self.errors())

    def test_success_cannot_skip_real_boot_check(self):
        self.machine["states"]["Ejecting"].append("Succeeded")
        self.assertTrue(any("requires BootVerifying" in error for error in validate_state_machine(self.machine)))

    def test_write_cannot_skip_elevation_and_confirmation(self):
        self.machine["states"]["TargetVerified"].append("Writing")
        errors = validate_state_machine(self.machine)
        self.assertTrue(any("requires AwaitingElevation" in error for error in errors))
        self.assertTrue(any("requires AwaitingEraseConsent" in error for error in errors))

    def test_missing_write_identity_guard_rejected(self):
        self.machine["required_guards"]["Writing"].remove("target_identity_rechecked")
        self.assertIn("missing contract guards: Writing", validate_state_machine(self.machine))

    def test_lab_profile_schema_refuses_customer_flag(self):
        from jsonschema import Draft202012Validator
        self.profile["customer_installation_allowed"] = True
        validator = Draft202012Validator(read_json(ROOT / "schemas/hardware-profile.schema.json"))
        self.assertTrue(list(validator.iter_errors(self.profile)))

    def obsidian_package(self):
        return read_json(ROOT / "catalog/obsidian-os-1-0-lab-r1.json")

    def test_obsidian_1_0_manifest_is_consistent(self):
        package = self.obsidian_package()
        self.assertEqual(validate_package(package, self.profiles, self.assets, ROOT), [])
        self.assertEqual(package["package_id"], "obsidian-os-1-0-lab-r1")
        self.assertTrue(package["includes_final_obsidian_launcher"])
        self.assertEqual(
            package["payload"]["sha256"],
            "094f8e698a236d3e1db64ad4f69a2e3e2bebbb3846728595972167221c8b3fa8")

    def test_obsidian_1_0_cannot_be_customer_release(self):
        package = self.obsidian_package()
        package["customer_installation_allowed"] = True
        self.assertTrue(any("customer release" in error
                            for error in validate_package(package, self.profiles, self.assets, ROOT)))

    def test_obsidian_1_0_rejects_tampered_image_size(self):
        package = self.obsidian_package()
        package["payload"]["image_bytes"] -= 512
        self.assertTrue(validate_package(package, self.profiles, self.assets, ROOT))

    def test_obsidian_1_0_declares_installer_created_data_partition(self):
        package = self.obsidian_package()
        data = package["storage_policy"]["data_partition"]
        self.assertEqual(data["label"], "OBSIDIAN")
        self.assertEqual(data["start_sector"], 2097152)
        self.assertEqual(package["boot_verification"]["identity_file"], "/etc/obsidian-release")



if __name__ == "__main__":
    unittest.main()
