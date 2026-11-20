# Operations and deployment decision

## Current decision

For a small, single-instance deployment, use one Windows host running the .NET
10 API and the bundled Windows DuckDB library, with one persistent local database
file on a backed-up volume. Serve the built web app from a reverse proxy and
send `/api` to the API on a loopback-only HTTP listener. This is a proposed
operating model, not an already provisioned production service. Do not run
multiple API processes or replicas against the same DuckDB file. Plan a
maintenance window for upgrades and backups. DuckDB is suitable here only while
the single-host availability and write-concurrency limits are acceptable; it is
not a horizontally scalable shared database.

Move to a managed server database such as PostgreSQL before requiring multiple
API instances, independent writers, high availability, or stronger recovery
objectives. That is a migration project: adapt DuckDB-specific SQL and types,
create and verify a data migration, test concurrency and rollback, and update
backup and monitoring procedures before switching traffic.

## Before exposing the service

- Run in `Production`, bind the API only to loopback, and configure `Jwt:Key`
  through the host's secret store or a protected environment variable. Use a
  unique high-entropy key, persistent across restarts and instances; changing it
  invalidates issued access tokens. Never commit or log it.
- Terminate HTTPS at a maintained reverse proxy with a valid certificate and
  automatic renewal. Serve web and API on the same HTTPS origin so `/api`
  works without the development CORS policy. Configure the mobile client with
  the public HTTPS API and website origins. Block direct public access to the
  API port. Do not trust forwarded headers from arbitrary clients; configure
  trusted proxies explicitly if forwarded headers are later needed.
- Use an absolute persistent `ConnectionStrings__DuckDb` connection string;
  run the versioned migrations before starting the API. Grant only the service
  account access to the data directory and its files. Do not seed production
  with the development account or store the database in the deployment folder.
- Password reset delivery is disabled outside Development until a real delivery
  adapter is implemented. Development delivery logs email and reset tokens, so
  do not enable the Development environment on a deployed service. Swagger is
  currently exposed by the app: restrict it at the proxy if public access is not
  intended. Set up monitoring and verify a restore before accepting real data.

## Health and logs

`GET /health` is anonymous and returns HTTP 200 with `healthy` status when the
API can open the configured database and query the `users` table, or HTTP 503
with `unhealthy` status on failure. Responses are not cached and contain no
connection details. This is a readiness signal, not a complete integrity,
writeability, migration-version, or backup check. A missing database file may
be created by DuckDB when connecting; the missing table still yields 503.
Restrict the endpoint to the monitoring network at the proxy if desired. Alert
on sustained failures and investigate database availability, permissions, disk
space, and schema state on the host without exposing diagnostics publicly.
Swagger lists it under **Internal** for operational endpoints. This tag is only
a documentation group; it does not authenticate callers or restrict access.

The API writes JSON console logs with method, route template, status and elapsed
time, plus a generic error on unhandled failures. Request paths, query strings,
headers, bodies and exception details are not included in these application
request/error events. Disable or redact equivalent proxy access logs and review
other framework and infrastructure logs before shipping them to a centralized
system. Store logs with restricted access and a defined retention period. The
development-only reset delivery exception described above still applies.

## Backup and recovery

1. Schedule a maintenance window, stop incoming traffic and the API, and wait
   for all DuckDB connections and writers to close cleanly. Include any other
   process that can write the database.
2. Copy the closed database file to encrypted, access-controlled storage on a
   separate host or service. Keep versioned copies with a retention policy and
   verify the copy completed. Never copy a live database file as the backup
   procedure; a write-ahead log or concurrent writes may make that copy invalid.
3. Regularly restore a copy to an isolated location, start a separate API against
   it, check `/health`, sign in with a controlled test account and verify a
   representative record. Record the result and restore time. Keep production
   data and credentials protected during the test.
4. For recovery, stop traffic and the API, preserve the damaged database and
   related files for investigation, restore a verified backup to the configured
   path, run any migrations required by the deployed release, start the API,
   check health and a representative user journey, then resume traffic.

The maximum data loss is the time since the last successful backup; recovery
time includes provisioning and restore. Set a backup interval and retention to
match the actual service objectives, and rehearse recovery after schema changes.
Do not use `reset-dev` for production recovery.
