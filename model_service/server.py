"""Run with python3 model_service/server.py. Standard library only."""
import csv
import json
import math
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
MODEL = json.loads((ROOT / "models/model.json").read_text())
GROUPS = MODEL["groups"]
WEIGHTS = MODEL["weights"]
if GROUPS != ["security", "office", "machines", "cooling"] or len(WEIGHTS) != len(GROUPS) or not all(
        math.isfinite(w) and w >= 0 for w in WEIGHTS):
    raise ValueError("Invalid trained model")


def predict(values):
    if not isinstance(values, dict) or set(values) != set(GROUPS):
        raise ValueError("Supply all four equipment groups.")
    for value in values.values():
        if type(value) not in (int, float) or not 0 <= value <= 1000000:
            raise ValueError("Each load must be a number from 0 to 1,000,000 kWh.")
    loads = [dict(group=g, kwh=values[g] * w) for g, w in zip(GROUPS, WEIGHTS)]
    return dict(loads=loads, predicted=sum(item["kwh"] for item in loads))


with (ROOT / "data/future.csv").open() as source:
    FORECAST = [dict(timestamp=row["timestamp"], holiday=row["holiday"] == "1",
                     **predict({g: float(row[g]) for g in GROUPS})) for row in csv.DictReader(source)]
if len(FORECAST) != 168:
    raise ValueError("Expected a 168-hour plan")


class Api(BaseHTTPRequestHandler):
    def reply(self, status, data):
        body = json.dumps(data).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        routes = {"/health": {"status": "healthy"}, "/model": MODEL, "/forecast": FORECAST}
        path = urlsplit(self.path).path
        self.reply(200, routes[path]) if path in routes else self.reply(404, {"error": "Unknown route"})

    def do_POST(self):
        if self.path != "/predict":
            return self.reply(404, {"error": "Unknown route"})
        try:
            length = int(self.headers.get("Content-Length", 0))
            if not 0 < length <= 4096:
                raise ValueError("Expected a small JSON object.")
            self.reply(200, predict(json.loads(self.rfile.read(length))))
        except (ValueError, RecursionError) as error:
            self.reply(400, {"error": str(error)})


if __name__ == "__main__":
    print("Building Energy model API at http://127.0.0.1:5091", flush=True)
    ThreadingHTTPServer(("127.0.0.1", 5091), Api).serve_forever()
