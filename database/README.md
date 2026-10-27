# Development database

The versioned migrations in `database/migrations` define the DuckDB schema.
The API does not create or alter tables at runtime. Run these commands from the
repository root:

```powershell
python -m pip install -r database/requirements.txt
python database/dev_db.py init
$env:GREENR_DEV_SEED_PASSWORD = Read-Host 'Choose a local development password'
python database/dev_db.py seed
```

`init` creates or upgrades `database/carbon_tracker.duckdb`. It can be run again
after pulling new migrations and preserves existing data. `seed` requires an
initialized database and adds a development account, four sample items, and one
active questionnaire. It uses fixed IDs, so rerunning it does not duplicate
records or overwrite edits, including a password changed through the API.

Migration 003 requires every item to have a real `users.user_id`. If an older
database has ownerless items, `init` stops and rolls back without deleting data.
Inspect them with `SELECT item_id, item_name FROM items WHERE user_id IS NULL`,
decide the correct owner for each row, and update those rows explicitly before
rerunning `init`. Do not assign all ownerless rows to a guessed user. The migration
also checks existing questionnaire version links, one active version per family,
and response ownership/family references before rebuilding the tables.

Items now require an owner at the database level; their user index supports
selective per-user queries, while DuckDB's date zonemap helps date filtering.
Responses may be anonymous (`user_id IS NULL`), but a supplied user ID must
exist. Questionnaire status is limited to `draft`, `active`, or `inactive`,
versions must be positive and unique within a family. DuckDB 1.x cannot
enforce the cross-row `supersedes_id`/`replaced_by_id` links or a unique active
version without blocking normal publish updates once responses exist. `init`
checks the links and active-version count every time it runs; application
writes must keep links within the same family and in version order, with at
most one active version. Authenticated item creation and claim-derived response
ownership are separate API work in BE-03 and BE-05.

The account is `dev_demo` / `dev@example.invalid`, with the local password you
supplied through `GREENR_DEV_SEED_PASSWORD`. These fixtures are only for local
development and UI/API testing. The address is reserved for examples; use your
own password and never deploy this account or seed a production database.
Unset the password variable when finished: `Remove-Item Env:GREENR_DEV_SEED_PASSWORD`.

Both commands accept `--database PATH` after the command to target another
local DuckDB file. The API's default connection string points to
`database/carbon_tracker.duckdb`.

## Destructive reset for development

The following command **permanently deletes** the specified database file,
including all local user data, and recreates an empty migrated database:

```powershell
python database/dev_db.py reset-dev --confirm-delete
```

Run `seed` separately afterward if sample data is wanted. Without
`--confirm-delete`, `reset-dev` refuses to run. Normal `init` and `seed` never
delete the database. Close the API and other DuckDB connections before reset.

`python database/migrate.py [PATH]` and `python database/init_db.py` remain
compatible migration entry points. `python database/init_test_data.py` is a
compatibility alias for `seed` and also requires the local password variable.

Run database tests from the repository root:

```powershell
python -m unittest discover -s database/tests -v
```

To inspect the local database with the optional DuckDB CLI:

```text
duckdb database/carbon_tracker.duckdb
SELECT * FROM items;
.quit
```
