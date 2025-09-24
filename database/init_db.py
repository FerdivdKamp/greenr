import os
import duckdb
"""
 Connect to or create the database file

Duckumentation:
https://duckdb.org/docs/stable/sql/statements/create_table

"""

# Clean up
if os.path.exists("carbon_tracker.duckdb"):
    os.remove("carbon_tracker.duckdb")

con = duckdb.connect("carbon_tracker.duckdb")

# Create tables
con.execute("""
CREATE TABLE users (
    user_id UUID DEFAULT uuidv4() PRIMARY KEY,
    email TEXT UNIQUE NOT NULL,
    first_name TEXT CHECK (LENGTH(first_name) <= 20),
    password_hash TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
""")

con.execute("""
CREATE TABLE houses (
    house_id UUID DEFAULT uuidv4(),
    user_id UUID REFERENCES users(user_id),
    energy_label TEXT,
    size_m2 INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
""")

con.execute("""
CREATE TABLE items (
    item_id UUID DEFAULT uuidv4(),
    user_id UUID REFERENCES users(user_id),
    item_name TEXT NOT NULL CHECK (LENGTH(item_name) <= 50),
    date_of_purchase DATE,
    use_case TEXT CHECK (LENGTH(use_case) <= 20),
    price NUMERIC(10, 2),
    footprint_kg NUMERIC(10, 3),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
""")

con.execute("""
CREATE TABLE commutes (
    commute_id UUID DEFAULT uuidv4(),
    user_id UUID REFERENCES users(user_id),
    mode TEXT,
    distance_km_per_trip NUMERIC(10, 3),
    times_per_week INTEGER,
    work_from_home_days_per_week INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
""")

con.execute("""
CREATE TABLE questionnaire (
  id            UUID PRIMARY KEY,
  title         TEXT NOT NULL,
  definition_json TEXT NOT NULL,
  version       INTEGER NOT NULL DEFAULT 1,
  created_at     TIMESTAMP NOT NULL  -- store UTC
);
""")

con.execute("""
CREATE TABLE response (
  id               UUID PRIMARY KEY,
  questionnaire_id  UUID NOT NULL REFERENCES questionnaire(id),
  user_id           TEXT,           -- or UUID if you have one
  submitted_at      TIMESTAMP NOT NULL,   -- UTC
);
""")

con.execute("""
CREATE TABLE response_item (
  id               UUID PRIMARY KEY,
  response_id       UUID NOT NULL REFERENCES response(id),
  question_id       TEXT NOT NULL,         -- matches JSON question "id"
  answer_text       TEXT,
  answer_numeric    DECIMAL(18,4), 
  answer_choice_id  TEXT
);
""")

con.execute("""
CREATE INDEX idx_response_questionnaire ON response(questionnaire_id);
CREATE INDEX idx_items_question ON response_item(question_id);
""")

print("DuckDB schema initialized.")
