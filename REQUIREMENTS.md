# PulsePost

> AI-powered article pipeline that monitors trending AI topics, generates articles in your voice, seeks your approval via Telegram, and publishes to your blog and X automatically.

---

## Background

Harry Zhao is a Senior Software Engineer specialising in AI-augmented systems, distributed platforms, and cloud-native services. He maintains a technical blog at harry-zhao-au.github.io and an X account for professional content sharing.

Writing and publishing high-quality technical articles consistently is time-consuming. PulsePost automates the research, writing, and publishing pipeline while keeping Harry in full control via a human-in-the-loop approval step.

---

## Goals

- Automatically surface trending AI topics from authoritative sources weekly
- Generate a full blog article and X thread in Harry's voice and style
- Seek approval via Telegram before any content is published
- Support edit feedback loop — rewrite based on Harry's instructions
- Publish approved content to the blog (via GitHub PR) and X (via Buffer)
- Provide full observability across the pipeline
- Demonstrate real-world Azure cloud architecture for portfolio purposes

---

## Non-Goals

- Not a general-purpose content platform — single user (Harry) only
- Not fully autonomous — always requires human approval before publishing
- Not a social media manager — X posting is limited to approved articles only
- No frontend UI — Telegram is the sole interaction interface

---

## Functional Requirements

### FR-01: Scheduled Trigger
- Pipeline runs automatically every Sunday at 8:00am AEST
- Trigger is idempotent — duplicate triggers within the same day are ignored

### FR-02: Manual Trigger
- User sends `/generate` to the Telegram bot
- Bot kicks off the same pipeline on demand

### FR-03: Topic Discovery
The system fetches trending AI content from three free sources:
- **Hacker News API** — top stories filtered by AI/LLM/agent keywords, ranked by score
- **GitHub Trending API** — top AI/LLM repositories trending this week
- **arXiv RSS** — latest papers from cs.AI feed

Existing blog post titles are fetched from the repository to avoid repeating topics.

### FR-04: Topic Selection
- Azure OpenAI (GPT-4o) analyses all fetched data
- Selects the single most relevant and timely topic for a senior engineering audience
- Returns: topic title, 3 key angles, relevance reasoning

### FR-05: Article Generation
Azure OpenAI generates three outputs from the selected topic:
1. **Full blog article** — Markdown format, 800–1200 words, Harry's tone and style
2. **X thread** — 6–8 tweets, max 280 characters each, ending with blog link
3. **Image prompt** — DALL-E prompt for a cover image matching the article

Harry's existing blog posts are included as style reference.

### FR-06: Draft Storage
- Generated draft is saved to Azure Table Storage
- Fields: article, x_thread, image_prompt, topic, sources, status, created_at
- RowKey is a UTC timestamp (`yyyyMMddHHmmss`) — each draft is a separate row
- Drafts older than 7 days are automatically purged on each pipeline run
- Latest draft is identified by querying the most recent RowKey with status `pending`

### FR-07: Telegram Notification
After generation, the bot sends Harry:
- Topic title and sources used
- Preview of the X thread (first 2 tweets)
- Link to full draft (GitHub Issue for Phase 1, inline for Phase 2)
- Image prompt
- Instructions: reply `APPROVE` / `REJECT` / `EDIT <feedback>`

### FR-08: Approval Handling
The system handles three response types:

**APPROVE**
- Sends X thread to Harry via Telegram for manual posting
- Opens a pull request to harry-zhao-au.github.io with the full article
- Sends Telegram confirmation with PR link
- Marks draft status as `approved`

**REJECT**
- Discards the draft
- Sends Telegram confirmation
- Marks draft status as `rejected`

**EDIT <feedback>**
- Passes original draft + Harry's feedback to Azure OpenAI
- Regenerates article and X thread
- Updates draft in Table Storage
- Sends revised draft to Telegram for re-review
- Loops back to FR-07 (max 3 edit iterations)

### FR-09: Blog PR
- PR is created against harry-zhao-au.github.io repository
- Includes article file in `_posts/YYYY-MM-DD-slug.md`
- Includes image placeholder with the generated image prompt in PR description
- Harry manually generates image, uploads, and merges the PR

### FR-10: Observability
Every pipeline step logs to Application Insights:
- Trigger source (scheduled / manual)
- Topics fetched (count per source)
- Topic selected
- Generation duration and token usage
- Approval action (approve / reject / edit)
- X post result
- PR creation result
- Any errors or retries

---

## Non-Functional Requirements

### NFR-01: Cost
- Total Azure cost must remain under $5 AUD/month
- Azure OpenAI cost must remain under $1 AUD/month

### NFR-02: Reliability
- Each pipeline step retries up to 3 times on failure
- Failed steps notify Harry via Telegram with error details

### NFR-03: Security
- No secrets hardcoded — all credentials stored in GitHub Actions secrets (Phase 1) or Azure Key Vault (Phase 2)
- `local.settings.json` excluded from version control
- Terraform state stored in Azure Blob Storage backend (Phase 2)

### NFR-04: Latency
- Full pipeline (trigger → Telegram notification) completes within 3 minutes

### NFR-05: Maintainability
- Infrastructure provisioned entirely via Terraform (Phase 2)
- CI/CD via GitHub Actions for all deployments
- All Functions unit tested with xUnit

---

## Solution Architecture

### Phase 1 — GitHub Actions (MVP)

```
Trigger: GitHub Actions cron / workflow_dispatch (/generate via Telegram)
    ↓
Step 1: Fetch Topics (Python script)
  → Hacker News API
  → GitHub Trending API
  → arXiv RSS
  → Existing blog posts (repo files)
    ↓
Step 2: Select Topic + Generate Article (Azure OpenAI GPT-4o)
  → Full blog article (Markdown)
  → X thread (6–8 tweets)
  → Image prompt (DALL-E)
    ↓
Step 3: Create GitHub Issue (draft storage + approval gate)
    ↓
Step 4: Telegram Notification (bot sends draft preview + issue link)
    ↓
[Harry reviews issue, comments APPROVE / REJECT / EDIT]
    ↓
Step 5: GitHub Actions on issue_comment event
  APPROVE → Buffer API (X post) + GitHub PR (blog)
  REJECT  → close issue
  EDIT    → Azure OpenAI rewrite → update issue → re-notify
```

**Phase 1 Tech Stack:**
- GitHub Actions (orchestration + CI/CD)
- Python (scripting)
- Azure OpenAI API (GPT-4o)
- GitHub Issues (draft storage + approval gate)
- Telegram Bot API (notifications)
- X thread delivered via Telegram for manual posting

---

### Phase 2 — Azure (Production)

```
Trigger: Azure Functions Timer Trigger (Sunday 8am AEST)
      OR: Telegram /generate → HTTP Trigger
    ↓
Service Bus Queue: "fetch-topics"
    ↓
Function: FetchTopics
  → HN + GitHub + arXiv + blog posts
  → publishes to Service Bus: "generate-article"
    ↓
Function: GenerateArticle
  → Azure OpenAI GPT-4o
  → saves draft to Azure Table Storage
  → sends Telegram notification
    ↓
[Harry replies in Telegram: APPROVE / REJECT / EDIT]
    ↓
Telegram pushes reply to:
Function: TelegramWebhook (HTTP Trigger)
  → reads draft from Table Storage
  APPROVE → publishes to Service Bus: "post-article"
  REJECT  → marks draft rejected, Telegram confirm
  EDIT    → Azure OpenAI rewrite → Table Storage → re-notify
    ↓
Function: PostArticle
  → Buffer API (X thread)
  → GitHub API (blog PR)
  → Telegram confirmation
    ↓
Application Insights (all steps)
```

**Phase 2 Tech Stack:**
- Azure Functions (C#, .NET 10, isolated worker)
- Azure Service Bus Basic (queue-based decoupling)
- Azure Table Storage (draft persistence)
- Azure OpenAI (GPT-4o deployment)
- Application Insights (observability)
- Terraform (IaC — all Azure resources)
- GitHub Actions (CI/CD — deploy to Azure)
- Telegram Bot API (human-in-the-loop)
- X thread delivered via Telegram for manual posting

---

## Azure Resources (Phase 2)

| Resource | SKU | Est. Monthly Cost |
|---|---|---|
| Resource Group | — | $0 |
| Storage Account (LRS) | Standard | ~$0.02 |
| Azure Functions | Consumption (Y1) | ~$0 |
| Service Bus Namespace | Basic | ~$0.05 |
| Application Insights | Pay-as-you-go | ~$0 |
| Azure OpenAI (GPT-4o) | S0 | ~$0.20 |
| **Total** | | **~$0.27/month** |

---

## Azure Functions (Phase 2)

| Function | Trigger | Responsibility |
|---|---|---|
| `Scheduler` | TimerTrigger `0 22 * * 0` | Initiates pipeline weekly |
| `TelegramWebhook` | HttpTrigger | Receives all Telegram messages |
| `FetchTopics` | ServiceBusTrigger | Fetches trending topics from 3 sources |
| `GenerateArticle` | ServiceBusTrigger | Calls Azure OpenAI, stores draft, notifies |
| `PostArticle` | ServiceBusTrigger | Posts to Buffer + creates blog PR |

---

## Repository Structure

```
PulsePost/
├── src/
│   └── PulsePost.Functions/
│       ├── Functions/
│       │   ├── Scheduler.cs
│       │   ├── TelegramWebhook.cs
│       │   ├── FetchTopics.cs
│       │   ├── GenerateArticle.cs
│       │   └── PostArticle.cs
│       ├── Services/
│       │   ├── IAzureOpenAIService.cs
│       │   ├── ITelegramService.cs
│       │   ├── ITopicFetchService.cs
│       │   └── IPublishService.cs
│       ├── Models/
│       │   ├── ArticleDraft.cs
│       │   └── TelegramMessage.cs
│       ├── host.json
│       └── local.settings.json       ← gitignored
├── tests/
│   └── PulsePost.Tests/
│       ├── FetchTopicsTests.cs
│       ├── GenerateArticleTests.cs
│       └── TelegramWebhookTests.cs
├── terraform/
│   ├── main.tf
│   ├── functions.tf
│   ├── servicebus.tf
│   ├── openai.tf
│   ├── variables.tf
│   └── outputs.tf
├── .github/
│   └── workflows/
│       ├── generate.yml              ← Phase 1: full pipeline
│       ├── handle-approval.yml       ← Phase 1: issue comment handler
│       └── deploy.yml                ← Phase 2: deploy to Azure
├── scripts/                          ← Phase 1 Python scripts
│   ├── fetch_topics.py
│   ├── generate_article.py
│   └── post_article.py
├── .gitignore
└── README.md
```

---

## Milestones

### Milestone 1 — Phase 1 MVP (GitHub Actions)
- [ ] Telegram bot setup
- [ ] Topic fetching scripts (HN + GitHub + arXiv)
- [ ] Azure OpenAI integration (topic selection + article generation)
- [ ] GitHub Actions workflow (generate + approval)
- [ ] Buffer API integration (X posting)
- [ ] Blog PR automation

### Milestone 2 — Phase 2 Azure Migration
- [ ] Terraform — provision all Azure resources
- [ ] C# Function App — all 5 functions
- [ ] Service Bus integration
- [ ] Table Storage draft persistence
- [ ] Telegram webhook (HTTP trigger)
- [ ] Application Insights telemetry
- [ ] GitHub Actions CI/CD deploy pipeline
- [ ] xUnit tests

---

## Secrets Reference

| Secret | Used In | Description |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | Phase 1 + 2 | Azure OpenAI instance URL |
| `AZURE_OPENAI_KEY` | Phase 1 + 2 | Azure OpenAI API key |
| `TELEGRAM_BOT_TOKEN` | Phase 1 + 2 | Telegram bot token |
| `TELEGRAM_CHAT_ID` | Phase 1 + 2 | Harry's Telegram chat ID |
| `GITHUB_TOKEN` | Phase 1 + 2 | GitHub API token for blog PR |
| `AZURE_CREDENTIALS` | Phase 2 | Azure service principal for Terraform |
