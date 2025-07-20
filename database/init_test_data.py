import duckdb
import bcrypt
import uuid
from datetime import datetime, date

# Connect to the existing DuckDB file
con = duckdb.connect("carbon_tracker.duckdb")

# Hash the password using bcrypt
password = "1234!"
hashed_pw = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')

# Generate UUID for user_id
user_id = uuid.UUID("123e4567-e89b-12d3-a456-426614174000")


# Insert sample user
con.execute("""
INSERT INTO users (user_id, email, first_name, password_hash, created_at, updated_at)
VALUES (?, ?, ?, ?, ?, ?)
ON CONFLICT(email) DO UPDATE SET
    first_name = EXCLUDED.first_name,
    password_hash = EXCLUDED.password_hash,
    updated_at = EXCLUDED.updated_at
""", (
    user_id,
    "ferdivdkamp@gmail.com",
    "ferdi",
    hashed_pw,
    datetime.now(),
    datetime.now()
))


# Add Giant Propel Advanced
con.execute("""
INSERT INTO items (item_id, user_id, item_name, date_of_purchase, use_case, price, footprint_kg, created_at, updated_at)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
""", (
    uuid.uuid4(),
    user_id,
    "Giant Propel Advanced",
    date(2016, 5, 1),
    "triathlon",
    1500.0,
    340.0,
    datetime.now(),
    datetime.now()
))

# Add Samsung Galaxy S10e
con.execute("""
INSERT INTO items (item_id, user_id, item_name, date_of_purchase, use_case, price, footprint_kg, created_at, updated_at)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
""", (
    uuid.uuid4(),
    user_id,
    "Samsung Galaxy S10e",
    date(2021, 10, 28),  # No purchase date provided
    "smartphone",
    330.0,
    85.0,
    datetime.now(),
    datetime.now()
))


print("User inserted with user_id:", user_id)
