from pathlib import Path
import shutil
import subprocess
import unittest


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPO_ROOT / "installServiceAutostart.sh"


class InstallServiceAutostartScriptTests(unittest.TestCase):
    def test_script_contains_systemd_compose_autostart_steps(self) -> None:
        self.assertTrue(
            SCRIPT_PATH.exists(),
            "installServiceAutostart.sh must exist at the repository root.",
        )

        script = SCRIPT_PATH.read_text(encoding="utf-8")

        self.assertTrue(script.startswith("#!/usr/bin/env bash\n"))
        self.assertIn("set -Eeuo pipefail", script)
        self.assertIn("advanced-rag.service", script)
        self.assertIn("After=docker.service network-online.target", script)
        self.assertIn("Wants=network-online.target", script)
        self.assertIn("Type=oneshot", script)
        self.assertIn("RemainAfterExit=yes", script)
        self.assertIn("docker compose", script)
        self.assertIn("--env-file", script)
        self.assertIn("compose.yaml", script)
        self.assertIn("up -d", script)
        self.assertIn("stop", script)
        self.assertIn("systemctl daemon-reload", script)
        self.assertIn("systemctl enable", script)
        self.assertIn("systemctl start", script)
        self.assertNotIn("openai_api_key", script)
        self.assertNotIn("postgres_admin_password", script)

    def test_script_has_valid_bash_syntax_when_bash_is_available(self) -> None:
        self.assertTrue(
            SCRIPT_PATH.exists(),
            "installServiceAutostart.sh must exist at the repository root.",
        )

        bash = shutil.which("bash")
        if bash is None:
            self.skipTest("bash is not available on this workstation.")

        result = subprocess.run(
            [bash, "-lc", "bash -n ./installServiceAutostart.sh"],
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
