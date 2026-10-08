# Task ownership migration and deployment

Tasks now require a valid `FamilyId`. Board names are display/grouping metadata, not an authorization boundary. Existing tasks with missing or invalid family ownership are inaccessible until repaired.

## Migration

Back up the MongoDB database before applying changes. Stop backend instances and other writers while applying the migration so membership, family, and task data cannot change during ownership resolution. Use the target environment's existing Mongo configuration; never point tests at that database.

Preview changes (default is read-only):

```bash
dotnet run --project TodoBackend -- --migrate-task-ownership
```

Apply reviewed changes:

```bash
dotnet run --project TodoBackend -- --migrate-task-ownership --apply
```

These commands exit without starting the HTTP server, creating indexes, or seeding demo data. They use the application's usual environment and configuration requirements.

- Valid existing family IDs are preserved; board values are aligned with that family.
- Missing family IDs are backfilled only when the board matches exactly one family.
- Unmatched boards, colliding boards, and invalid existing family IDs are reported by task ID and left unchanged. Determine ownership from trusted records, then explicitly repair those records. Do not infer ownership from an ambiguous board name.
- Re-running the migration after successful application makes no further changes.

Deploy the backend and frontend together. `tasks` now requires `familyId: String!` instead of `boardId`. `createTask` requires `familyId: String!` and no longer accepts `boardId`; the backend derives the board from the family. The legacy `todos` query retains its shape but returns only tasks in accessible existing families.

Development demo mode remains local-only. Demo seed tasks now contain the seeded family's ID; existing seed tasks need the migration too.

## Validation

```bash
npm run test:run --prefix todo-frontend
npm run build --prefix todo-frontend
dotnet test TodoBackend.Tests/TodoBackend.Tests.csproj
```

MongoDB integration tests require a running disposable/test MongoDB server:

```bash
TEST_MONGO_CONNECTION_STRING=mongodb://localhost:27017 dotnet test TodoBackend.Tests/TodoBackend.Tests.csproj
```

Each integration test creates and drops its own randomly named database. The connection must permit database creation and deletion. Without this variable, MongoDB integration tests are explicitly skipped. Verify these tests in the test environment before production rollout.
