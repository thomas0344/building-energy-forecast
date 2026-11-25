using System.Net.Http.Json;

record Load(string Group, double Kwh);
record ForecastHour(DateTime Timestamp, bool Holiday, Load[] Loads, double Predicted);

sealed class ModelClient(HttpClient http)
{
    public async Task<ForecastHour[]> Forecast(CancellationToken token) =>
        await http.GetFromJsonAsync<ForecastHour[]>("forecast", token)
            ?? throw new HttpRequestException("Empty model response");

    public async Task<IResult> Relay(string route, Dictionary<string, double>? inputs, CancellationToken token)
    {
        using var body = inputs is null ? null : new StringContent(
            System.Text.Json.JsonSerializer.Serialize(inputs), System.Text.Encoding.UTF8, "application/json");
        using var response = inputs is null ? await http.GetAsync(route, token)
            : await http.PostAsync(route, body, token);
        return Results.Content(await response.Content.ReadAsStringAsync(token), "application/json",
            statusCode: (int)response.StatusCode);
    }

    public async Task<bool> Healthy(CancellationToken token)
    {
        try {
            using var response = await http.GetAsync("health", token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return false; }
    }
}
