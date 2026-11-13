-- One row per player: today's SaveData JSON as a single JSONB document plus a revision
-- (optimistic concurrency; a stale write is rejected).
CREATE TABLE IF NOT EXISTS profiles (
    player_id uuid PRIMARY KEY REFERENCES players(id),
    document jsonb NOT NULL,
    revision integer NOT NULL DEFAULT 1,
    updated_utc timestamptz NOT NULL DEFAULT now()
);

-- Written by task 016 (fight resolution); created here only.
CREATE TABLE IF NOT EXISTS fight_records (
    player_id uuid NOT NULL REFERENCES players(id),
    fight_id uuid NOT NULL,
    encounter_id text NOT NULL,
    seed bigint NOT NULL,
    outcome text NOT NULL,
    revive_fired boolean NOT NULL DEFAULT false,
    result jsonb NOT NULL,
    acknowledged boolean NOT NULL DEFAULT false,
    created_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (player_id, fight_id)
)
