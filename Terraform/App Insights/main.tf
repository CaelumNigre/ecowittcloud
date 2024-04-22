locals {
  location_suffix = var.appi_location == "westeurope" ? "euw" : "other"
}

resource "azurerm_application_insights" "appinsights" {
  name                = "appi-${var.appi_name}-${var.env_suffix}-${local.location_suffix}-01"
  location            = var.appi_location
  resource_group_name = var.appi_rg
  application_type    = "web"
  retention_in_days   = 90
  workspace_id        = var.appi_workspace
}
