variable "prefix" {
  default = "pulsepost"
}

variable "resource_group_name" {
  default = "pulsepost-rg"
}

variable "location" {
  default = "australiaeast"
}

variable "storage_account_name" {
  default = "pulsepostsa"
}

variable "telegram_bot_token" {
  sensitive = true
}

variable "telegram_chat_id" {
  sensitive = true
}

variable "github_pat" {
  sensitive = true
}

variable "azure_openai_endpoint" {
  sensitive = true
}

variable "azure_openai_key" {
  sensitive = true
}

variable "azure_openai_deployment" {
  default = "gpt-4o"
}
