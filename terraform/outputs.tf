output "function_app_name" {
  value = azurerm_linux_function_app.func.name
}

output "function_app_url" {
  value = "https://${azurerm_linux_function_app.func.default_hostname}"
}

output "telegram_webhook_url" {
  value = "https://${azurerm_linux_function_app.func.default_hostname}/api/TelegramWebhook"
}
