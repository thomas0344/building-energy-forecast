-- Each row is one hour. Sum the hours to get one day's kWh.
SELECT date(timestamp) AS day, round(sum(energy_kwh), 2) AS daily_kwh
FROM meter GROUP BY day ORDER BY day LIMIT 7;

-- Compare model predictions with the same meter's actual test readings.
SELECT h.timestamp, m.energy_kwh AS actual, h.predicted
FROM history AS h JOIN meter AS m USING(timestamp)
ORDER BY abs(m.energy_kwh - h.predicted) DESC LIMIT 5;

SELECT name, quantity, watts, schedule FROM equipment ORDER BY name;
