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
CREATE TABLE questions (
    question_id UUID DEFAULT uuidv4() PRIMARY KEY,
    question_text TEXT NOT NULL,
    category TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
""")

con.execute("""
CREATE TABLE answers (
    answer_id UUID DEFAULT uuidv4(),
    user_id UUID REFERENCES users(user_id),
    question_id UUID REFERENCES questions(question_id),
    answer_text TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
""")

print("DuckDB schema initialized.")
