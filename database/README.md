

### Duckdb 


Python scripts for quickly creating the database and some test data



```
winget install DuckDB.cli
```
Installing DuckDb for CLI usage, (not required) also see [DuckDb documentation](https://duckdb.org/docs/installation/?version=stable&environment=cli&platform=win&download_method=package_manager)


To create the database, structure `python database/init_db.py`
To Create some test data, run `python database/init_test_data.py`


During testing updating the database can be a bit of work, it might be simpler to just delete the [NAME].duckdb and run the python scripts again.

To query from terminal run this to open db

`duckdb carbon_tracker.duckdb`

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