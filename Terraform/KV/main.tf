locals {
  location_suffix = var.kv_location == "westeurope" ? "euw" : "other"
}

data "azurerm_subscription" "current" {
}

resource "azurerm_key_vault" "keyvault" {
  # checkov:skip=CKV2_AZURE_32:Not applicable as we use AF Consumption plan
  # checkov:skip=CKV_AZURE_189:Not applicable as we use AF Consumption plan
  # checkov:skip=CKV_AZURE_109:Not applicable as we need to access KV from Internet
  name                       = "kv-${var.kv_name}-${var.env_suffix}-${local.location_suffix}-01"
  location                   = var.kv_location
  resource_group_name        = var.kv_rg
  tenant_id                  = data.azurerm_subscription.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  enable_rbac_authorization  = true
  purge_protection_enabled   = true
}