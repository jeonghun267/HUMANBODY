import importlib.util
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("compile_check", Path(__file__).with_name("compile_check.py"))
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)


class BuildStatusTests(unittest.TestCase):
    def test_restore_failure_cannot_pass_without_cs_error(self):
        result = subprocess.CompletedProcess([], 1, "error NU1301: Cannot load service index", "")
        with patch.object(checker.subprocess, "run", return_value=result), patch("builtins.print"):
            self.assertNotEqual(checker.build("missing.csproj"), 0)

    def test_msbuild_failure_cannot_pass_without_cs_error(self):
        result = subprocess.CompletedProcess([], 1, "error MSB1009: Project does not exist", "")
        with patch.object(checker.subprocess, "run", return_value=result), patch("builtins.print"):
            self.assertNotEqual(checker.build("missing.csproj"), 0)

    def test_successful_build_passes(self):
        with patch.object(checker.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, "Build succeeded", "")):
            self.assertEqual(checker.build("project.csproj"), 0)


if __name__ == "__main__":
    unittest.main()
