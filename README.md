# Building Energy Forecast

This project predicts how much electricity a small office uses each hour. The dashboard adds the hourly predictions to show daily and weekly totals.

I used Python to generate one year of sample meter readings. The equipment falls into four groups: security, office equipment, machines, and cooling. On holidays, the office is closed, but the cameras and security desk still use electricity.

## Model used

I used linear regression. The model takes the estimated energy consumption of the four equipment groups and uses historical data to find a suitable weight for each group.

The weights are non-negative. I chose this model because equipment may use less than its full rated power. The weights help adjust the estimates to match the meter readings.

I used the first 7,320 hourly records for training and the remaining 1,440 records (60 days) for testing. On the test data, the predictions were off by 0.214 kWh per hour on average.

The trained model is saved in [models/model.json](models/model.json), so the application can use it each time it starts without training it again.

## How to run

Install Python 3.9 or newer and the .NET 11 SDK. Open three terminals in the project folder and run one command in each.

| Terminal | Command |
| --- | --- |
| 1. Python model | `python3 model_service/server.py` |
| 2. C# backend | `dotnet run --project backend -- --urls http://localhost:5080` |
| 3. Frontend | `python3 -m http.server 5173 --bind 127.0.0.1 --directory frontend` |

Keep the three terminals open while using the [dashboard](http://localhost:5173).
