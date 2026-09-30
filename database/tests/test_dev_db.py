import subprocess
import sys
import types
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch

import duckdb

sys.path.insert(0, str(Path(__file__).parents[1]))
from dev_db import ITEMS, USER_ID, seed
from migrate import apply_migrations


SCRIPT = Path(__file__).parents[1] / "dev_db.py"
ROOT = Path(__file__).parents[2]


class DevelopmentDatabaseTests(unittest.TestCase):
    def setUp(self):
        self.database = Path(__file__).with_name(f"_dev_{uuid.uuid4().hex}.duckdb")
        self.addCleanup(lambda: self.database.unlink(missing_ok=True))

    def run_command(self, *args):
        return subprocess.run(
            [sys.executable, str(SCRIPT), *args, "--database", str(self.database)],
            cwd=ROOT, text=True, capture_output=True,
        )

    def seed_with_stub_hash(self, password="local-password"):
        fake_bcrypt = types.SimpleNamespace(
            gensalt=lambda: b"salt",
            hashpw=lambda value, salt: b"test-hash:" + value,
        )
        with patch.dict(sys.modules, {"bcrypt": fake_bcrypt}):
            seed(self.database, password)

    def test_init_and_seed_are_repeatable_and_preserve_edits(self):
        first_init = self.run_command("init")
        self.assertEqual(0, first_init.returncode, first_init.stderr)
        second_init = self.run_command("init")
        self.assertEqual(0, second_init.returncode, second_init.stderr)
        self.seed_with_stub_hash()

        connection = duckdb.connect(str(self.database))
        try:
            original_hash = connection.execute(
                "SELECT password_hash FROM users WHERE user_id = ?", [USER_ID]
            ).fetchone()[0]
            item_id = connection.execute("SELECT item_id FROM items LIMIT 1").fetchone()[0]
            connection.execute("UPDATE items SET item_name = 'Edited locally' WHERE item_id = ?", [item_id])
            extra_id = uuid.uuid4()
            connection.execute(
                "INSERT INTO items (item_id, user_id, item_name) VALUES (?, ?, ?)",
                [extra_id, USER_ID, "My own item"],
            )
        finally:
            connection.close()

        self.seed_with_stub_hash("changed-password")
        connection = duckdb.connect(str(self.database), read_only=True)
        try:
            self.assertEqual((1, original_hash), connection.execute(
                "SELECT COUNT(*), MIN(password_hash) FROM users WHERE user_id = ?", [USER_ID]
            ).fetchone())
            self.assertEqual(len(ITEMS) + 1, connection.execute(
                "SELECT COUNT(*) FROM items"
            ).fetchone()[0])
            self.assertEqual("Edited locally", connection.execute(
                "SELECT item_name FROM items WHERE item_id = ?", [item_id]
            ).fetchone()[0])
            self.assertEqual(1, connection.execute(
                "SELECT COUNT(*) FROM questionnaire"
            ).fetchone()[0])
            self.assertEqual(2, connection.execute(
                "SELECT COUNT(*) FROM schema_migrations"
            ).fetchone()[0])
        finally:
            connection.close()

    def test_seed_requires_init_and_a_local_password(self):
        with self.assertRaisesRegex(ValueError, "Run init first"):
            self.seed_with_stub_hash()
        apply_migrations(self.database)
        with self.assertRaisesRegex(ValueError, "GREENR_DEV_SEED_PASSWORD"):
            self.seed_with_stub_hash("")

    def test_reset_dev_requires_confirmation_and_clears_database(self):
        initialized = self.run_command("init")
        self.assertEqual(0, initialized.returncode, initialized.stderr)
        self.seed_with_stub_hash()
        refused = self.run_command("reset-dev")
        self.assertNotEqual(0, refused.returncode)
        self.assertIn("permanently deletes", refused.stderr)
        connection = duckdb.connect(str(self.database), read_only=True)
        self.assertEqual(1, connection.execute("SELECT COUNT(*) FROM users").fetchone()[0])
        connection.close()

        reset = self.run_command("reset-dev", "--confirm-delete")
        self.assertEqual(0, reset.returncode, reset.stderr)
        connection = duckdb.connect(str(self.database), read_only=True)
        try:
            self.assertEqual(0, connection.execute("SELECT COUNT(*) FROM users").fetchone()[0])
            self.assertEqual(2, connection.execute(
                "SELECT COUNT(*) FROM schema_migrations"
            ).fetchone()[0])
        finally:
            connection.close()


if __name__ == "__main__":
    unittest.main()
