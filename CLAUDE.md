# PulsePost — Claude Context

## What This Project Does

AI-powered article pipeline. Fetches trending AI topics, generates articles in Harry's voice via Azure OpenAI, sends drafts to Telegram for approval, then publishes to the blog repo.

## Tech Stack

- **Azure Functions** (.NET 8 isolated, Consumption Y1) — `src/PulsePost.Functions/`
- **Azure Service Bus** (Basic, 3 queues) — async pipeline between functions
- **Azure Table Storage** — draft persistence (`ArticleDrafts` table)
- **Azure OpenAI** (GPT-4o) — topic selection, article generation, X thread, image prompt
- **Python scripts** — GitHub Actions pipeline (fetch, generate, notify)
- **Bicep** — infrastructure as code (`bicep/main.bicep`)

## Key Commands

```bash
# Build
dotnet publish src/PulsePost.Functions/PulsePost.Functions.csproj -c Release -o publish/

# Deploy infra (from Cloud Shell or hotspot — corporate firewall blocks management.azure.com)
az group create --name pulsepost-rg --location australiaeast
az deployment group create --resource-group pulsepost-rg --template-file bicep/main.bicep --parameters @bicep/parameters.json
```

## Known Issues / Constraints

- **Corporate endpoint security** blocks `management.azure.com` — all Azure CLI/Terraform infra commands must run from hotspot or Azure Cloud Shell
- **`bicep/parameters.json`** is gitignored — contains real secrets, never commit
- **Terraform** is legacy — replaced by Bicep due to firewall blocking HashiCorp registry downloads
- **Service Bus SKU is Basic** — Basic tier does not support topics, only queues. Do not add topic-based routing.
- **`host.json` must be in csproj** as `CopyToOutputDirectory` — otherwise Azure Functions won't register

## CI/CD

| Workflow | Trigger | Runner |
|---|---|---|
| `deploy.yml` | `src/**` push | `ubuntu-latest` |
| `infra.yml` | `bicep/**` push | `self-hosted` (uses local az login) |
| `generate.yml` | Sunday 8am AEST | `ubuntu-latest` |
| `handle-approval.yml` | Issue comment | `ubuntu-latest` |

## Azure Resources

All in resource group `pulsepost-rg`, region `australiaeast`:

- `pulsepostsa` — Storage Account
- `pulsepost-law` — Log Analytics Workspace
- `pulsepost-ai` — Application Insights
- `pulsepost-bus` — Service Bus Namespace
- `pulsepost-plan` — App Service Plan (Y1 Consumption)
- `pulsepost-func` — Function App (.NET 8 isolated)

## Telegram Commands

- `/generate` — triggers full pipeline
- `APPROVE` — publishes latest pending draft
- `REJECT` — discards draft
- `EDIT <feedback>` — rewrites with feedback

## Package Versions (important)

Packages are pinned to 1.x series for .NET 8 compatibility. The 2.x series requires .NET 9/10:
- `Microsoft.Azure.Functions.Worker` 1.22.0
- `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` 1.3.2
- `Microsoft.Azure.Functions.Worker.ApplicationInsights` 1.2.0
- `Microsoft.ApplicationInsights.WorkerService` 2.22.0 (explicit — not brought in transitively)
