# DigitalOcean Test And Production Environments

The shared test environment deploys automatically from `develop`. Production is described by a separate spec, uses a different database, and remains manual until launch.

## Environment Layout

| Environment | App spec | Branch | Database | Automatic deploy |
| --- | --- | --- | --- | --- |
| Test | `.do/app-test.yaml` | `develop` | `family-organizer-test` | Yes |
| Production | `.do/app-production.yaml` | `main` | `family-organizer-production` | No |

Never point both apps at the same database or copy family records between them.

## Create The Shared Test App

1. Create or attach a DigitalOcean managed MongoDB cluster. Enable the provider's available automated backups and note the retention window.
2. Create an App Platform app from `.do/app-test.yaml` in `bschuch/todo-app`.
3. Replace `Mongo__ConnectionString` with the encrypted test database connection string. Keep `Mongo__DatabaseName=family-organizer-test`.
4. Confirm `${APP_URL}` resolves to the generated HTTPS app URL for both frontend GraphQL configuration and backend CORS.
5. Deploy. App Platform should report `/health/ready` as healthy before routing traffic.
6. Open the generated HTTPS frontend URL, sign up the first account, create the family, and create an invite in Settings.
7. Give each tester an invite code privately. Testers sign up with their own account and then join with the code.

`SeedDemoData` must remain `false` in Test and Production. There is no shared hosted demo login.

## Promotion Flow

1. Merge feature work into `develop`; the test app deploys automatically.
2. Complete the smoke checks in `docs/RELEASE_CHECKLIST.md`.
3. Merge the tested commit into `main`.
4. Do not create or deploy the production App Platform app until production launch is approved.
5. At launch, create the app from `.do/app-production.yaml`, attach a production-only MongoDB database, and trigger the first deployment manually.

## Operations

- Liveness: `GET /health/live` confirms the process is running.
- Readiness: `GET /health/ready` confirms MongoDB responds to a ping.
- Logs contain request method, path, status, duration, and server exceptions. Request bodies and authorization headers are not logged.
- Rollback: select the prior successful App Platform deployment. Database data remains unchanged because MongoDB is external to the application deployment.
- Before a database-affecting release, confirm a recent managed backup exists. Restore it into a new temporary database, point a temporary app at that database, and verify sign-in plus calendar reads before considering the restore tested.
- A production restore should first target a new database name. Change the production connection only after validation; keep the previous database available for rollback.

## Required Settings

Backend runtime settings:

- `ASPNETCORE_ENVIRONMENT=Staging` or `Production`
- `Mongo__ConnectionString` as an encrypted secret
- `Mongo__DatabaseName`
- `Cors__AllowedOrigins__0`
- `SeedDemoData=false`
- `Session__LifetimeDays=30`
- `RateLimit__AuthenticationRequestsPerMinute=10`
- `RateLimit__GraphQLRequestsPerMinute=120`

Frontend build settings:

- `VITE_GRAPHQL_URL`
- `VITE_APP_ENVIRONMENT=Test` or `Production`

Hosted startup intentionally fails when MongoDB settings or allowed origins are missing. GraphQL exception details remain available only in Development.
