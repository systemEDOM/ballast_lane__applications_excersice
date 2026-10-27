CREATE TABLE IF NOT EXISTS users (
    id                  TEXT    NOT NULL PRIMARY KEY,
    email               TEXT    NOT NULL UNIQUE,
    display_name        TEXT    NOT NULL,
    password_hash       TEXT    NOT NULL,
    password_salt       TEXT    NOT NULL,
    password_iterations INTEGER NOT NULL,
    created_at          TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS tasks (
    id          TEXT    NOT NULL PRIMARY KEY,
    user_id     TEXT    NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    title       TEXT    NOT NULL,
    description TEXT    NULL,
    status      TEXT    NOT NULL,
    due_date    TEXT    NULL,
    created_at  TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_tasks_user_id ON tasks (user_id);
