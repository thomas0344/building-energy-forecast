"""Build the shareable sample database. Live accounts stay in App_Data."""
import csv
import json
from pathlib import Path
import sqlite3
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent


def build():
    with tempfile.TemporaryDirectory(dir=HERE) as temp:
        target = Path(temp) / "sample.sqlite"
        db = sqlite3.connect(target)
        try:
            db.execute("PRAGMA foreign_keys=ON")
            db.executescript((HERE / "schema.sql").read_text())
            with (ROOT / "data/meter.csv").open() as f:
                db.executemany("INSERT INTO meter VALUES(?,?,?,?,?,?,?,?,?)", list(csv.reader(f))[1:])
            def rows(path):
                return json.loads((ROOT / path).read_text())
            db.executemany("INSERT INTO equipment VALUES(:name,:group,:count,:watts,:schedule)", rows("data/equipment.json"))
            db.executemany("INSERT INTO history VALUES(:timestamp,:predicted)", rows("results/history.json"))
            db.execute("INSERT INTO metrics VALUES(1,:mae,:rmse,:mape,:r2,:baselineMae,:trainHours,:testHours)", rows("results/metrics.json"))
            db.commit()
        finally:
            db.close()
        target.replace(HERE / "sample.sqlite")


if __name__ == "__main__":
    build()
