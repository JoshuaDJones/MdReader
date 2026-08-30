# Azure Deployment, Cost, and Performance Plan

Updated: August 23, 2026

## Goal

Deploy Lunoria to Azure with production-capable paid services while keeping the initial closed-beta cost reasonable and the application as responsive as practical compared with local development.

Local execution has nearly zero network latency, so a cloud deployment cannot match every local request exactly. The target is for normal navigation and playthrough actions to feel comparably responsive after allowing for roughly 30-100 ms of internet latency for players near the selected Azure region.

## Recommended architecture

```text
Players
  -> Azure Static Web Apps (React/Vite frontend)
  -> Azure App Service (ASP.NET Core API and SignalR hubs)
  -> Azure SQL Database (EF Core gameplay data)
  -> Azure Blob Storage (uploaded images)
```

Use the same Azure region for App Service, Azure SQL, and Blob Storage. East US 2 is the initial recommendation for players primarily located in the eastern United States. Static Web Apps distributes the frontend separately.

## Recommended fast closed-beta configuration

| Service | Recommended tier | Estimated monthly cost |
|---|---|---:|
| React frontend | Azure Static Web Apps Standard | About $9 |
| .NET API | Linux App Service B2 | $25.55 |
| Database | Azure SQL Standard S0, provisioned DTU | About $15 |
| Images | Blob Storage Hot tier with LRS | About $1-$3 initially |
| Monitoring and bandwidth | Application Insights and normal light usage | About $0-$5 initially |
| **Expected total** | | **About $50-$60 per month** |

Prices vary by region, agreement, usage, and taxes. Verify the final estimate in the Azure Pricing Calculator before creating the resources.

The B2 API plan provides two cores and 3.5 GB of memory. S0 provides more database capacity and storage than the Basic 5-DTU database. This pairing is the preferred starting point when responsiveness matters more than reaching the absolute lowest monthly price.

## Lower-cost alternative

A lower-cost paid configuration is expected to cost about $30-$40 per month:

- Static Web Apps Standard.
- Linux App Service B1 at approximately $13.14 per month.
- Azure SQL Basic at approximately $5 per month.
- The same Blob Storage and monitoring configuration.

Azure SQL Basic is limited to 5 DTUs and 2 GB. Images are stored in Blob Storage, so the database can remain small, but Basic is more likely than S0 to become a gameplay bottleneck. Start with B2/S0 when local-like responsiveness is the priority.

## Required Azure configuration

### App Service

- Publish the `Eldoria.Api/Eldoria.Api.csproj` project.
- Use Linux and the .NET 8 runtime.
- Enable HTTPS Only.
- Enable WebSockets.
- Enable Always On.
- Keep session affinity enabled while SignalR is hosted directly by the API.
- Run one API instance initially.
- Do not add Azure SignalR Service until multiple API instances or substantially heavier connection volume require it.

Configure these application settings:

| Setting | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Set to `Production`. |
| `Jwt__Key` | A new random production signing key containing at least 32 UTF-8 bytes. |
| `Jwt__Issuer` | Production JWT issuer. |
| `Jwt__Audience` | Production JWT audience. |
| `AzureStorage__AccountName` | Blob Storage account name. |
| `AzureStorage__AccessKey` | Blob Storage account access key. |
| `AzureStorage__ContainerName` | Image container name. |

Add the Azure SQL connection under App Service connection strings with the name `DefaultConnection` and the Azure SQL type. Never commit production secrets to the repository.

### Azure SQL

- Use a single provisioned S0 database initially.
- Place it in the same region as App Service.
- Apply EF Core migrations through a controlled deployment migration bundle or explicit release step.
- Do not automatically migrate the production database every time the API starts.
- Configure short-term backups and test a restore before public launch.
- Scale the DTU tier only after measuring database utilization and query duration.

### Static Web Apps

Use these monorepo build settings:

| Setting | Value |
|---|---|
| App location | `Lunoria.Web` |
| API location | Empty |
| Output location | `dist` |
| Build command | `npm run build` |

The frontend build must receive:

```text
VITE_API_BASE_URL=https://<api-app-name>.azurewebsites.net/api/v1
```

This is a Vite build-time value and must be present in the GitHub Actions build environment.

Add `Lunoria.Web/staticwebapp.config.json` with a navigation fallback to `/index.html` before deployment so direct visits and refreshes on React routes do not return an Azure 404.

### CORS

The API currently has frontend origins hardcoded in `Eldoria.Api/Program.cs`, including:

```text
https://ambitious-mud-06f2ad40f.6.azurestaticapps.net
```

Confirm that this is the deployed frontend. Move the production allowlist to configuration before adding a different Static Web Apps address or custom domain.

## Performance plan

1. Keep API, SQL, and Blob Storage in the same Azure region.
2. Keep App Service Always On so the API remains loaded.
3. Use provisioned SQL rather than an auto-pausing serverless database.
4. Serve the React build through Static Web Apps.
5. Compress uploaded images and set appropriate browser cache headers for images, sounds, and hashed build assets.
6. Split the current large JavaScript bundle with route and feature lazy loading.
7. Avoid sequential API requests when one server response can provide the required playthrough state.
8. Inspect EF Core queries and indexes for slow scene and playthrough snapshot loads.
9. Enable Application Insights and monitor request duration, dependency duration, CPU, memory, failures, and SQL utilization.
10. Test with multiple real devices through the public Azure addresses before increasing tiers.

Do not jump directly to Premium App Service, Azure Front Door, private endpoints, or a separate Azure SignalR Service. First identify whether the API, database, network, or frontend bundle is responsible for any measured delay.

## Cost controls

- Create a resource-group or subscription budget of $60 per month.
- Configure alerts at 50%, 80%, and 100% of the budget.
- Also enable forecast and cost-anomaly alerts.
- Remember that a budget alert sends notifications but does not automatically stop resources.
- Review Cost Analysis after the first day, first week, and first complete billing month.
- Avoid Windows App Service because its comparable Basic tier is substantially more expensive than Linux.

## Deployment order

1. Make production-safety changes: configurable CORS, production-only Swagger behavior, global error handling, and a health endpoint.
2. Add frontend and API GitHub Actions workflows.
3. Create one resource group and the Storage, SQL, App Service, Application Insights, and Static Web Apps resources.
4. Configure secrets and connection strings in Azure.
5. Apply the database migrations through the controlled release step.
6. Deploy and validate the API.
7. Build the frontend with the production API URL and deploy it.
8. Verify registration, login, image upload, journey creation, playthrough start, guest joining, SignalR reconnect, combat, chest opening, potion use, trading, and scene ending.
9. Record response-time and resource metrics during a representative multiplayer session.
10. Scale only the constrained service.

## Official references

- [Azure App Service for Linux pricing](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/)
- [Azure Static Web Apps plans](https://learn.microsoft.com/en-us/azure/static-web-apps/plans)
- [Azure Static Web Apps configuration](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration)
- [Azure SQL Database pricing](https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single/)
- [Azure SQL DTU resource limits](https://learn.microsoft.com/en-us/azure/azure-sql/database/resource-limits-dtu-single-databases)
- [Publish ASP.NET Core SignalR to App Service](https://learn.microsoft.com/aspnet/core/signalr/publish-to-azure-web-app)
- [App Service performance diagnostics](https://learn.microsoft.com/en-us/troubleshoot/azure/app-service/troubleshoot-performance-degradation)
- [Azure Cost Management budgets](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets)
