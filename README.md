# PulsePost

AI-powered article pipeline — monitors trending AI topics, generates articles in your voice, seeks approval via Telegram, and publishes to your blog automatically.

## How It Works

1. **Trigger** — runs every Sunday 8am AEST (or manually via `workflow_dispatch`)
2. **Fetch** — pulls trending AI topics from Hacker News, GitHub, and arXiv
3. **Generate** — Azure OpenAI selects the best topic and writes a full article + X thread + image prompt
4. **Notify** — sends draft preview to Telegram with a link to review
5. **Approve** — comment `APPROVE` / `REJECT` / `EDIT <feedback>` on the GitHub Issue
6. **Publish** — on approval, opens a PR to the blog repo and sends X thread via Telegram

## Secrets Required

| Secret | Description |
|---|---|
| `GH_PAT` | GitHub Personal Access Token (repo scope) |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI instance URL |
| `AZURE_OPENAI_KEY` | Azure OpenAI API key |
| `AZURE_OPENAI_DEPLOYMENT` | GPT-4o deployment name |
| `TELEGRAM_BOT_TOKEN` | Telegram bot token |
| `TELEGRAM_CHAT_ID` | Your Telegram chat ID |

## Setup

1. Fork / create this repo on GitHub
2. Add all secrets under `Settings → Secrets → Actions`
3. Create labels `pending-approval`, `approved`, `rejected` in the repo
4. Enable Issues in repo settings
5. Trigger manually via `Actions → Generate Article → Run workflow` to test

## Approval Commands

Comment on the generated GitHub Issue:

| Command | Action |
|---|---|
| `APPROVE` | Creates blog PR + sends X thread to Telegram |
| `REJECT` | Closes issue, discards draft |
| `EDIT make it more technical` | Rewrites article with your feedback, re-notifies |

## Project Structure

```
PulsePost/
├── scripts/
│   ├── fetch_topics.py       # HN + GitHub + arXiv fetcher
│   ├── generate_article.py   # Azure OpenAI article generator
│   ├── create_issue.py       # GitHub Issue + Telegram notifier
│   ├── handle_approval.py    # Approve / reject / edit handler
│   └── requirements.txt
├── .github/workflows/
│   ├── generate.yml          # Scheduled + manual trigger
│   └── handle-approval.yml   # Issue comment handler
├── terraform/                # Phase 2 — Azure infrastructure
└── REQUIREMENTS.md
```

## Phase 2

Phase 2 migrates the pipeline to Azure Functions + Service Bus with full Terraform provisioning. See `REQUIREMENTS.md` for the full architecture.
