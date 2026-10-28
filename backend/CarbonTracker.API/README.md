## Introduction to the greenr backend


### Prerequisites and toolchain

The API and test project both target **.NET 10 LTS**. Install the .NET 10 SDK
**10.0.4xx** (not just a runtime). The repository's `global.json` selects the
latest installed servicing patch in that feature band and excludes preview SDKs.
Run `dotnet --info` from the repository root to check the selected SDK. Keep the
SDK and runtimes serviced with current security patches. Visual Studio users need
Visual Studio 2026 with .NET 10 support, or use the CLI/VS Code.

This replaces the previous .NET 8 API / .NET 9 tests split. Both older versions
reach end of support on November 10, 2026; .NET 10 is supported through November
2028 ([Microsoft support guidance](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)).
JWT bearer authentication and the integration-test host use matching .NET 10
packages. Swagger remains on Swashbuckle; the unused
`Microsoft.AspNetCore.OpenApi` reference was removed because the API does not use
`AddOpenApi`/`MapOpenApi`.

### Restore, build, and test

Run these commands from the **repository root**, including in a fresh worktree:

```powershell
dotnet restore backend/CarbonTracker.API/CarbonTracker.API.sln
dotnet build backend/CarbonTracker.API/CarbonTracker.API.sln --no-restore
dotnet test backend/CarbonTracker.API/CarbonTracker.API.sln --no-build --no-restore
```

Restore needs access to NuGet.org on first use. Build/test do not require Python,
a local DuckDB database, or local JWT configuration. Tests include API startup,
Swagger UI and OpenAPI generation with an isolated test signing key and an
in-memory database connection, plus controller and token-service checks.

### Run locally

Initialise the database using [the database guide](../../database/README.md).
Configure `Jwt:Key` in user secrets using the root
[authentication setup and testing guide](../../README.md#authentication-setup-and-testing-issue-16),
which also includes a manual Swagger checklist. Then start the API from its project directory so the relative database path
resolves correctly:

```powershell
cd backend/CarbonTracker.API
dotnet run --launch-profile http
```

Open http://localhost:5285/swagger, then start the desired client. Visual Studio
can also start the API with F5; VS Code and Visual Studio can attach a debugger
to an API started from the CLI.

### Troubleshooting build cancellation

BE-01 reproduced `MSB5021` with SDK 10.0.401 on Windows when running
`dotnet build ... --no-restore --disable-build-servers` inside the restricted
Codex execution sandbox. The identical command, against the same fresh worktree,
succeeded outside the sandbox without changing source or deleting build output;
the original .NET 8/.NET 9 tests also passed there. This isolates the observed
cancellation to the execution environment, rather than a C# compilation error
or a required cache reset. The specific underlying OS restriction was not
identified. `MSB5021` reports cancellation; it does not identify its cause.

If this happens in a restricted runner, allow the build/compiler child processes
through the runner's approval mechanism, or use a normal developer terminal.
Do not disable host security controls. If it also fails in a normal terminal,
capture a diagnostic log and check earlier messages and runner timeout/resource
limits before assuming it is the same cause:

```powershell
dotnet build backend/CarbonTracker.API/CarbonTracker.API.sln --no-restore --disable-build-servers --verbosity diagnostic > backend-build.log
```

If output is stale or locked after switching SDKs, stop the running API/debugger
and use the SDK's scoped cleanup commands from the repository root:

```powershell
dotnet build-server shutdown
dotnet clean backend/CarbonTracker.API/CarbonTracker.API.sln
```

Then repeat restore/build/test above. These commands clean generated project
output; no recursive directory deletion or database reset is needed.


### Swagger

Swagger is a set of tools and standards for documenting, testing, and interacting with RESTful APIs. It's now part of the broader OpenAPI Specification (OAS) ecosystem, but people often still say "Swagger" to refer to both the tools and the specification.

* Describes your API (endpoints, methods, parameters, responses).
* Generates interactive documentation so developers can try out the API in the browser.
* Makes integration easier by generating client/server code automatically (if needed).

You will the it's listening to one or more addresses, you can copy-paste either of the addresses and add /swagger to open the Swagger UI in the browser.


For example:
```
http://localhost:5285/swagger/index.html
```

```
CarbonTracker.API/ # Main Web API project
├── Connected Services/ # (auto-generated) external service references
├── Dependencies/ # NuGet packages
├── Imports/ # Global using directives (C# 10+)
├── Properties/
│ └── launchSettings.json # Local run/debug profiles (e.g. IIS Express, Kestrel)
├── Contracts/ # API contracts (DTOs) exposed over the wire
│ └── ItemDto.cs # Shape of Item returned/accepted by API
├── Controllers/ # API controllers (routes, request handling)
│ ├── ItemsController.cs # Endpoints for items (/api/items)
│ └── AuthenticationController.cs # Register, login, and authenticated /api/auth/me
├── Mapping/ # Mapping logic between Models and DTOs
│ └── ItemMapping.cs # Extension methods: Item ↔ ItemDto
├── Models/ # Domain/data models (DB-facing)
│ ├── Items.cs # Item entity mapped to DuckDB items table
│ └── User.cs # User entity mapped to DuckDB users table
├── appsettings.json # Configuration (connection strings, logging, etc.)
├── bin/ # Build output binaries
└── obj/ # Build artifacts (intermediate)
```

### Contracts

Determine dat models for external communication (API)
Contracts holds Data Transfer Objects (DTOs) that define the shape of data exchanged between the client and server. 
They are designed to be lightweight and focused on the data needed for API interactions, without exposing internal implementation details.

e.g. you can 
In this use case, the DTO (Data Transfer Object) represents a subset of the database table that’s exposed through the API.

The table itself might have 10 columns, but the DTO only includes 4 of them — the ones that are relevant and safe to send or receive via the API. The remaining 6 columns stay internal to the database layer and are not part of the DTO, so they can’t be modified or even seen through that API call.

You’d typically have different DTOs (contracts) for each context or endpoint — for example:
Admin DTOs might include all 10 columns, since admins are allowed to view or modify everything.
User DTOs might only include 4–5 of those columns, omitting sensitive or system-managed fields.

This approach lets you:

Clearly define what each API can send and receive,

Protect sensitive data by never exposing it outside the intended scope,

And decouple your internal database schema from your public API contracts, so database changes don’t automatically break client integrations.

### Models
Determine data models for internal use (DB)
Models represent the internal data structures that map directly to the database tables. They include all the fields and properties needed for data storage, retrieval, and manipulation within the application.

### Controllers
Controllers handle incoming HTTP requests, process them, and return appropriate responses. They define the API endpoints and the logic for handling various operations such as creating, reading, updating, and deleting resources.

### Configuration / Appsettings.json
Basic configuration file for the backend application. It typically contains settings such as database connection strings, logging configurations, and other application-specific settings.
You can create environent specific versions of this file, e.g. appsettings.Development.json or appsettings.Production.json to override settings based on the environment the application is running in.

### Services
Services contain the business logic of the application. They interact with the data models and perform operations such as data validation, processing, and communication with external systems or databases.

### Authentication
Becrypt is used to hash user passwords before storing them in the database. This ensures that even if the database is compromised, the actual passwords remain secure.
jwt (JSON Web Tokens) are used for authenticating users and securing API endpoints. When a user logs in successfully, the server generates a JWT that the client must include in the Authorization header of subsequent requests to access protected resources.



### Database interaction

We create a connection (centrally in Program.cs) to the DuckDB database file using the connection string defined in appsettings.json.)
This connection is then injected into the controllers via dependency injection, allowing them to execute SQL queries against the database as needed.

So for a single database operation, the steps are:
1. Create connection 
1. Create Command
1. Add SQL to command
1. Add parameters to command (if any)
1. Excute command (ExecuteReader, ExecuteNonQuery, etc)

1. For a transaction, you would also:
1. Create connection
1. Begin transaction
1. Create Command
1. Add transaction to command (so it knows to use that transaction)
1. Add SQL to command
1. Add parameters to command (if any)
1. Excute command (ExecuteReader, ExecuteNonQuery, etc)
1. Commit transaction (or rollback on error)



### Dependency Ingections

The backend uses Dependency Injection (DI) to manage the creation and lifetime of various services and components. This allows for better separation of concerns, easier testing, and more maintainable code.

Lifetimes: 
- Transient: A new instance is created each time it is requested. Suitable for lightweight, stateless services.
- Scoped: A single instance is created per request. Useful for services that need to maintain state within a request but not across requests.
- Singleton: A single instance is created and shared throughout the application's lifetime. Ideal for services that maintain global state or configuration.
