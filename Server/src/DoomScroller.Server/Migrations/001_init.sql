-- Baseline: tracks applied migrations. Game tables arrive in task 012.
CREATE TABLE IF NOT EXISTS schema_info (
    id integer PRIMARY KEY,
    created_utc timestamptz NOT NULL DEFAULT now()
);
