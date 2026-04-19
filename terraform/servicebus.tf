resource "azurerm_servicebus_namespace" "sb" {
  name                = "${var.prefix}-bus"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  sku                 = "Basic"
}

resource "azurerm_servicebus_queue" "fetch_topics" {
  name         = "fetch-topics"
  namespace_id = azurerm_servicebus_namespace.sb.id
}

resource "azurerm_servicebus_queue" "generate_article" {
  name         = "generate-article"
  namespace_id = azurerm_servicebus_namespace.sb.id
}

resource "azurerm_servicebus_queue" "post_article" {
  name         = "post-article"
  namespace_id = azurerm_servicebus_namespace.sb.id
}
