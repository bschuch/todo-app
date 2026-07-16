# Release Checklist

## Before Test Deployment

- [ ] Frontend tests pass.
- [ ] Frontend production build passes.
- [ ] Backend Release build passes.
- [ ] New configuration values are present in `.do/app-test.yaml`.
- [ ] Database/index changes are backward-compatible with current records.
- [ ] No secret, connection string, token, or real family data is committed.

## Shared Test Verification

- [ ] App Platform reports `/health/ready` healthy over HTTPS.
- [ ] The UI displays the `Test` environment badge.
- [ ] A signed-out browser cannot access family data.
- [ ] Sign-up, sign-in, sign-out, and session-expiration behavior work.
- [ ] An owner can create an invite and a separate account can join.
- [ ] The invited account cannot access another family's identifiers.
- [ ] Calendar and chore changes survive an API redeploy.
- [ ] Application logs contain no authorization tokens, passwords, invite codes, notes, or GraphQL request bodies.
- [ ] The previous App Platform deployment can be selected for rollback.

## Promotion To Main

- [ ] Record the tested commit SHA and deployment URL.
- [ ] Confirm a recent managed MongoDB backup exists.
- [ ] Merge the exact tested changes from `develop` into `main`.
- [ ] Keep production automatic deployment disabled.

## Future Production Launch

- [ ] Create a production-only MongoDB database and verify backup retention.
- [ ] Create the app from `.do/app-production.yaml` with encrypted secrets.
- [ ] Verify production CORS permits only the production frontend.
- [ ] Run the shared test verification against production with new accounts and non-test data.
- [ ] Complete a restore drill into a temporary database.
