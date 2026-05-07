# Development Setup

## Architecture

- `todo-frontend`: React 19 + Vite + Apollo Client.
- `TodoBackend`: .NET 8 + Hot Chocolate GraphQL + MongoDB EF Core provider.
- MongoDB database defaults to `TodoDatabase`.

## Environment Variables

Backend:

- `Mongo__ConnectionString`: defaults to `mongodb://localhost:27017`.
- `Mongo__DatabaseName`: defaults to `TodoDatabase`.
- `Cors__AllowedOrigins__0`: first allowed origin.
- `ASPNETCORE_ENVIRONMENT`: use `Development` locally.

Frontend:

- `VITE_GRAPHQL_URL`: optional. Defaults to `http://<current-host>:5288/graphql`.

## Run Locally

```bash
npm install
npm install --prefix todo-frontend
dotnet restore TodoBackend/TodoBackend.csproj
npm run start
```

Run services separately when debugging:

```bash
dotnet run --project TodoBackend/TodoBackend.csproj --urls http://0.0.0.0:5288
npm run dev --prefix todo-frontend -- --host 0.0.0.0
```

## LAN Testing

1. Start the backend on `0.0.0.0:5288`.
2. Start Vite with `--host 0.0.0.0`.
3. Open `http://<your-lan-ip>:5173` on another device.

The backend allows private LAN origins on ports `5173` and `5174` only in Development.

## Access Model

- Development without a token uses the seeded demo family.
- Production requires a signed-in user.
- Families are only returned to users with a `FamilyMembership`.
- Invite codes can be created from Settings and accepted from the access bar.

## Verification

```bash
npm run test:run --prefix todo-frontend
npm run build --prefix todo-frontend
dotnet build TodoBackend/TodoBackend.csproj --no-restore
```

## Troubleshooting

- `Failed to fetch`: confirm the backend is running and `VITE_GRAPHQL_URL` points to `/graphql`.
- GraphQL CORS errors: add the frontend origin to `Cors__AllowedOrigins`.
- Empty families in Production: sign in and create or join a family with an invite code.
