-- Rebuild the related tables because DuckDB cannot add foreign keys or CHECK
-- constraints to existing tables. The migration runner wraps this in a transaction.
CREATE TEMP TABLE db03_items AS SELECT * FROM items;
CREATE TEMP TABLE db03_questionnaire AS SELECT * FROM questionnaire;
CREATE TEMP TABLE db03_response AS SELECT * FROM response;
CREATE TEMP TABLE db03_response_item AS SELECT * FROM response_item;

DROP TABLE response_item;
DROP TABLE response;
DROP TABLE questionnaire;
DROP TABLE items;

CREATE TABLE items (
    item_id UUID PRIMARY KEY DEFAULT uuidv4(),
    user_id UUID NOT NULL REFERENCES users(user_id),
    item_name TEXT NOT NULL CHECK (LENGTH(item_name) <= 50),
    date_of_purchase DATE,
    use_case TEXT CHECK (LENGTH(use_case) <= 20),
    price NUMERIC(10, 2),
    footprint_kg NUMERIC(10, 3),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
INSERT INTO items SELECT * FROM db03_items;
-- DuckDB only uses single-column ART indexes for index scans. Date filters
-- also benefit from the automatic zonemap on date_of_purchase.
CREATE INDEX idx_items_user ON items(user_id);

CREATE TABLE questionnaire (
    id UUID PRIMARY KEY,
    canonical_id UUID NOT NULL,
    version INTEGER NOT NULL CHECK (version > 0),
    title TEXT NOT NULL,
    definition_json TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('draft', 'active', 'inactive')),
    supersedes_id UUID,
    replaced_by_id UUID,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    UNIQUE (canonical_id, version)
);
-- Self-referencing foreign keys block normal publish updates in DuckDB 1.x.
-- Existing links are checked by migrate.py before the table is rebuilt.
INSERT INTO questionnaire SELECT * FROM db03_questionnaire;
CREATE TABLE response (
    id UUID PRIMARY KEY,
    questionnaire_id UUID NOT NULL REFERENCES questionnaire(id),
    canonical_id UUID NOT NULL,
    -- Anonymous responses remain valid; a supplied owner must be a real user.
    user_id UUID REFERENCES users(user_id),
    submitted_at TIMESTAMP NOT NULL,
    definition_hash TEXT,
    answers_json TEXT NOT NULL
);
INSERT INTO response SELECT * FROM db03_response;
CREATE INDEX idx_response_questionnaire ON response(questionnaire_id);
CREATE INDEX idx_response_canonical ON response(canonical_id);
CREATE INDEX idx_response_user ON response(user_id);

CREATE TABLE response_item (
    id UUID PRIMARY KEY,
    response_id UUID NOT NULL REFERENCES response(id),
    question_id TEXT NOT NULL,
    answer_text TEXT,
    answer_numeric DECIMAL(18, 4),
    answer_choice_id TEXT
);
INSERT INTO response_item SELECT * FROM db03_response_item;
CREATE INDEX idx_response_item_question ON response_item(question_id);

DROP TABLE db03_response_item;
DROP TABLE db03_response;
DROP TABLE db03_questionnaire;
DROP TABLE db03_items;
