"""Explicit local DuckDB init, seed, and destructive reset commands."""

from __future__ import annotations

import argparse
from datetime import date, datetime
import os
from pathlib import Path
import uuid

import duckdb

from migrate import apply_migrations


DEFAULT_DATABASE = Path(__file__).with_name("carbon_tracker.duckdb")
DEV_PASSWORD_ENV = "GREENR_DEV_SEED_PASSWORD"
SEED_NAMESPACE = uuid.UUID("606641fa-0949-4cc1-8358-fdfcb90ffecd")
USER_ID = uuid.uuid5(SEED_NAMESPACE, "user")
QUESTIONNAIRE_ID = uuid.UUID("7607e780-806f-48a3-b378-ccd2e1c502c7")
SEED_TIMESTAMP = datetime(2024, 1, 1)
ITEMS = (
    ("Giant Propel Advanced", date(2016, 5, 1), "triathlon", 1500.0, 340.0),
    ("Samsung Galaxy S10e", date(2021, 10, 28), "smartphone", 330.0, 85.0),
    ("Nike VaporFly 2", date(2023, 4, 23), "sport", 175.0, 15.0),
    ("Kobo Glo HD", date(2016, 8, 1), "entertainment", 120.0, 40.0),
)
QUESTIONNAIRE_JSON = ('{"title":"Woon-werk verkeer","pages":[{"name":"page1",'
                      '"title":"Woon-werk verkeer","elements":[{"type":"rating",'
                      '"name":"dagen_naar_werk","rateMin":0,"rateMax":7,"isRequired":true},'
                      '{"type":"checkbox","name":"vervoer","choices":['
                      '{"value":"public_transport","text":"OV"},'
                      '{"value":"car","text":"Auto"},{"value":"bike","text":"Fiets"}],'
                      '"validators":[{"type":"answercount","maxCount":3}]},'
                      '{"type":"text","name":"reis_tijd_totaal","inputType":"time"}]}]}')


def seed(database: Path, password: str) -> None:
    """Insert stable development fixtures without changing existing rows."""
    if not password:
        raise ValueError(f"Set {DEV_PASSWORD_ENV} to a local development password before seeding.")
    if not database.is_file():
        raise ValueError(f"Database does not exist: {database}. Run init first.")

    try:
        import bcrypt
    except ModuleNotFoundError as error:
        raise RuntimeError("bcrypt is required. Install database/requirements.txt.") from error

    connection = duckdb.connect(str(database))
    try:
        if not connection.execute(
            "SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_migrations'"
        ).fetchone() or not connection.execute(
            "SELECT 1 FROM schema_migrations WHERE version = 1"
        ).fetchone():
            raise ValueError("Database is not initialized. Run init first.")

        connection.execute("BEGIN TRANSACTION")
        try:
            # A later seed must not replace a password changed through the API.
            if not connection.execute("SELECT 1 FROM users WHERE user_id = ?", [USER_ID]).fetchone():
                password_hash = bcrypt.hashpw(password.encode(), bcrypt.gensalt()).decode()
                connection.execute("""
                    INSERT INTO users (user_id, user_name, email, first_name, password_hash, created_at, updated_at)
                    VALUES (?, ?, ?, ?, ?, ?, ?) ON CONFLICT DO NOTHING
                """, [USER_ID, "dev_demo", "dev@example.invalid", "Demo", password_hash,
                      SEED_TIMESTAMP, SEED_TIMESTAMP])

            for name, purchased, use_case, price, footprint in ITEMS:
                connection.execute("""
                    INSERT INTO items (item_id, user_id, item_name, date_of_purchase, use_case,
                                       price, footprint_kg, created_at, updated_at)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?) ON CONFLICT DO NOTHING
                """, [uuid.uuid5(SEED_NAMESPACE, f"item:{name}"), USER_ID, name,
                      purchased, use_case, price, footprint, SEED_TIMESTAMP, SEED_TIMESTAMP])

            connection.execute("""
                INSERT INTO questionnaire (id, canonical_id, title, version, status,
                                           definition_json, created_at, updated_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?) ON CONFLICT DO NOTHING
            """, [QUESTIONNAIRE_ID, QUESTIONNAIRE_ID, "Woon-werk verkeer", 1, "active",
                  QUESTIONNAIRE_JSON, SEED_TIMESTAMP, SEED_TIMESTAMP])
            connection.execute("COMMIT")
        except Exception:
            connection.execute("ROLLBACK")
            raise
    finally:
        connection.close()


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(description="Manage a local development DuckDB database.")
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("init", "seed", "reset-dev"):
        command = commands.add_parser(name)
        command.add_argument("--database", type=Path, default=DEFAULT_DATABASE,
                             help="DuckDB file (default: database/carbon_tracker.duckdb)")
        if name == "reset-dev":
            command.add_argument("--confirm-delete", action="store_true",
                                 help="Confirm permanent deletion of the specified database")
    args = parser.parse_args(argv)

    try:
        if args.command == "init":
            applied = apply_migrations(args.database)
            print(f"Initialized {args.database}; applied migrations: {applied}")
        elif args.command == "seed":
            seed(args.database, os.environ.get(DEV_PASSWORD_ENV, ""))
            print(f"Development seed data present in {args.database}")
        else:
            if not args.confirm_delete:
                parser.error("reset-dev permanently deletes the database; pass --confirm-delete to proceed")
            args.database.unlink(missing_ok=True)
            applied = apply_migrations(args.database)
            print(f"Reset {args.database}; applied migrations: {applied}")
    except (ValueError, RuntimeError) as error:
        parser.error(str(error))


if __name__ == "__main__":
    main()
