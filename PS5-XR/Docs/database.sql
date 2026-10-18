-- Mirrors the Java onCreate schema. Runtime timestamps: UTC Unix milliseconds.
PRAGMA foreign_keys = ON;
CREATE TABLE sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    equipment TEXT NOT NULL,
    model TEXT NOT NULL,
    step_count INTEGER NOT NULL CHECK(step_count > 0),
    started_at INTEGER NOT NULL,
    finished_at INTEGER
);
CREATE TABLE completed_steps (
    session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    step_number INTEGER NOT NULL CHECK(step_number >= 0),
    completed_at INTEGER NOT NULL,
    PRIMARY KEY(session_id, step_number)
);
