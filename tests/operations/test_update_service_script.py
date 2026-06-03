from pathlib import Path
import shutil
import subprocess
import unittest


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPO_ROOT / "updateService.sh"


class UpdateServiceScriptTests(unittest.TestCase):
    def test_script_contains_required_safe_update_steps(self) -> None:
        self.assertTrue(SCRIPT_PATH.exists(), "updateService.sh must exist at the repository root.")

        script = SCRIPT_PATH.read_text(encoding="utf-8")

        self.assertTrue(script.startswith("#!/usr/bin/env bash\n"))
        self.assertIn("set -Eeuo pipefail", script)
        self.assertIn("git pull --ff-only", script)
        self.assertIn("pg_dump", script)
        self.assertIn("docker compose", script)
        self.assertIn("--env-file", script)
        self.assertIn("compose.yaml", script)
        self.assertIn("up -d --build --remove-orphans", script)
        self.assertIn("wait_for_service", script)
        self.assertIn("dotnet-api", script)
        self.assertIn("rag-api", script)
        self.assertIn("caddy", script)

        forbidden_snippets = [
            "git reset --hard",
            "docker compose down -v",
            "docker volume rm",
            "rm -rf",
        ]

        for snippet in forbidden_snippets:
            self.assertNotIn(snippet, script)

    def test_script_has_valid_bash_syntax_when_bash_is_available(self) -> None:
        self.assertTrue(SCRIPT_PATH.exists(), "updateService.sh must exist at the repository root.")

        bash = shutil.which("bash")
        if bash is None:
            self.skipTest("bash is not available on this workstation.")

        result = subprocess.run(
            [bash, "-lc", "bash -n ./updateService.sh"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(
            result.returncode,
            0,
            f"bash -n failed:\nSTDOUT:\n{result.stdout}\nSTDERR:\n{result.stderr}",
        )


if __name__ == "__main__":
    unittest.main()
