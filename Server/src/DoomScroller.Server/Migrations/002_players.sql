-- Players: one row per anonymous install. platform_id/platform_kind are reserved for a later
-- Game Center / Google Play Games link (nullable; no linking built yet).
CREATE TABLE IF NOT EXISTS players (
    id uuid PRIMARY KEY,
    install_id uuid NOT NULL UNIQUE,
    platform_kind text NULL,
    platform_id text NULL,
    created_utc timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS players_platform_idx ON players (platform_kind, platform_id) WHERE platform_id IS NOT NULL;
