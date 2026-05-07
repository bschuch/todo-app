# DigitalOcean App Platform Deployment

This repo is set up for DigitalOcean App Platform using a static frontend, a .NET backend service, and a managed MongoDB database.

## Recommended Components

- Static site: `todo-frontend`
- Web service: `TodoBackend`
- Managed database: MongoDB

## Required Environment Variables

Backend runtime:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Mongo__ConnectionString=<managed-mongodb-connection-string>`
- `Mongo__DatabaseName=TodoDatabase`
- `Cors__AllowedOrigins__0=https://<your-app-domain>`

Frontend build time:

- `VITE_GRAPHQL_URL=https://<your-backend-domain>/graphql`

## Deploy Steps

1. Push the repo to GitHub.
2. In DigitalOcean, create an App Platform app from `bschuch/todo-app`.
3. Add the static frontend from `todo-frontend`.
4. Add the backend service from `TodoBackend`.
5. Add or attach a managed MongoDB database.
6. Set the environment variables above as encrypted values where appropriate.
7. Deploy and open the frontend URL.
8. Create the first account and family, then invite additional family members.

## Production Checks

- Frontend loads over HTTPS.
- Backend GraphQL responds over HTTPS.
- Sign-up and sign-in work.
- A user only sees families they created or joined.
- Invite codes join the expected family.
- MongoDB data persists after redeploy.

## Notes

- Development's no-token demo fallback only runs when `ASPNETCORE_ENVIRONMENT=Development`.
- The backend respects DigitalOcean's `PORT` environment variable.
- Keep GraphQL exception details disabled in Production.
