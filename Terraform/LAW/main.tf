locals {
  location_suffix = var.law_location == "westeurope" ? "euw" : "other"
}

resource "azurerm_log_analytics_workspace" "law" {
  name                = "log-${var.law_name}-${var.env_suffix}-${local.location_suffix}-01"
  location            = var.law_location
  resource_group_name = var.law_rg
  sku                 = "PerGB2018"
  retention_in_days   = 30
  daily_quota_gb      = 1
}