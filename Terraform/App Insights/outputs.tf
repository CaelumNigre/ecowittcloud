output "app_insights_key" {
  value = azurerm_application_insights.appinsights.instrumentation_key
}

output "app_insights_connection_string" {
  value = azurerm_application_insights.appinsights.connection_string
}
