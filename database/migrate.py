"""Apply versioned DuckDB schema migrations.

Usage: python database/migrate.py [path-to-database]
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
from pathlib import Path
import re

try:
    import duckdb
except ModuleNotFoundError as error:
    raise SystemExit("DuckDB is required. Install it with: python -m pip install -r database/requirements.txt") from error


MIGRATIONS_DIRECTORY = Path(__file__).with_name("migrations")
MIGRATION_NAME = re.compile(r"^(?P<version>\d+)_.+\.sql$")


def migration_files() -> list[tuple[int, Path]]:
    return sorted(
        (int(match.group("version")), path)
        for path in MIGRATIONS_DIRECTORY.glob("*.sql")
        if (match := MIGRATION_NAME.match(path.name))
    )


def has_column(connection, table: str, column: str) -> bool:
    return connection.execute(
        "SELECT 1 FROM information_schema.columns WHERE table_schema = 'main' AND table_name = ? AND column_name = ?",
        [table, column],
    ).fetchone() is not None


def record_migration(connection, version: int, path: Path) -> None:
    connection.execute(
        "INSERT INTO schema_migrations (version, name, applied_at) VALUES (?, ?, ?)",
        [version, path.name, datetime.now(timezone.utc)],
    )


def upgrade_legacy_user_name(connection, migrations: list[tuple[int, Path]]) -> bool:
    """Upgrade before 001 creates foreign-key tables that reference users."""
    legacy_migration = next(((version, path) for version, path in migrations if version == 2), None)
    if legacy_migration is None or not (
        has_column(connection, "users", "username")
        and not has_column(connection, "users", "user_name")
    ):
        return False

    version, path = legacy_migration
    if connection.execute("SELECT 1 FROM schema_migrations WHERE version = ?", [version]).fetchone():
        return False
    connection.execute("BEGIN TRANSACTION")
    try:
        connection.execute(path.read_text(encoding="utf-8"))
        record_migration(connection, version, path)
        connection.execute("COMMIT")
        return True
    except Exception:
        connection.execute("ROLLBACK")
        raise


def apply_migrations(database: Path) -> list[int]:
    database.parent.mkdir(parents=True, exist_ok=True)
    connection = duckdb.connect(str(database))
    try:
        connection.execute("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                applied_at TIMESTAMP NOT NULL
            )
        """)
        migrations = migration_files()
        legacy_upgraded = upgrade_legacy_user_name(connection, migrations)
        applied: list[int] = [2] if legacy_upgraded else []
        for version, path in migrations:
            if connection.execute("SELECT 1 FROM schema_migrations WHERE version = ?", [version]).fetchone():
                continue
            connection.execute("BEGIN TRANSACTION")
            try:
                # A fresh database receives user_name in 001, so 002 is a
                # recorded no-op. Legacy databases were handled before 001.
                if version != 2:
                    connection.execute(path.read_text(encoding="utf-8"))
                record_migration(connection, version, path)
                connection.execute("COMMIT")
            except Exception:
                connection.execute("ROLLBACK")
                raise
            applied.append(version)
        return applied
    finally:
        connection.close()


def main() -> None:
    parser = argparse.ArgumentParser(description="Apply Carbon Tracker DuckDB migrations.")
    parser.add_argument("database", nargs="?", type=Path,
                        default=Path(__file__).with_name("carbon_tracker.duckdb"),
                        help="DuckDB file (default: database/carbon_tracker.duckdb).")
    args = parser.parse_args()
    applied = apply_migrations(args.database)
    print("Applied migrations: " + ", ".join(map(str, applied)) if applied else "Database is already up to date.")


if __name__ == "__main__":
    main()
