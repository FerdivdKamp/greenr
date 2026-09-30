
# greenr


This repository contains 
database (DuckDb Visuals Studio Code)
backend (dotnet, Visual Studio)
mobile app (Flutter, Visual Studio Code)

Each has its own subfolder with a README.md for its part


To get started.

1. The database folder has explicit commands to initialize and seed a local DuckDB database. Follow [database/README.md](database/README.md).
1. Open the backend solution in Visual Studio and make a build
1. Open the mobile app, run the emulator and after that flutter

The mobile app will talk with the database through the backend.



## Frontend

Uses react typescript and vite
https://nodejs.org/en/download



# Functional stuff

## Creating questionnaires

You can use this and save and edit the JSON locally
https://surveyjs.io/try/reactjs#try-creator

After that the backend has an endpoint to store the questionnaire in the database, or add a new version of it and publish it. Just check swagger






### Quick Start
```
python -m pip install -r database/requirements.txt
python database/dev_db.py init

cd backend\CarbonTracker.API
dotnet run --launch-profile http
```

Then open: http://localhost:5285/swagger
