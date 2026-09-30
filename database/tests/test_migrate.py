import sys
import unittest
from pathlib import Path

import duckdb

sys.path.insert(0, str(Path(__file__).parents[1]))
from migrate import apply_migrations


class MigrationTests(unittest.TestCase):
    def setUp(self):
        self.database = Path(__file__).with_name(f"_{self._testMethodName}.duckdb")
        self.database.unlink(missing_ok=True)

    def tearDown(self):
        self.database.unlink(missing_ok=True)

    def test_blank_database_has_api_schema_and_is_idempotent(self):
        self.assertEqual([1, 2], apply_migrations(self.database))
        self.assertEqual([], apply_migrations(self.database))

        connection = duckdb.connect(str(self.database), read_only=True)
        try:
            expected_columns = {
                "users": {"user_id", "user_name", "email", "password_hash"},
                "refresh_tokens": {"token", "user_id", "expires_at", "revoked_at"},
                "password_reset_tokens": {"token", "user_id", "expires_at", "used_at"},
                "items": {"item_id", "item_name", "use_case", "price", "footprint_kg", "date_of_purchase"},
                "questionnaire": {"id", "canonical_id", "version", "title", "definition_json", "status", "supersedes_id", "replaced_by_id", "created_at", "updated_at"},
                "response": {"id", "questionnaire_id", "canonical_id", "user_id", "submitted_at", "definition_hash", "answers_json"},
                "response_item": {"id", "response_id", "question_id", "answer_text", "answer_numeric", "answer_choice_id"},
            }
            for table, required in expected_columns.items():
                columns = {row[0] for row in connection.execute(
                    "SELECT column_name FROM information_schema.columns WHERE table_name = ?", [table]
                ).fetchall()}
                self.assertTrue(required <= columns, table)
            self.assertEqual(2, connection.execute("SELECT COUNT(*) FROM schema_migrations").fetchone()[0])
        finally:
            connection.close()

    def test_legacy_username_column_is_upgraded(self):
        connection = duckdb.connect(str(self.database))
        connection.execute("""
            CREATE TABLE users (
                user_id UUID PRIMARY KEY, email TEXT UNIQUE NOT NULL,
                username TEXT UNIQUE NOT NULL, first_name TEXT,
                password_hash TEXT NOT NULL, created_at TIMESTAMP, updated_at TIMESTAMP
            )
        """)
        connection.close()

        self.assertEqual([2, 1], apply_migrations(self.database))
        connection = duckdb.connect(str(self.database), read_only=True)
        try:
            columns = {row[0] for row in connection.execute(
                "SELECT column_name FROM information_schema.columns WHERE table_name = ?", ["users"]
            ).fetchall()}
            self.assertIn("user_name", columns)
            self.assertNotIn("username", columns)
        finally:
            connection.close()


if __name__ == "__main__":
    unittest.main()
