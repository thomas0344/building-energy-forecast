"""Fit non-negative equipment coefficients by coordinate descent."""
import csv
import json
import math
from statistics import mean
from generate import GROUPS, ROOT


def read(name):
    with (ROOT / f"data/{name}.csv").open() as f:
        return list(csv.DictReader(f))


def save(name, value):
    (ROOT / name).write_text(json.dumps(value, indent=2) + "\n")


def features(row):
    return [float(row[g]) for g in GROUPS]


def predict(x, weights):
    return sum(a * b for a, b in zip(x, weights))


def fit(xs, ys):
    weights = [1.0] * len(GROUPS)
    residual = [y - predict(x, weights) for x, y in zip(xs, ys)]
    for _ in range(500):
        largest = 0
        for j, column in enumerate(zip(*xs)):
            norm = sum(v*v for v in column)
            if norm == 0:
                continue
            delta = max(-weights[j], sum(v*r for v, r in zip(column, residual)) / norm)
            weights[j] += delta
            residual = [r - delta*v for r, v in zip(residual, column)]
            largest = max(largest, abs(delta))
        if largest < 1e-8:
            break
    return weights


def main():
    rows = read("meter")
    split = len(rows) - 60 * 24
    xs = [features(r) for r in rows]
    ys = [float(r["energy_kwh"]) for r in rows]
    weights = fit(xs[:split], ys[:split])
    save("models/model.json", dict(groups=GROUPS, weights=weights))
    actual = ys[split:]
    pred = [predict(x, weights) for x in xs[split:]]
    errors = [a-p for a, p in zip(actual, pred)]
    center = mean(actual)
    metrics = dict(mae=mean(map(abs, errors)), rmse=math.sqrt(mean(e*e for e in errors)),
                   mape=100*mean(abs(e/a) for e, a in zip(errors, actual) if a),
                   r2=1-sum(e*e for e in errors)/sum((a-center)**2 for a in actual),
                   baselineMae=mean(abs(a-b) for a,b in zip(actual, ys[split-168:])),
                   trainHours=split, testHours=len(actual))
    save("results/metrics.json", metrics)
    save("results/history.json", [dict(timestamp=r["timestamp"], actual=a, predicted=p)
                                 for r,a,p in zip(rows[split:], actual, pred)])
    with (ROOT / "results/forecast.csv").open("w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["timestamp", "predicted_kwh"])
        for row in read("future"):
            writer.writerow([row["timestamp"], predict(features(row), weights)])
    print(json.dumps(metrics, indent=2))


if __name__ == "__main__":
    main()
