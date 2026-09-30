-- Run only when a legacy users.username column exists; see migrate.py.
ALTER TABLE users RENAME COLUMN username TO user_name;
