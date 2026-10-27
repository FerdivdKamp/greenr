import sys
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch

import duckdb

sys.path.insert(0, str(Path(__file__).parents[1]))
from migrate import apply_migrations, migration_files


class MigrationTests(unittest.TestCase):
    def setUp(self):
        self.database = Path(__file__).with_name(f"_{self._testMethodName}.duckdb")
        self.database.unlink(missing_ok=True)

    def tearDown(self):
        self.database.unlink(missing_ok=True)

    def initialise_v2(self):
        with patch("migrate.migration_files", return_value=migration_files()[:2]):
            self.assertEqual([1, 2], apply_migrations(self.database))

    def test_blank_database_has_api_schema_and_is_idempotent(self):
        self.assertEqual([1, 2, 3], apply_migrations(self.database))
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
            self.assertEqual(3, connection.execute("SELECT COUNT(*) FROM schema_migrations").fetchone()[0])
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

        self.assertEqual([2, 1, 3], apply_migrations(self.database))
        connection = duckdb.connect(str(self.database), read_only=True)
        try:
            columns = {row[0] for row in connection.execute(
                "SELECT column_name FROM information_schema.columns WHERE table_name = ?", ["users"]
            ).fetchall()}
            self.assertIn("user_name", columns)
            self.assertNotIn("username", columns)
        finally:
            connection.close()

    def test_db03_upgrades_existing_owned_data_without_losing_links(self):
        self.initialise_v2()
        user_id, item_id, family_id, first_id, second_id, response_id = (uuid.uuid4() for _ in range(6))
        connection = duckdb.connect(str(self.database))
        try:
            connection.execute(
                "INSERT INTO users (user_id, user_name, email, password_hash) VALUES (?, 'owner', 'owner@example.invalid', 'hash')",
                [user_id],
            )
            connection.execute(
                "INSERT INTO items (item_id, user_id, item_name, date_of_purchase) VALUES (?, ?, 'Bike', '2025-01-02')",
                [item_id, user_id],
            )
            connection.execute("""
                INSERT INTO questionnaire (id, canonical_id, version, title, definition_json, status, created_at, updated_at)
                VALUES (?, ?, 1, 'First', '{}', 'inactive', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """, [first_id, family_id])
            connection.execute("""
                INSERT INTO questionnaire (id, canonical_id, version, title, definition_json, status, supersedes_id, created_at, updated_at)
                VALUES (?, ?, 2, 'Second', '{}', 'active', ?, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """, [second_id, family_id, first_id])
            connection.execute("UPDATE questionnaire SET replaced_by_id = ? WHERE id = ?", [second_id, first_id])
            connection.execute("""
                INSERT INTO response (id, questionnaire_id, canonical_id, user_id, submitted_at, answers_json)
                VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP, '{}')
            """, [response_id, second_id, family_id, user_id])
        finally:
            connection.close()

        self.assertEqual([3], apply_migrations(self.database))
        self.assertEqual([], apply_migrations(self.database))
        connection = duckdb.connect(str(self.database))
        try:
            self.assertEqual((user_id, "Bike"), connection.execute(
                "SELECT user_id, item_name FROM items WHERE item_id = ?", [item_id]
            ).fetchone())
            self.assertEqual(first_id, connection.execute(
                "SELECT supersedes_id FROM questionnaire WHERE id = ?", [second_id]
            ).fetchone()[0])
            self.assertEqual(second_id, connection.execute(
                "SELECT replaced_by_id FROM questionnaire WHERE id = ?", [first_id]
            ).fetchone()[0])
            self.assertEqual(user_id, connection.execute(
                "SELECT user_id FROM response WHERE id = ?", [response_id]
            ).fetchone()[0])
            indexes = {row[0] for row in connection.execute(
                "SELECT index_name FROM duckdb_indexes()"
            ).fetchall()}
            self.assertIn("idx_items_user", indexes)
            self.assertIn("idx_response_user", indexes)
        finally:
            connection.close()

    def test_db03_refuses_orphan_items_without_changing_data(self):
        self.initialise_v2()
        item_id = uuid.uuid4()
        connection = duckdb.connect(str(self.database))
        connection.execute(
            "INSERT INTO items (item_id, item_name) VALUES (?, 'Needs an owner')", [item_id]
        )
        connection.close()

        with self.assertRaisesRegex(ValueError, "items without a user"):
            apply_migrations(self.database)
        connection = duckdb.connect(str(self.database), read_only=True)
        try:
            self.assertEqual(1, connection.execute(
                "SELECT COUNT(*) FROM items WHERE item_id = ? AND user_id IS NULL", [item_id]
            ).fetchone()[0])
            self.assertEqual(0, connection.execute(
                "SELECT COUNT(*) FROM schema_migrations WHERE version = 3"
            ).fetchone()[0])
        finally:
            connection.close()

    def test_db03_refuses_cross_family_version_link_then_allows_repair(self):
        self.initialise_v2()
        first_id, other_id = uuid.uuid4(), uuid.uuid4()
        connection = duckdb.connect(str(self.database))
        try:
            for questionnaire_id in (first_id, other_id):
                connection.execute("""
                    INSERT INTO questionnaire (id, canonical_id, version, title, definition_json,
                                               status, created_at, updated_at)
                    VALUES (?, ?, 1, 'Version one', '{}', 'draft', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                """, [questionnaire_id, questionnaire_id])
            connection.execute(
                "UPDATE questionnaire SET replaced_by_id = ? WHERE id = ?", [other_id, first_id]
            )
        finally:
            connection.close()

        with self.assertRaisesRegex(ValueError, "invalid replaced-by link"):
            apply_migrations(self.database)
        connection = duckdb.connect(str(self.database))
        try:
            self.assertEqual(other_id, connection.execute(
                "SELECT replaced_by_id FROM questionnaire WHERE id = ?", [first_id]
            ).fetchone()[0])
            connection.execute(
                "UPDATE questionnaire SET replaced_by_id = NULL WHERE id = ?", [first_id]
            )
        finally:
            connection.close()
        self.assertEqual([3], apply_migrations(self.database))

    def test_db03_constraints_reject_invalid_new_rows(self):
        apply_migrations(self.database)
        connection = duckdb.connect(str(self.database))
        try:
            with self.assertRaises(duckdb.ConstraintException):
                connection.execute("INSERT INTO items (item_name) VALUES ('No owner')")
            family_id, questionnaire_id = uuid.uuid4(), uuid.uuid4()
            connection.execute("""
                INSERT INTO questionnaire (id, canonical_id, version, title, definition_json, status, created_at, updated_at)
                VALUES (?, ?, 1, 'First', '{}', 'active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """, [questionnaire_id, family_id])
            with self.assertRaises(duckdb.ConstraintException):
                connection.execute("""
                    INSERT INTO questionnaire (id, canonical_id, version, title, definition_json, status, created_at, updated_at)
                    VALUES (?, ?, 0, 'Invalid', '{}', 'active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                """, [uuid.uuid4(), family_id])
            with self.assertRaises(duckdb.ConstraintException):
                connection.execute("""
                    INSERT INTO response (id, questionnaire_id, canonical_id, user_id, submitted_at, answers_json)
                    VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP, '{}')
                """, [uuid.uuid4(), questionnaire_id, family_id, uuid.uuid4()])
            second_id = uuid.uuid4()
            connection.execute("""
                INSERT INTO questionnaire (id, canonical_id, version, title, definition_json, status,
                                           supersedes_id, created_at, updated_at)
                VALUES (?, ?, 2, 'Second', '{}', 'draft', ?, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """, [second_id, family_id, questionnaire_id])
            connection.execute("""
                INSERT INTO response (id, questionnaire_id, canonical_id, submitted_at, answers_json)
                VALUES (?, ?, ?, CURRENT_TIMESTAMP, '{}')
            """, [uuid.uuid4(), questionnaire_id, family_id])
            connection.execute(
                "UPDATE questionnaire SET replaced_by_id = ?, status = 'inactive' WHERE id = ?",
                [second_id, questionnaire_id],
            )
            connection.execute(
                "UPDATE questionnaire SET status = 'active' WHERE id = ?", [second_id]
            )
            with self.assertRaises(duckdb.ConstraintException):
                connection.execute("""
                    INSERT INTO questionnaire (id, canonical_id, version, title, definition_json, status,
                                               created_at, updated_at)
                    VALUES (?, ?, 3, 'Third', '{}', 'unknown', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                """, [uuid.uuid4(), family_id])
        finally:
            connection.close()

    def test_init_audits_active_versions_and_response_family(self):
        apply_migrations(self.database)
        family_id, first_id, second_id = (uuid.uuid4() for _ in range(3))
        connection = duckdb.connect(str(self.database))
        try:
            for version, questionnaire_id in ((1, first_id), (2, second_id)):
                connection.execute("""
                    INSERT INTO questionnaire (id, canonical_id, version, title, definition_json,
                                               status, created_at, updated_at)
                    VALUES (?, ?, ?, 'Version', '{}', 'active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                """, [questionnaire_id, family_id, version])
        finally:
            connection.close()
        with self.assertRaisesRegex(ValueError, "multiple active versions"):
            apply_migrations(self.database)

        connection = duckdb.connect(str(self.database))
        try:
            connection.execute(
                "UPDATE questionnaire SET status = 'draft' WHERE id = ?", [second_id]
            )
            connection.execute("""
                INSERT INTO response (id, questionnaire_id, canonical_id, submitted_at, answers_json)
                VALUES (?, ?, ?, CURRENT_TIMESTAMP, '{}')
            """, [uuid.uuid4(), first_id, uuid.uuid4()])
        finally:
            connection.close()
        with self.assertRaisesRegex(ValueError, "mismatched questionnaire family"):
            apply_migrations(self.database)


if __name__ == "__main__":
    unittest.main()
