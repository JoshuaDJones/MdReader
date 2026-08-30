# Azure Test Environment Deployment Guide

Updated: August 29, 2026

## Goal

Deploy the current Lunoria application to a safe Azure testing environment using Azure Static Web Apps, Azure App Service, Azure SQL Database, Azure Blob Storage, and Application Insights.

This guide is based on the current repository state and the recommendations in `azure-deployment-cost-and-performance-plan.md`.

## Current repository findings

- `Eldoria.Api/appsettings.json` contains real-looking JWT and Azure Storage credentials in a tracked file.
- `Eldoria.Api/Program.cs` hardcodes CORS origins.
- Swagger is currently enabled in Production.
- The API does not currently expose a health endpoint or use a global production exception handler.
- The existing Static Web Apps workflow builds the older `Eldoria.Web` project instead of `Lunoria.Web`.
- The existing API workflow publishes from the repository root instead of explicitly publishing `Eldoria.Api`.
- `Lunoria.Web/staticwebapp.config.json` is missing.
- Azure CLI is not currently installed on the local development machine, so Azure Portal is the easiest initial setup route.
- Existing workflows reference a Static Web App and an App Service named `Lunoria-Api`, indicating that Azure resources may already exist.

Do not deploy the repository in its current state without first addressing the security and deployment-readiness items below.

## 1. Choose whether to reuse or isolate the test environment

If the existing Azure database contains important data, create a separate test environment. Suggested names are:

- Resource group: `rg-lunoria-test`
- API App Service: `lunoria-api-test-<unique-suffix>`
- Database: `LunoriaTest`
- Blob container: `lunoria-images-test`
- Static Web App: `lunoria-web-test`

If the existing Azure environment and its data are disposable, it can be reused to avoid paying for duplicate resources.

Use B1 App Service and Azure SQL Basic for functional testing. Use B2 and SQL S0 when testing representative performance, SignalR activity, or multiplayer gameplay.

## 2. Rotate and remove exposed credentials

Complete this before any deployment:

1. Open the existing Storage Account in Azure Portal.
2. Go to **Security + networking > Access keys**.
3. Regenerate the exposed storage access key.
4. Generate a new cryptographically random JWT signing key containing at least 32 UTF-8 bytes.
5. Remove both secrets from tracked `Eldoria.Api/appsettings.json`.
6. Store local-development values through .NET User Secrets or another untracked local configuration source.
7. Store testing values in App Service environment variables.

If either credential was previously committed and pushed to GitHub, treat it as compromised even if the repository is private. Rotation is mandatory; deleting the value from the latest commit is not sufficient.

The application currently authenticates to Blob Storage with a shared account key. Use the rotated key for the first test deployment. Longer term, replace shared-key authentication with an App Service managed identity.

Official reference: [Manage Azure Storage account access keys](https://learn.microsoft.com/en-us/azure/storage/common/storage-account-keys-manage)

## 3. Make the API production-safe

Make the following repository changes before deployment:

- Load allowed CORS origins from configuration instead of hardcoding them.
- Retain local frontend origins for Development.
- Add the testing Static Web Apps origin to test-environment configuration.
- Enable Swagger and Swagger UI only in Development.
- Add a global production exception handler that returns safe Problem Details responses.
- Add an anonymous `/health` endpoint.
- Remove or restrict unconditional request `Console.WriteLine` logging.
- Verify that Production never returns stack traces or internal exception details.

The CORS allowlist must contain the exact frontend origin without a trailing slash. SignalR requires WebSockets, credentials support, and a valid explicit frontend origin.

## 4. Add Static Web Apps routing configuration

Add `staticwebapp.config.json` with a navigation fallback that rewrites unknown client-side routes to `/index.html`.

Because the current frontend is built manually with Vite before deployment, the configuration file must end up inside `dist`. The simplest location is:

```text
Lunoria.Web/public/staticwebapp.config.json
```

Vite will copy files from `public` into `dist`. This fallback allows direct visits and browser refreshes on routes such as `/series/...` and `/join/...` without returning an Azure 404.

Official reference: [Configure Azure Static Web Apps](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration)

## 5. Correct the GitHub Actions workflows

### Frontend workflow

Update the existing Static Web Apps workflow to:

1. Use `Lunoria.Web` instead of `Eldoria.Web` as the working directory.
2. Run `npm ci`.
3. Provide `VITE_API_BASE_URL` before running the build.
4. Run `npm run build` from `Lunoria.Web`.
5. Deploy `Lunoria.Web/dist`.
6. When using `skip_app_build: true`, set `app_location` to `Lunoria.Web/dist` and leave `output_location` empty.

The API is hosted separately in App Service, so the Static Web Apps `api_location` should remain empty.

Official reference: [Azure Static Web Apps build configuration](https://learn.microsoft.com/en-us/azure/static-web-apps/build-configuration?tabs=github-actions)

### API workflow

Update the existing App Service workflow to:

1. Restore `Eldoria.Api/Eldoria.Api.csproj` explicitly.
2. Build `Eldoria.Api/Eldoria.Api.csproj` explicitly.
3. Publish `Eldoria.Api/Eldoria.Api.csproj` explicitly.
4. Upload only the API publish output as the deployment artifact.
5. Deploy that artifact to the testing App Service.
6. Retain the current OpenID Connect Azure login only after confirming that its Azure identity and role assignment are still valid.

Use `main` to deploy automatically to the test environment if that matches the development workflow. Add a separate manually approved production deployment later.

Official reference: [Deploy Azure App Service using GitHub Actions](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions?tabs=openid)

## 6. Create or configure Blob Storage

In Azure Portal:

1. Create or reuse a General Purpose v2 Storage Account.
2. Select Standard performance.
3. Select locally redundant storage (LRS).
4. Select the Hot access tier.
5. Place the account in East US 2 with the API and database.
6. Create a container such as `lunoria-images-test`.
7. Permit anonymous blob-level access for that container because the current application stores and displays public blob URLs.
8. Store the account name, rotated access key, and container name in App Service settings.

Do not put the storage key in source control or GitHub workflow files.

The current image-deletion policy intentionally retains blobs because playthrough snapshots share their URLs. Preserve that behavior unless the product policy changes.

## 7. Create or configure Azure SQL

In Azure Portal:

1. Create a logical Azure SQL Server or reuse an appropriate existing server.
2. Create a database named `LunoriaTest`.
3. Select Azure SQL Basic for functional testing or Standard S0 for representative performance.
4. Place the server and database in East US 2.
5. Configure a SQL administrator or Microsoft Entra administrator.
6. Configure firewall access for the controlled migration process and App Service.
7. Copy the ADO.NET connection string without exposing its password.
8. Store the connection string in App Service under the name `DefaultConnection` with type `SQLAzure`.

For the initial closed beta, allowing Azure services to reach Azure SQL is the simpler configuration. A managed-identity and private-network configuration can replace it during later hardening.

Official reference: [Deploy an ASP.NET Core app with Azure SQL](https://learn.microsoft.com/en-us/azure/app-service/tutorial-dotnetcore-sqldb-app)

## 8. Create or configure App Service

Create or configure:

- Linux App Service plan
- B1 for functional testing or B2 for performance testing
- One instance initially
- ASP.NET Core/.NET 8 runtime
- Application Insights enabled

Configure these platform settings:

- HTTPS Only: enabled
- WebSockets: enabled
- Always On: enabled
- Session affinity: enabled
- Minimum TLS version: at least TLS 1.2
- Health Check path: `/health`

Add these App Service environment variables:

| Setting | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Set to `Production`. |
| `Jwt__Key` | New production/test JWT signing key. |
| `Jwt__Issuer` | Testing JWT issuer. |
| `Jwt__Audience` | Testing JWT audience. |
| `AzureStorage__AccountName` | Blob Storage account name. |
| `AzureStorage__AccessKey` | Rotated Blob Storage key. |
| `AzureStorage__ContainerName` | Testing image container. |
| CORS origin settings | Exact Static Web Apps testing origin. |

Store `DefaultConnection` in the separate App Service **Connection strings** section rather than as a plain app setting.

Official reference: [Configure an Azure App Service app](https://learn.microsoft.com/en-us/azure/app-service/configure-common)

### Deployment slot limitation

Basic App Service plans do not support deployment slots. On B1 or B2, deploy to the default Production slot of the test App Service. The resource itself is the test environment even though Azure calls its default slot Production.

If staging-to-production slot swaps become necessary, move to Standard S1 or higher.

Official reference: [Azure App Service deployment slots](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/app-service-slots)

## 9. Apply database migrations separately

Do not automatically migrate the production or testing database every time the API starts.

Create an EF Core migration bundle during CI and execute it through a manually approved deployment step against `LunoriaTest`.

For the first test deployment:

1. Confirm the test database is disposable or has a restorable backup.
2. Generate the migration bundle from `Eldoria.Infrastructure` using `Eldoria.Api` as the startup project.
3. Store the migration connection string in a protected GitHub Environment secret.
4. Run the migration through a manually dispatched workflow.
5. Confirm the expected migrations appear in `__EFMigrationsHistory`.
6. Deploy the API only after migration succeeds.

Use a reviewed idempotent SQL script instead if SQL review is required before schema changes.

Official reference: [Applying EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)

## 10. Deploy and validate the API first

Run the API workflow manually for the first deployment.

Verify:

- App Service reports a successful deployment.
- `/health` returns a successful response.
- Application Insights receives requests.
- The API starts without missing-configuration errors.
- The Azure SQL connection succeeds.
- Blob upload configuration is valid.
- Swagger is unavailable in Production.
- Responses and logs do not expose secrets, stack traces, or internal exception details.

Do not deploy the frontend until the API URL is stable and the health check succeeds.

## 11. Configure and deploy the frontend

Create a GitHub Environment or repository variable containing:

```text
VITE_API_BASE_URL=https://<test-api-name>.azurewebsites.net/api/v1
```

This value is not normally secret, but it must exist in the GitHub Actions environment before `npm run build`. Vite embeds it into the generated frontend bundle at build time.

Deploy the frontend and record its exact Azure Static Web Apps origin. Add that origin to the API CORS configuration without a trailing slash, then restart or redeploy the API.

Verify direct navigation and browser refreshes on nested React routes.

## 12. Run the complete test pass

Test at minimum:

### Authentication and basic application behavior

- Registration
- Login
- Token expiration and unauthorized behavior
- Production errors do not expose internal details

### Series

- List series
- Create a valid series
- Reject a duplicate series name with `409 Conflict`
- Reject missing required values
- Update a series
- Reject a conflicting update name with `409 Conflict`
- Delete and cancel deletion
- Navigate to the correct Journeys page
- Verify the authenticated page at 1200px wide

### Content creation

- Image upload and display
- Journey creation and editing
- Scene creation and editing
- Character, spell, equipment, consumable, dialog, event, and chest configuration

### Playthrough

- Start a playthrough
- Open journey intro pages
- Start and resume scenes
- Activate and add participants
- Movement
- Combat
- Chest opening
- Potion use
- Item trading
- Forfeit action
- Scene ending

### Guest and SignalR

- Create a guest invitation
- Join from a real phone through the public URL
- Verify live event updates
- Verify reconnect behavior
- Rotate and revoke a guest invitation
- Verify completed playthrough behavior

### Reliability and usability

- Refresh nested routes directly
- Test API/network failure messages
- Verify form controls cannot submit duplicate requests
- Verify drawers and dialogs remain usable at common desktop heights
- Test multiple real devices simultaneously

### Performance

- Inspect API request duration
- Inspect SQL dependency duration
- Monitor App Service CPU and memory
- Monitor Azure SQL utilization
- Record SignalR behavior during a representative multiplayer session
- Scale only after identifying the constrained service

## 13. Configure cost protection

Create a monthly Azure budget scoped to the test resource group or subscription.

Configure alerts at:

- 50%
- 80%
- 100%

Also configure forecast and cost-anomaly alerts. Budget alerts notify users but do not automatically stop resources.

Review Cost Analysis after:

- The first day
- The first week
- The first complete billing month

Official references:

- [Create and manage Azure budgets](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets)
- [Monitor usage and spending with cost alerts](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/cost-mgt-alerts-monitor-usage-spending)

## 14. Plan the .NET runtime upgrade

The API currently targets .NET 8. As of August 29, 2026, .NET 8 is in maintenance and reaches end of support on November 10, 2026.

It is acceptable for the immediate testing deployment, but migrate the solution to .NET 10 before a longer-lived public launch. Keep the deployed runtime and NuGet security patches current while the migration is planned.

Official reference: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)

## Recommended execution order

1. Decide whether the existing Azure resources are disposable or create isolated test resources.
2. Rotate the exposed storage and JWT credentials.
3. Remove secrets from tracked configuration.
4. Add configurable CORS, production-safe Swagger behavior, global error handling, and `/health`.
5. Add `staticwebapp.config.json`.
6. Correct the frontend and API workflows.
7. Create or configure Storage, Azure SQL, App Service, Application Insights, and Static Web Apps.
8. Configure App Service secrets and connection strings.
9. Apply EF Core migrations through the controlled migration step.
10. Deploy and validate the API.
11. Build and deploy the frontend with the testing API URL.
12. Execute the complete functional, device, SignalR, and performance test pass.
13. Configure budgets and monitoring.
14. Plan the .NET 10 upgrade before public production use.

## Rollback expectations

Because B1 and B2 do not support deployment slots, application rollback means redeploying the last known-good Git commit or retained deployment artifact.

Database rollback must be handled separately:

- Prefer restoring the disposable test database or an Azure SQL backup.
- Do not automatically run EF Core `Down` migrations against important data.
- Review destructive migrations before applying them.
- Retain a record of the application commit associated with each applied database migration.

## Completion criteria

The test environment is ready when:

- No production/test secrets exist in tracked files.
- The API health endpoint succeeds.
- The database schema is current.
- The API and frontend deploy from corrected workflows.
- Deep React routes survive direct navigation and refresh.
- Image uploads work through Azure Blob Storage.
- Authentication and Series CRUD work correctly.
- The complete playthrough loop works.
- A real phone can join and receive SignalR updates.
- Application Insights records useful telemetry without exposing secrets.
- Budget alerts are active.
- A repeatable rollback procedure has been recorded and tested.
