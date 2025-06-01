import duckdb
import bcrypt
import uuid
from datetime import datetime

# Connect to the existing DuckDB file
con = duckdb.connect("carbon_tracker.duckdb")

# Hash the password using bcrypt
password = "1234!"
hashed_pw = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt()).decode('utf-8')

# Generate UUID for user_id
user_id = str(uuid.uuid4())

# Insert sample user
con.execute("""
INSERT INTO users (user_id, email, first_name, password_hash, created_at, updated_at)
VALUES (?, ?, ?, ?, ?, ?)
""", (user_id, "ferdivdkamp@gmail.com", "ferdi", hashed_pw, datetime.now(), datetime.now()))

print("User inserted with user_id:", user_id)
