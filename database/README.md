

### DuckDB

The versioned migrations in `database/migrations` are the single source of truth
for the schema. The API does not create or alter tables at runtime.



```
winget install DuckDB.cli
```
Installing DuckDb for CLI usage, (not required) also see [DuckDb documentation](https://duckdb.org/docs/installation/?version=stable&environment=cli&platform=win&download_method=package_manager)


Install the Python dependency once:

```
python -m pip install -r database/requirements.txt
```

Create a blank database (from the repository root):

```
python database/migrate.py database/carbon_tracker.duckdb
```

Run the same command to upgrade an existing local database. It is safe to run
more than once; successfully applied versions are recorded in `schema_migrations`.
Databases created by the old API with `users.username` are upgraded to the
canonical `users.user_name` column.

`python database/init_db.py` remains a compatibility alias and no longer deletes
an existing database. To create test data after migrating, run:

```
python database/init_test_data.py
```

The seed script writes to `database/carbon_tracker.duckdb`, the same database
used by the migration command and the API. Run the migration command first.


During testing updating the database can be a bit of work, it might be simpler to just delete the [NAME].duckdb and run the python scripts again.

To query the development database from the repository root, run:

`duckdb database/carbon_tracker.duckdb`

`select * from items;`

Describe TABLE
`DESCRIBE response;`  

Describe all tables in schema
`SELECT table_name, column_name, data_type FROM information_schema.columns WHERE table_schema = 'main';`

The quit the shell CTRL+C or `.quit`



```SQL
select id, canonical_id, version, status, supersedes_id, replaced_by_id, created_at
from questionnaire;
```
