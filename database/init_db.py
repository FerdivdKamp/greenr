import duckdb

# Connect to or create the database file
con = duckdb.connect("carbon_tracker.duckdb")

# Create tables
con.execute("""
CREATE TABLE users (
    user_id UUID DEFAULT uuidv4() PRIMARY KEY,
    email TEXT UNIQUE NOT NULL,
    first_name TEXT,
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
    item_name TEXT NOT NULL,
    date_of_purchase DATE,
    use_case TEXT,
    price NUMERIC,
    footprint_kg NUMERIC,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
""")

con.execute("""
CREATE TABLE commutes (
    commute_id UUID DEFAULT uuidv4(),
    user_id UUID REFERENCES users(user_id),
    mode TEXT,
    distance_km_per_trip NUMERIC,
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
