CREATE TABLE IF NOT EXISTS accounts (
    email_key TEXT PRIMARY KEY,
    password_hash TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS meter (
    timestamp TEXT PRIMARY KEY, holiday INTEGER NOT NULL,
    temperature_c REAL NOT NULL, occupancy REAL NOT NULL,
    security REAL NOT NULL, office REAL NOT NULL, machines REAL NOT NULL,
    cooling REAL NOT NULL, energy_kwh REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS equipment (
    name TEXT PRIMARY KEY, category TEXT NOT NULL, quantity INTEGER NOT NULL,
    watts REAL NOT NULL, schedule TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS history (
    timestamp TEXT PRIMARY KEY REFERENCES meter(timestamp), predicted REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS metrics (
    id INTEGER PRIMARY KEY CHECK(id=1), mae REAL, rmse REAL, mape REAL, r2 REAL,
    baselineMae REAL, trainHours INTEGER, testHours INTEGER
);
