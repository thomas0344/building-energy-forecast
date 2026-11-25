record PlannedHour(DateTime Time, double Expected, bool Holiday, double Machines);
record Alert(DateTime Time, string Message, double Expected, double? Actual);

sealed class MeterMonitor(ModelClient client) : BackgroundService
{
    private readonly object gate = new();
    private readonly List<Alert> alerts = [];
    private PlannedHour[] plan = [];
    private int index;
    private int highHours;
    private double? previous;
    private double? actual;

    public IResult Snapshot()
    {
        lock (gate)
            return plan.Length == 0 ? Results.StatusCode(503) : Results.Ok(new { current = plan[Math.Max(0, index - 1)], actual, alerts = alerts.AsEnumerable().Reverse().ToArray() });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (plan.Length == 0)
        {
            try {
                var hours = await client.Forecast(stoppingToken);
                lock (gate)
                    plan = hours.Select(h => new PlannedHour(h.Timestamp, h.Predicted, h.Holiday,
                        h.Loads.Single(x => x.Group == "machines").Kwh)).ToArray();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) {
                await Task.Delay(3000, stoppingToken);
            }
        }
        // Replay through Monday lunchtime immediately so the first screen has examples.
        lock (gate)
            while (index < 85) Advance();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            lock (gate) Advance();
    }

    private void Advance()
    {
        if (index == plan.Length)
        {
            index = 0;
            previous = null;
            highHours = 0;
            alerts.Clear();
        }
        var hour = plan[index];
        actual = Simulate(hour);
        var high = actual > hour.Expected + Math.Max(0.25, 0.30 * hour.Expected);
        highHours = high ? highHours + 1 : 0;
        string? message = null;
        if (actual is null)
            message = "Meter reading missing · Check telemetry";
        else if (actual < 0.05 && hour.Expected >= 0.25)
        {
            if (previous is null or >= 0.05)
                message = "Possible power cut · Check supply and meter";
        }
        else if (previous is < 0.05 && index > 0 && plan[index-1].Expected >= 0.25)
            message = "Consumption resumed after near-zero reading";
        else if (index > 0 && previous.HasValue && hour.Machines > 0 && plan[index-1].Machines == 0
            && actual - previous < hour.Expected - plan[index-1].Expected - hour.Machines / 2)
            message = "Expected workshop startup rise missing";
        else if (highHours == 2)
            message = hour.Holiday ? "High holiday usage for two hours" : "High usage for two hours";
        if (message != null) alerts.Add(new Alert(hour.Time, message, hour.Expected, actual));
        previous = actual;
        index++;
    }

    private static double? Simulate(PlannedHour hour)
    {
        var t = hour.Time;
        if (t.Day == 1 && t.Hour is 14 or 15) return hour.Expected + 3;
        if (t.Day == 4 && t.Hour == 10) return hour.Expected - hour.Machines;
        if (t.Day == 4 && t.Hour == 11) return 0;
        if (t.Day == 5 && t.Hour == 14) return null;
        return hour.Expected * (1 + 0.025 * Math.Sin(t.Hour));
    }
}
