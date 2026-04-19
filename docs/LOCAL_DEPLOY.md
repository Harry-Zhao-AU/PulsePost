# Local Terraform Deployment Guide

Deploy Azure infrastructure locally using your own Azure credentials — no Service Principal required.

---

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [Terraform](https://developer.hashicorp.com/terraform/install) installed
- An active Azure subscription

---

## Step 1 — Install Tools

```bash
winget install Microsoft.AzureCLI
winget install Hashicorp.Terraform
```

Verify:
```bash
az --version
terraform --version
```

---

## Step 2 — Login to Azure

```bash
az login
```

Browser opens → sign in → done.

Set your subscription:
```bash
az account set --subscription "YOUR_SUBSCRIPTION_ID"
```

Find your subscription ID:
```bash
az account show --query id -o tsv
```

---

## Step 3 — Create tfvars File

Create `terraform/terraform.tfvars` with your secrets:

```hcl
telegram_bot_token      = "your-telegram-bot-token"
telegram_chat_id        = "your-telegram-chat-id"
github_pat              = "your-github-pat"
azure_openai_endpoint   = "https://your-instance.openai.azure.com/"
azure_openai_key        = "your-openai-api-key"
```

> ⚠️ This file contains secrets — never commit it. It is already in `.gitignore`.

---

## Step 4 — Initialise Terraform

```bash
cd terraform
terraform init
```

---

## Step 5 — Preview Changes

```bash
terraform plan
```

Review what will be created:
- Resource Group
- Storage Account + Table Storage
- Application Insights + Log Analytics Workspace
- Service Bus Namespace + 3 queues
- Function App (Consumption, .NET 10)

---

## Step 6 — Apply

```bash
terraform apply
```

Type `yes` when prompted. Takes 3–5 minutes.

---

## Step 7 — Check Outputs

```bash
terraform output
```

Expected output:
```
function_app_name    = "pulsepost-func"
function_app_url     = "https://pulsepost-func.azurewebsites.net"
telegram_webhook_url = "https://pulsepost-func.azurewebsites.net/api/TelegramWebhook"
```

---

## Step 8 — Deploy Function App Code

Download the publish profile from Azure Portal:
1. Go to `portal.azure.com`
2. Open `pulsepost-func` Function App
3. Click **Get publish profile** → download the file
4. Add contents as `AZURE_PUBLISH_PROFILE` secret in GitHub

Then push any change to `main` to trigger the GitHub Actions deploy workflow.

---

## Step 9 — Register Telegram Webhook

After deployment, register your Function App URL with Telegram:

```bash
curl -s "https://api.telegram.org/bot<YOUR_BOT_TOKEN>/setWebhook" \
  -d "url=https://pulsepost-func.azurewebsites.net/api/TelegramWebhook"
```

Expected response:
```json
{"ok": true, "result": true, "description": "Webhook was set"}
```

---

## Tear Down

To delete all Azure resources:

```bash
terraform destroy
```

---

## Troubleshooting

| Error | Fix |
|---|---|
| `az login` fails | Try `az login --use-device-code` |
| `terraform init` fails | Check internet connection and Terraform version |
| Resource already exists | Import with `terraform import` or rename in `variables.tf` |
| Function App not starting | Check Application Insights logs in Azure Portal |
