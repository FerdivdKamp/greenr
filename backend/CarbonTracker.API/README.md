## Introduction to the greenr backend


To run the backend, when this is running you can start the mobile app (see the mobile app README).
```bash
dotnet run
```


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
│ └── UsersController.cs # Endpoints for users (/api/users)
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

