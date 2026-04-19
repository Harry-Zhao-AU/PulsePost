resource "azurerm_service_plan" "plan" {
  name                = "${var.prefix}-plan"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = "Y1"
}

resource "azurerm_linux_function_app" "func" {
  name                       = "${var.prefix}-func"
  resource_group_name        = azurerm_resource_group.rg.name
  location                   = azurerm_resource_group.rg.location
  storage_account_name       = azurerm_storage_account.sa.name
  storage_account_access_key = azurerm_storage_account.sa.primary_access_key
  service_plan_id            = azurerm_service_plan.plan.id
  https_only                 = true

  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }
  }

  app_settings = {
    APPINSIGHTS_INSTRUMENTATIONKEY = azurerm_application_insights.ai.instrumentation_key
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.ai.connection_string

    SERVICE_BUS_CONNECTION  = azurerm_servicebus_namespace.sb.default_primary_connection_string
    STORAGE_CONNECTION      = azurerm_storage_account.sa.primary_connection_string

    AZURE_OPENAI_ENDPOINT   = var.azure_openai_endpoint
    AZURE_OPENAI_KEY        = var.azure_openai_key
    AZURE_OPENAI_DEPLOYMENT = var.azure_openai_deployment

    TELEGRAM_BOT_TOKEN      = var.telegram_bot_token
    TELEGRAM_CHAT_ID        = var.telegram_chat_id
    GITHUB_PAT              = var.github_pat
  }
}
