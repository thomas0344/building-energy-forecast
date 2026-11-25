"""Run from a terminal with the standalone model service running."""
import json
from urllib.request import Request, urlopen
from urllib.error import URLError


def predict(machine_load):
    inputs = dict(security=0.382, office=4, machines=machine_load, cooling=6)
    request = Request("http://127.0.0.1:5091/predict", json.dumps(inputs).encode(),
                      headers={"Content-Type": "application/json"})
    with urlopen(request, timeout=3) as response:
        return json.load(response)["predicted"]


if __name__ == "__main__":
    try:
        running = predict(7.2)
        stopped = predict(0)
    except URLError as error:
        raise SystemExit(f"Start python3 model_service/server.py first. {error}")
    print(f"Machines running: {running:.2f} kWh")
    print(f"Machines off:     {stopped:.2f} kWh")
    print(f"Difference:       {running - stopped:.2f} kWh")
