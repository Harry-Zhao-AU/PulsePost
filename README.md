# PulsePost

AI-powered article pipeline — monitors trending AI topics, generates articles in your voice, seeks approval via Telegram, and publishes to your blog automatically.

## Full Flow

```
Every Sunday 8am AEST (or /generate via Telegram)
        │
        ▼
generate.yml (GitHub Actions)
  ├── fetch_topics.py    → scrapes HN, GitHub trending, arXiv
  ├── generate_article.py → Azure OpenAI selects topic, writes article + X thread + image prompt
  └── create_issue.py    → creates GitHub Issue + sends Telegram preview
        │
        ▼
You receive Telegram message with draft preview
        │
        ├── Reply: /generate        → triggers full pipeline manually
        ├── Reply: APPROVE          → publishes article
        ├── Reply: REJECT           → discards draft
        └── Reply: EDIT <feedback>  → rewrites and re-sends
        │
        ▼ (APPROVE)
TelegramWebhook (Azure Function)
  └── sends ServiceBus message → post-article queue
        │
        ▼
PostArticle (Azure Function)
  └── opens PR to blog repo + sends X thread via Telegram
```

## Architecture

```
GitHub Actions (scheduled/manual)
      │
      ▼
Python scripts → Azure OpenAI → GitHub Issue + Telegram

Telegram Bot
      │ (webhook)
      ▼
Azure Function App (pulsepost-func)
  ├── TelegramWebhook  — receives Telegram commands
  ├── FetchTopics      — Service Bus triggered, fetches topics
  ├── GenerateArticle  — Service Bus triggered, generates article
  └── PostArticle      — Service Bus triggered, publishes to blog

Azure Infrastructure
  ├── Service Bus (pulsepost-bus)  — 3 queues: fetch-topics, generate-article, post-article
  ├── Storage Account (pulsepostsa) — ArticleDrafts table
  ├── Function App (pulsepost-func) — .NET 8 isolated, Consumption (Y1)
  └── Application Insights (pulsepost-ai)
```

## CI/CD

| Workflow | Trigger | Purpose |
|---|---|---|
| `generate.yml` | Sunday 8am AEST + manual | Runs article generation pipeline |
| `handle-approval.yml` | GitHub Issue comment | Handles approve/reject/edit |
| `deploy.yml` | Push to `src/**` | Deploys Function App code |
| `infra.yml` | Push to `bicep/**` | Deploys Azure infrastructure |

## Secrets Required

| Secret | Description |
|---|---|
| `GH_PAT` | GitHub Personal Access Token (repo scope) |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI instance URL |
| `AZURE_OPENAI_KEY` | Azure OpenAI API key |
| `AZURE_OPENAI_DEPLOYMENT` | GPT-4o deployment name (default: `gpt-4o`) |
| `TELEGRAM_BOT_TOKEN` | Telegram bot token from @BotFather |
| `TELEGRAM_CHAT_ID` | Your Telegram chat ID |
| `AZURE_PUBLISH_PROFILE` | Publish profile from `pulsepost-func` Azure Portal |

## Setup

1. Deploy Azure infrastructure via Cloud Shell:
   ```bash
   az group create --name pulsepost-rg --location australiaeast
   az deployment group create --resource-group pulsepost-rg --template-file bicep/main.bicep --parameters @parameters.json
   ```
2. Add all secrets under `Settings → Secrets → Actions`
3. Download publish profile from Azure Portal → `pulsepost-func` → Get publish profile → add as `AZURE_PUBLISH_PROFILE`
4. Register Telegram webhook:
   ```bash
   curl -s "https://api.telegram.org/bot<TOKEN>/setWebhook" -d "url=https://pulsepost-func.azurewebsites.net/api/TelegramWebhook"
   ```
5. Send `/generate` to your Telegram bot to test

## Telegram Commands

| Command | Action |
|---|---|
| `/generate` | Start pipeline — fetch topics → generate article → send draft |
| `APPROVE` | Publish latest pending draft |
| `REJECT` | Discard latest pending draft |
| `EDIT <feedback>` | Rewrite with feedback, e.g. `EDIT make it more technical` |

## Project Structure

```
PulsePost/
├── src/PulsePost.Functions/   # Azure Functions (.NET 8 isolated)
│   ├── Functions/             # TelegramWebhook, FetchTopics, GenerateArticle, PostArticle, Scheduler
│   ├── Services/              # OpenAI, Telegram, TopicFetch, DraftStorage, Publish
│   └── host.json
├── scripts/                   # Python pipeline (GitHub Actions)
│   ├── fetch_topics.py
│   ├── generate_article.py
│   ├── create_issue.py
│   └── handle_approval.py
├── bicep/                     # Azure infrastructure (Bicep)
│   └── main.bicep
├── .github/workflows/
│   ├── generate.yml
│   ├── handle-approval.yml
│   ├── deploy.yml
│   └── infra.yml
└── terraform/                 # Legacy — replaced by Bicep
```
