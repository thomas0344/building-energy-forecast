# Building Energy Forecast

An ASP.NET Core application that forecasts hourly electricity use for a small office. Compare holiday and working-day loads, inspect meter alerts, and see how closely the regression model predicts energy use on simulated data.

## Panel walkthrough

1. Read the [three-page project guide](docs/build-guide.pdf) for the design questions.
2. Follow a forecast request from the [C# API](backend/Program.cs) through its [HTTP client](backend/ModelClient.cs) to the [Python model service](model_service/server.py).
3. Inspect the [training script](training/train.py), [learned weights](models/model.json), and [test results](results/metrics.json). Average hourly error was **0.214 kWh**, compared with **0.399 kWh** for the same-hour-last-week baseline, across the final 60 days of simulated readings.
4. Run the dashboard below. Compare Friday 1 January with Monday 4 January at 10:00, when workshop machines start.

## How to run

Clone this repository or download and extract its ZIP, then open the repository root in your editor.

Requirements are .NET 11 SDK and Python 3.9 or above.

Open 3 terminals in the same project folder.

Terminal 1

    python3 model_service/server.py

Terminal 2

    dotnet run --project backend -- --urls http://localhost:5080

Terminal 3

    python3 -m http.server 5173 --bind 127.0.0.1 --directory frontend

After running these open http://localhost:5173 in the browser.

Create an account using email and password. Password should have minimum 12 characters. After that the dashboard will be available.

All 3 terminals should be running while using the application.

## About the project

My idea was to calculate the energy consumption based on the equipment available in a building and its working time.

For example there are 16 CCTV cameras and each camera consumes 12 watts.

16 × 12 / 1000 = 0.192 kWh

Also there is a security recorder which consumes 0.190 kWh and board lights which consume 0.180 kWh during night time.

I divided the equipment into 4 groups.

- Security
- Office equipment
- Machines
- Cooling

During holidays and weekends the office will be closed but security equipment will still be working.

Normal office time is 8 AM to 6 PM and machines work from 10 AM to 4 PM. Cooling consumption also changes according to the outside temperature. Computers consumption depends on the number of people working.

All these details are given in data/equipment.json.

## Dataset and training

Since actual building electricity data was not available I generated sample historical data using Python.

For generating data and training the model run

    python3 training/generate.py
    python3 training/train.py
    python3 database/build_seed.py

There are 8760 hourly records in meter.csv which is one year of data. Also future.csv contains 168 records for the next 7 days.

The dataset contains date and time, holiday, temperature, occupancy and energy consumption of different equipment groups.

The historical dataset also contains energy_kwh which is the simulated actual meter reading. Some random variations are added to the readings.

## Model used

I used linear regression without intercept for this project. The model takes the estimated energy consumption of the 4 equipment groups and finds suitable weights for each group based on the historical data.

The weights are non-negative.

The reason for using this model is that the equipment may not always consume its full rated power and the model can adjust the estimated consumption.

First 7320 records are used for training and the remaining 1440 records (60 days) are used for testing.

The trained model is saved in models/model.json. So every time we open the application there is no need to train the model again.

## Model accuracy

The results I got from testing are

- MAE - 0.214 kWh
- RMSE - 0.464 kWh
- MAPE - 4.42%
- R² - 0.9955

I also compared the prediction with the consumption of the same hour in the previous week. Its MAE was 0.399 kWh.

These results are based on generated data. In an actual building the accuracy may be different because of unexpected working hours, weather changes, equipment problems etc.

## Working of application

For backend I used C# ASP.NET Core and for the model I used Python. Frontend is made using HTML, CSS and JavaScript.

SQLite is used for the database.

The frontend runs on port 5173, C# backend on 5080 and Python model service on 5091.

The frontend sends requests to the C# backend and the backend communicates with Python using HTTP requests. Python calculates the prediction and returns the result to C#. Then the result is displayed in the dashboard.

ModelClient.cs is used for connecting the backend with the Python service.

Main files are

- backend/Program.cs
- backend/ModelClient.cs
- frontend/app.js
- frontend/index.html
- frontend/style.css
- training/train.py
- models/model.json

In the dashboard the daily consumption is calculated by adding the 24 hourly predictions. Peak consumption means the highest consumption in a single hour.

We can select a particular day and see its hourly consumption and equipment breakdown.

There is also a model performance section where actual and predicted values can be compared. Dataset can also be downloaded from the dashboard.

## Meter alerts

I also added a meter monitoring feature to check whether the actual consumption is different from the predicted consumption.

For demonstration one simulated hourly reading is processed every 5 seconds.

Some situations added in this are high electricity usage during holidays, machine not starting at its scheduled time, possible power cut and missing meter reading.

For example on Monday at 10 AM machines are supposed to start. If the expected increase in consumption is not happening it will generate an alert.

High consumption alert is generated when consumption exceeds the forecast by more than 0.25 kWh or 30%, whichever is higher, for two consecutive hours.

If the consumption is below 0.05 kWh when expected consumption is at least 0.25 kWh it shows a possible power cut. It doesn't necessarily mean there is an actual power cut.

Missing meter data is shown as a separate alert.

The monitoring code is in backend/MeterMonitor.cs. The frontend updates the alerts every 5 seconds.

Currently alerts are stored in memory and the sample week repeats.

## Login and database

There is a login and signup page in the application. Passwords are stored as salted hashes and session cookies are used for authentication.

Some of the APIs used are

POST /api/auth/signup

POST /api/auth/login

POST /api/auth/logout

GET /api/health

GET /api/energy/forecast?hours=24

GET /api/energy/monitor

The database is SQLite and it runs inside the C# backend using Microsoft.Data.Sqlite.

Account details are stored in energy.sqlite inside App_Data by default. The sample database doesn't contain any user accounts.

The live App_Data folder should not be included while submitting the project.

## System health

I added a system health option to check whether the backend, database and Python model service are working properly.

It checks the health API every 15 seconds.

It shows System healthy, System degraded or API unreachable depending on the status.

If the Python service is stopped the backend can still run but new prediction requests will return HTTP 503.

The Python model is independent from the C# backend.

We can directly check the model using

http://127.0.0.1:5091/model

Also to test the model separately run

    python3 model_service/demo.py

In this demo removing 7.2 kWh of scheduled machine load reduces the predicted consumption by around 5.89 kWh.

Example prediction input

    {"security":0.382,"office":4,"machines":7.2,"cooling":6}

The Python service also has /forecast and /health endpoints.

## Testing and making changes

To test the project we can select January 1 which is a holiday and check the equipment consumption. Then select Monday January 4 and check the consumption at 10 AM when the machines start working.

We can also change the number of cameras in equipment.json and generate the data and train the model again.

After changing the data or training run database/build_seed.py and restart the backend and model service.

To change only future consumption we can edit future.csv without changing the historical data.

Electricity cost can also be estimated by multiplying the total kWh with the electricity rate per unit.

## Limitations and future improvements

Currently the project uses simulated electricity readings and assumed weather conditions.

In future actual smart meter readings can be connected to the application and the model can be trained using real data. Actual weather data can also be used.

The meter alert values can be adjusted according to the building.

If cumulative meter readings are used we need to calculate the difference between consecutive readings to get hourly consumption.

## AI assistance

My initial idea was to predict energy consumption based on equipment and its working schedule and also consider holidays and non-working hours.

AI assisted with implementation, testing and documentation. The application uses the trained regression model for its forecasts.
