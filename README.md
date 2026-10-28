
# greenr


This repository contains 
database (DuckDb Visuals Studio Code)
backend (dotnet, Visual Studio)
mobile app (Flutter, Visual Studio Code)

Each has its own subfolder with a README.md for its part


To get started.

1. The database folder has explicit commands to initialize and seed a local DuckDB database. Follow [database/README.md](database/README.md).
1. Install the .NET 10 SDK and follow the backend [restore, build, and test guide](backend/CarbonTracker.API/README.md).
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
# Set Jwt:Key in user secrets before starting; see Authentication testing below.
dotnet run --launch-profile http
```

Then open: http://localhost:5285/swagger

## Authentication setup and testing (issue #16)

Run the following PowerShell commands from the repository root. The API targets
.NET 10, as does the test project; install the .NET 10 SDK in the
10.0.4xx feature band selected by `global.json`. Initialize the database as
shown in Quick Start.

Generate a local JWT signing key and store it outside the repository:

```powershell
$jwtBytes = New-Object byte[] 32
$jwtRandom = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRandom.GetBytes($jwtBytes)
$jwtRandom.Dispose()
$jwtKey = [Convert]::ToBase64String($jwtBytes)
dotnet user-secrets set 'Jwt:Key' $jwtKey --project backend/CarbonTracker.API
Remove-Variable jwtKey, jwtBytes, jwtRandom
```

User secrets load in the `Development` environment used by the `http` launch
profile and Visual Studio. For other environments, supply `Jwt__Key` through
your environment or secret manager (at least 32 UTF-8 bytes). The API fails at
startup if the key is absent or too short. Replace any previously used committed
development key; changing the key invalidates old access tokens. Keep the key out
of appsettings files, source control, and client configuration.

Run the automated checks from the repository root:

```powershell
dotnet restore backend/CarbonTracker.API/CarbonTracker.API.sln
dotnet build backend/CarbonTracker.API/CarbonTracker.API.sln --no-restore --disable-build-servers -m:1
dotnet test backend/CarbonTracker.API/CarbonTracker.API.sln --no-build --no-restore --disable-build-servers -m:1
```

To run just the authentication integration tests (building first if needed):

```powershell
dotnet test backend/CarbonTracker.API/CarbonTracker.API.sln --disable-build-servers -m:1 --filter FullyQualifiedName~AuthenticationTests
```

The integration tests run the actual HTTP pipeline with a random signing key
and a temporary DuckDB database created from the canonical schema. They do not
need your user secrets, a running API, or your development database.

For a manual check, start the API:

```powershell
Push-Location backend/CarbonTracker.API
dotnet run --launch-profile http
# After stopping the API with Ctrl+C:
Pop-Location
```

Open [Swagger](http://localhost:5285/swagger) and use **Try it out**:

1. Call `GET /api/auth/me` before authorizing: expect **401**.
2. Register with `POST /api/auth/register`, for example
   `{"username":"auth_demo","email":"auth_demo@example.invalid","password":"Local-demo-123!"}`:
   expect **200** with `Account created.` Use a unique name/email on later runs.
3. Repeat registration: expect **409** with a safe problem response.
   Missing fields, an invalid email, a username longer than 20 characters, or a
   password shorter than 8 characters return **400** with validation errors.
   Usernames allow letters, digits, underscores, dots, and hyphens. Passwords
   cannot exceed 72 UTF-8 bytes (BCrypt's input limit).
4. Call `POST /api/auth/login` with
   `{"identifier":"auth_demo","password":"wrong-password"}`: expect **401**.
   An unknown account receives the same error. Use the correct password:
   expect **200** with `accessToken` and `refreshToken`. Email also works as
   the identifier; username/email matching ignores case.
5. Click **Authorize**, paste only the `accessToken` value (Swagger adds the
   `Bearer` prefix), and call `GET /api/auth/me`: expect **200** with the
   registered `userId` and `username`.
6. Replace the token with invalid text: expect **401**. Expired tokens and
   tokens with the wrong signature, issuer, or audience also return **401**;
   these cases are covered by automated tests.
7. `GET /api/users` returns **404**: the public account directory was removed.
   The mobile prototype's user-list screen will need the planned sign-in flow. 
   Since it is not in Swagger use http://localhost:5285/api/users 

Refresh-token rotation and password-reset delivery remain separate backlog
work; this issue does not add refresh/revoke endpoints.

## Personal items setup and testing (issue #17)

Apply the database migrations with `python database/dev_db.py init`, configure
the signing key above, and start the API. All `/api/items` endpoints now require
a bearer token and use its user ID. There is no public all-items endpoint.
Client-supplied `userId` and `itemId` fields are ignored; create assigns a new
ID, and update uses the ID in the URL. Existing clients must sign in first.

| Endpoint | Result |
| --- | --- |
| `GET /api/items` | Your items, ordered by purchase date descending, unknown dates last. |
| `GET /api/items/{id}` | Your item, or 404 for a missing item or another user's item. |
| `POST /api/items` | Create your item; 201 with the saved item and a Location header. |
| `PUT /api/items/{id}` | Replace your item's editable fields; 200 with the saved item, or 404. |
| `DELETE /api/items/{id}` | Delete your item; 204, or 404. |
| `GET /api/items/summary` | Your item count, total price, total recorded footprint, and breakdown by `useCase`. |

Create and update use the same body (PUT replaces all editable fields):

```json
{
  "itemName": "Bicycle",
  "useCase": "Transport",
  "price": 125.50,
  "footprintKg": 12.345,
  "dateOfPurchase": "2020-06-15"
}
```

`itemName` is required, cannot be whitespace, and allows at most 50 characters.
`useCase` allows at most 20 characters and defaults to an empty string; null is
invalid. Names and use cases are trimmed when saved. `price` and `footprintKg`
are required and nonnegative, with at most two and three decimal places and
maximum values of 99999999.99 and 9999999.999 respectively. `dateOfPurchase`
may be omitted or null for an unknown date; otherwise it must be a valid ISO
date after 0001-01-01 and on or before today in UTC. Invalid input returns a
400 validation problem with field errors.

The summary returns `itemCount`, `totalPrice`, `totalFootprintKg`, and
`byUseCase` (groups with `useCase` and the same three totals). An empty account
has zero totals and an empty breakdown. Footprint totals simply add the stored
kilograms; they do not estimate emissions or amortise purchases. Legacy null
amounts count as zero, and null use cases join the empty use-case group.

Manual Swagger check:

1. Without authorization, call `GET /api/items` and `GET /api/items/summary`:
   expect 401. Register and log in as account A using the authentication guide.
2. Authorize as A, create the example item, and copy the returned `itemId`.
   Check that list, get-by-ID, and summary include the saved values.
3. Update the item and verify the list and summary reflect the new values.
   Try a negative price, blank name, or future date: expect 400 field errors.
4. Register/login as account B and replace Swagger's authorization with B's
   access token. A's item must be absent from B's list and summary. Reading,
   updating, or deleting A's item by ID must return 404.
5. Authorize as A again, delete the item (204), and verify it is absent from
   the list and summary. A second delete returns 404.

Run the complete automated suite using the commands above, or just item tests:

```powershell
dotnet test backend/CarbonTracker.API/CarbonTracker.API.sln --disable-build-servers -m:1 --filter FullyQualifiedName~ItemsTests
```

The tests use real registration/login, HTTP endpoints, and an isolated DuckDB
database with the canonical migrations. They cover CRUD, validation, summaries,
missing/invalid owner claims, forged body IDs, and cross-user isolation.
