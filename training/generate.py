"""Create hourly meter observations and a week of future operating assumptions."""
import csv
import json
import math
import random
from datetime import datetime, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUPS = ["security", "office", "machines", "cooling"]
HOLIDAYS = {"01-01", "01-26", "03-14", "05-01", "08-15", "10-02", "12-25"}
START = datetime(2026, 1, 1)


def operating_plan(t, inventory):
    holiday = t.strftime("%m-%d") in HOLIDAYS
    opened = t.weekday() < 5 and not holiday and 8 <= t.hour < 18
    occupancy = (0.72 if t.weekday() == 4 or t.hour == 13 else 0.92) if opened else 0
    temp = 28 + 3 * math.sin(t.timetuple().tm_yday * 2 * math.pi / 365) + 4 * math.cos((t.hour - 15) * math.pi / 12)
    duty = dict(always=1, night=int(t.hour < 6 or t.hour >= 18), open=int(opened),
                occupied=occupancy, production=int(opened and 10 <= t.hour < 16),
                cooling=int(opened) * min(1, max(0, (temp - 22) / 12)))
    loads = dict.fromkeys(GROUPS, 0.0)
    for item in inventory:
        loads[item["group"]] += item["count"] * item["watts"] / 1000 * duty[item["schedule"]]
    return dict(timestamp=t.isoformat(), holiday=int(holiday), temperature_c=round(temp, 2),
                occupancy=occupancy, **{g: round(loads[g], 5) for g in GROUPS})


def main():
    inventory = json.loads((ROOT / "data/equipment.json").read_text())
    rng = random.Random(42)
    for name, start, hours in [("meter", START, 8760), ("future", datetime(2027, 1, 1), 168)]:
        with (ROOT / f"data/{name}.csv").open("w", newline="") as f:
            writer = None
            for i in range(hours):
                row = operating_plan(start + timedelta(hours=i), inventory)
                if name == "meter":
                    # Simulated meter has imperfect nameplate estimates and measurement noise.
                    typical = sum(row[g] * factor for g, factor in zip(GROUPS, [1.04, .91, .82, .94]))
                    row["energy_kwh"] = round(typical * rng.gauss(1, .055), 4)
                if writer is None:
                    writer = csv.DictWriter(f, fieldnames=row.keys())
                    writer.writeheader()
                writer.writerow(row)
        print(name, hours, "hourly rows")


if __name__ == "__main__":
    main()
