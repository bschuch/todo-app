# Family Organizer

A private family dashboard for calendars, chores, members, and household planning. The app uses a React/Vite frontend, a .NET 8 GraphQL backend, and MongoDB storage.

## Prerequisites

- Node.js 20+
- .NET SDK 8+
- MongoDB running locally, or a MongoDB connection string

## Local Setup

```bash
npm install
npm install --prefix todo-frontend
dotnet restore TodoBackend/TodoBackend.csproj
```

Start MongoDB, then run both apps:

```bash
npm run start
```

Default local URLs:

- Frontend: `http://localhost:5173`
- Backend GraphQL: `http://localhost:5288/graphql`
- LAN frontend: `http://<your-lan-ip>:5173`

Local Development seeds a demo family when `SeedDemoData=true`. Hosted Test and Production environments require sign-in and family membership.

## Branch And Environment Flow

- `develop` automatically deploys to the shared DigitalOcean test environment.
- Tested changes are merged into `main`.
- Production deployment from `main` remains manual until production launch.

## Useful Commands

```bash
npm run test:run --prefix todo-frontend
npm run build --prefix todo-frontend
dotnet build TodoBackend/TodoBackend.csproj --no-restore
dotnet test TodoBackend.Tests/TodoBackend.Tests.csproj --no-restore
```

## Documentation

- Development setup: `docs/DEVELOPMENT.md`
- DigitalOcean deployment: `docs/DEPLOYMENT_DIGITALOCEAN.md`
- Release checklist: `docs/RELEASE_CHECKLIST.md`
