# Greenr delivery plan and GitHub issue backlog

## Current picture

Greenr is a learning project for helping a person understand their carbon footprint. It has four parts:

- **Backend:** ASP.NET Core 8 API with DuckDB, JWT token creation, items, and versioned SurveyJS questionnaires.
- **Frontend:** React, TypeScript, Vite, React Query, SurveyJS, charts, and some Storybook setup.
- **Mobile:** Flutter prototype that lists users and items and visualises an item's footprint over time.
- **Database:** DuckDB schema and seed-data Python scripts.

There is a useful foundation, but the components do not yet form a secure end-to-end product. In particular, the API schema and authentication service disagree about `user_name` versus `username`; authentication/authorization middleware is not in the request pipeline; item and questionnaire data are not scoped to the signed-in user; clients contain hard-coded development URLs/IDs; and automated tests are mostly scaffolding. A local `dotnet build --no-restore --disable-build-servers` currently stops with `MSB5021` (compiler process cancelled) without reporting a compile error, so build health must be established first.

## Product assumption and questions

This backlog assumes the first vertical slice is a **personal carbon-tracking MVP**: a user signs up, signs in, records and manages their own purchases, and sees a simple footprint summary. Questionnaires remain a second vertical slice, once identity and ownership are sound.

Please answer these before starting the corresponding milestone; they intentionally do not block the foundation work.

1. Is the MVP above the intended priority, or should **questionnaire authoring/completion** be the primary first user journey?
2. Should DuckDB remain the long-term single-user/development store, or should a future issue prepare a move to PostgreSQL for multi-user deployment?
3. Which client is the first-class experience for this release: web, Android Flutter, or both equally?
4. Do you want only personal accounts initially, or should the questionnaire author/publisher be a distinct admin role?

## Global plan

| Phase | Outcome | Exit check |
| --- | --- | --- |
| 0. Stabilise | A repeatable local setup and a green build/test command. | A new contributor can initialise data and build/test all active projects from documented commands. |
| 1. Secure the foundation | One consistent schema and authenticated, user-owned API resources. | A user cannot read or alter another user's items or responses. |
| 2. Deliver the item MVP | Web or mobile user can complete the core item journey. | Register, login, create/edit/delete an item, and see own totals works end to end. |
| 3. Complete questionnaires | Controlled authoring/publishing and authenticated completion. | A published questionnaire can be completed once by the intended user and results are attributable. |
| 4. Improve confidence | Meaningful tests, accessibility, observability, CI, and deployment choices. | Pull requests automatically validate the system and release configuration has no committed secrets. |

### Recommended learning order

Start with issues **DB-01**, **BE-01**, **BE-02**, and **BE-03**. They make the application runnable, teach schema/API/security fundamentals, and unblock every client feature. Then choose **FE-01 + FE-02** for a web-first journey or **MO-01 + MO-02** for Flutter-first. Work in small branches; every issue should finish with a manual Swagger/client check and at least one automated test where practical.

## Ordered GitHub issues

Issue numbers below are backlog order, not existing GitHub issue IDs. Suggested labels use `area:*`, `type:*`, and `priority:*`.

### Database

#### DB-01 — Create one versioned database schema and remove schema drift

`area:database`, `type:bug`, `priority:critical`  
**Depends on:** none

The initializer defines `users.user_name`, while `UsersService.EnsureSchema` creates `username`; the service itself queries `user_name`. Make the database scripts the single source of truth, decide the canonical column names, and remove runtime table creation from the API. Introduce numbered, idempotent migration scripts (or a deliberately small migration runner) and record applied versions.

**Acceptance criteria**

- A blank database can be created from documented commands and contains the schema expected by the API.
- The users, refresh-token, reset-token, items, questionnaire, response, and response-item definitions agree with the C# queries.
- Applying migrations twice is safe, and an upgrade path is documented for an existing local database.

#### DB-02 — Make seed data repeatable and safe for development

`area:database`, `type:chore`, `priority:high`  
**Depends on:** DB-01

Replace the current “delete the database and rerun Python” workflow with explicit `init`, `seed`, and (if wanted) `reset-dev` commands. Keep destructive reset opt-in and make seeds deterministic enough for UI and API testing.

**Acceptance criteria**

- Initialisation and seeding can be run from the repository root and are documented.
- Reset is clearly named as destructive; normal seed runs do not accidentally delete data.
- Seeded accounts and sample data are development-only and documented without presenting a production credential pattern.

#### DB-03 — Add data ownership, integrity constraints, and useful indexes

`area:database`, `type:feature`, `priority:high`  
**Depends on:** DB-01

Make ownership a database concern as well as an API concern: require `items.user_id`, decide whether a response must have a `user_id`, add foreign keys/checks for questionnaire status/version, and index common per-user queries.

**Acceptance criteria**

- Items always belong to a user and item queries are efficient by user and date.
- Responses have an explicitly chosen anonymous/authenticated policy.
- Status values and version relationships cannot silently become invalid.

### Backend

#### BE-01 — Make the backend build and test command reliable

`area:backend`, `type:bug`, `priority:critical`  
**Depends on:** none

Investigate the reproducible `MSB5021` compiler-process cancellation. Capture the actual environment/toolchain cause, align target frameworks where appropriate (API is .NET 8; tests are .NET 9), and remove stale build artefacts only through safe, documented commands.

**Acceptance criteria**

- `dotnet restore`, `dotnet build`, and `dotnet test` have documented commands and succeed from a clean checkout.
- The solution uses a deliberate, supported SDK/target-framework strategy.
- A short troubleshooting note explains the resolved cancellation and any required local prerequisites.

#### BE-02 — Complete JWT authentication plumbing and secure user endpoints

`area:backend`, `type:security`, `priority:critical`  
**Depends on:** DB-01, BE-01

Add `UseAuthentication()` and `UseAuthorization()` in the correct request-pipeline order; validate register/login input; map expected domain errors to safe HTTP responses; protect/remove the public user-list endpoint; and move the development JWT key out of committed configuration into user secrets/environment configuration.

**Acceptance criteria**

- A valid bearer token reaches an `[Authorize]` endpoint and an absent/invalid one returns 401.
- Register, duplicate registration, invalid login, and `/api/auth/me` have defined, tested responses.
- No JWT signing secret is committed; local setup states where to set it.

#### BE-03 — Implement user-owned item CRUD and summary API

`area:backend`, `type:feature`, `priority:critical`  
**Depends on:** DB-03, BE-02

Replace client-supplied/general item access with authenticated endpoints that derive `user_id` from claims. Use create/update DTOs and validation, add `DELETE`, and expose an intentionally simple summary (for example total footprint, item count, and category/date breakdown) rather than embedding business logic in clients.

**Acceptance criteria**

- Users can create, list, update, and delete only their own items.
- Invalid prices, footprints, names, and dates receive clear 400 validation responses.
- The item list and summary are covered by API/integration tests, including cross-user isolation.

#### BE-04 — Finish refresh-token lifecycle and password-reset delivery contract

`area:backend`, `type:security`, `priority:high`  
**Depends on:** DB-01, BE-02

Token rows exist but there are no refresh/revoke endpoints, and reset tokens are created but never delivered. Add rotation/revocation/expiry handling and define a development-safe reset delivery adapter (log/mail sandbox) before choosing a real email provider.

**Acceptance criteria**

- Refreshing rotates or otherwise safely manages tokens; logout revokes the active token.
- Expired/revoked tokens fail predictably.
- Reset requests remain account-enumeration safe and can be tested without exposing tokens in production responses.

#### BE-05 — Secure and clarify questionnaire administration and responses

`area:backend`, `type:feature`, `priority:high`  
**Depends on:** DB-03, BE-02

Define roles/permissions if administration is retained. Restrict creating, publishing, and listing definitions appropriately; derive response owner from the auth claim instead of accepting arbitrary `UserId`; validate status and submitted answers against the published definition as far as the chosen product rules require.

**Acceptance criteria**

- Only the chosen admin role can author/publish; ordinary users can retrieve published content only.
- A response cannot be attributed to another user by changing JSON.
- Publish/version behaviour and response submission have integration tests.

#### BE-06 — Make the API consumable by both clients

`area:backend`, `type:feature`, `priority:medium`  
**Depends on:** BE-03

Configure development CORS for the web app, keep OpenAPI accurate, standardise problem responses, and document the base URLs for browser, Android emulator, and physical devices. Do not allow arbitrary production origins.

**Acceptance criteria**

- Web development and Android emulator requests work without disabling browser/platform safeguards.
- Swagger/OpenAPI documents auth, request shapes, and representative failure responses.
- Client setup is documented in one place.

### Frontend (React)

#### FE-01 — Establish frontend environment configuration and API error handling

`area:frontend`, `type:chore`, `priority:high`  
**Depends on:** BE-02, BE-06

Provide a tracked `.env.example`, validate `VITE_API_BASE_URL`, and make the HTTP wrapper attach the access token, handle 401/logout, and surface useful errors. Keep secrets out of Vite environment variables.

**Acceptance criteria**

- A new developer can configure and run the frontend without editing source URLs.
- Protected calls send the bearer token and a 401 produces a clear signed-out state.
- API errors are readable and not silently swallowed.

#### FE-02 — Build the web authentication journey

`area:frontend`, `type:feature`, `priority:high`  
**Depends on:** FE-01

Implement register, login, logout, persisted session/refresh strategy, protected routes, and a small account view. Choose token storage deliberately and document the security trade-off.

**Acceptance criteria**

- A user can register, sign in, reload, sign out, and cannot visit protected item routes while signed out.
- Form validation and server errors are accessible.
- The journey has component and/or browser-level coverage.

#### FE-03 — Turn Items into a complete personal tracker UI

`area:frontend`, `type:feature`, `priority:high`  
**Depends on:** FE-02, BE-03

Replace manual prototype loading with an own-items list, create/edit/delete form, loading/empty/error states, filters, and summary/chart driven by the API. Keep charts honest about the chosen footprint calculation rather than implying unimplemented science.

**Acceptance criteria**

- The complete item CRUD journey works from the browser for the signed-in user.
- Mobile-width layout, keyboard interactions, labels, and contrast are checked.
- Cached queries update after mutations without a full page reload.

#### FE-04 — Finish questionnaire participant and admin flows

`area:frontend`, `type:feature`, `priority:medium`  
**Depends on:** FE-02, BE-05

Remove the hard-coded canonical questionnaire ID, add a questionnaire selection/history route, and only expose author/publish screens to the configured role. Give submission success/failure a durable, accessible UI.

**Acceptance criteria**

- Participants can find and complete a published questionnaire without changing source code.
- Admin controls are hidden and protected for non-admin users.
- Survey loading and invalid/submission states are tested.

#### FE-05 — Make React quality checks part of normal work

`area:frontend`, `type:quality`, `priority:medium`  
**Depends on:** FE-03

Repair/complete the Storybook/Vitest setup, add focused tests for API hooks and key forms, run lint/format/type-check consistently, and remove generated documentation from version control if it is build output.

**Acceptance criteria**

- `npm run lint`, `npm run build`, and the chosen test command are documented and green.
- Key reusable UI states can be viewed/tested in Storybook or equivalent.

### Mobile (Flutter)

#### MO-01 — Make the Flutter app configurable and runnable on supported targets

`area:mobile`, `type:bug`, `priority:high`  
**Depends on:** BE-01, BE-06

Remove hard-coded `10.0.2.2:7285` endpoints and define development configuration for Android emulator, physical device, and optional web/desktop. Confirm whether HTTP/HTTPS and local certificates work for the target.

**Acceptance criteria**

- The configured API base URL is not duplicated in widgets.
- A documented emulator run reaches the API and displays a useful connection error when unavailable.
- `flutter analyze` and `flutter test` are green.

#### MO-02 — Add mobile sign-in and authenticated item list

`area:mobile`, `type:feature`, `priority:high`  
**Depends on:** MO-01, BE-02, BE-03

Build a minimal navigation structure with sign-in/sign-out, token/session management, own-item list, and resilient loading/error/empty states. Do not retain the public user-list screen as an app entry point.

**Acceptance criteria**

- An Android user can sign in and see only their items.
- Signed-out and expired-session behaviour is clear.
- Widget tests cover parsing and the principal loading/error states.

#### MO-03 — Deliver mobile item entry and footprint summary

`area:mobile`, `type:feature`, `priority:medium`  
**Depends on:** MO-02

Add an accessible item form, edit/delete actions, and a summary/chart based on BE-03. Keep the current amortisation visualisation only if it represents a documented calculation users can understand.

**Acceptance criteria**

- Create/edit/delete synchronises with the API and refreshes the list/summary.
- Form and destructive-action confirmation work comfortably on a small screen.

#### MO-04 — Decide whether questionnaires belong in the mobile release

`area:mobile`, `type:decision`, `priority:low`  
**Depends on:** BE-05

Evaluate native Flutter form rendering versus a web view/deferring to the React web experience. This should be a short decision record before building a second SurveyJS-style client.

**Acceptance criteria**

- The decision records user value, maintenance cost, offline needs, accessibility, and a recommended next action.

### Cross-cutting quality and delivery

#### QD-01 — Add an end-to-end local developer guide

`area:delivery`, `type:documentation`, `priority:high`  
**Depends on:** DB-02, BE-01, BE-06

Replace scattered and stale startup notes with a root guide: prerequisites, initialise/seed database, configure secrets, run API, run web, run Flutter, test commands, ports, and how the projects communicate.

**Acceptance criteria**

- A newcomer can reach the selected first client journey from a clean checkout.
- Commands work from the stated directory and distinguish safe from destructive operations.

#### QD-02 — Establish automated checks in GitHub Actions

`area:delivery`, `type:ci`, `priority:medium`  
**Depends on:** BE-01, FE-05, MO-01

Add separate, cache-aware workflows/jobs for .NET build/test, frontend lint/build/test, and Flutter analyse/test. Keep secrets out of CI logs and initially run only checks that are locally reproducible.

**Acceptance criteria**

- Pull requests show pass/fail results for every active discipline.
- The workflow uses pinned/reproducible toolchain versions and does not need a committed local database.

#### QD-03 — Add structured logging, health checks, and a deployment decision record

`area:delivery`, `type:operations`, `priority:low`  
**Depends on:** BE-03

Introduce a non-sensitive API health endpoint and request/error logging, then document the hosting model, HTTPS/reverse-proxy requirements, database backup story, and the DuckDB-versus-server-database decision.

**Acceptance criteria**

- Operators can determine whether the API and database dependency are healthy without seeing credentials or personal data.
- The repository documents a realistic deployment and recovery approach for the intended scale.

