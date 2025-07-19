

### Duckdb 


Python scripts for quickly creating the database and some test data



```
winget install DuckDB.cli
```
Installing DuckDb for CLI usage, (not required) also see [DuckDb documentation](https://duckdb.org/docs/installation/?version=stable&environment=cli&platform=win&download_method=package_manager)


To create the database, structure `python database/init_db.py`
To Create some test data, run `python database/init_test_data.py`

To query from terminal

`duckdb carbon_tracker.duckdb`

`select * from items;`


The quit the shell

`.quit`