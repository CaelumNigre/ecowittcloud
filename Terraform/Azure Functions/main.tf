locals {
  location_suffix = var.af_location == "westeurope" ? "euw" : "other"
}

resource "azurerm_storage_account" "fapp-operational" {
  # checkov:skip=CKV_AZURE_59:This is storage account for Azure Functions in Consumption plan - access cannot be restricted
  # checkov:skip=CKV_AZURE_33:Queue service is not used by this account
  # checkov:skip=CKV_AZURE_206:ZRS is good enough for me
  # checkov:skip=CKV2_AZURE_41:FIXME need to better understanding of the SAS expiration setting
  # checkov:skip=CKV2_AZURE_33:This is storage account for Azure Functions in Consumption plan - access cannot be restricted
  # checkov:skip=CKV2_AZURE_38:Soft delete not required
  # checkov:skip=CKV2_AZURE_1:Account does not contain sensitive data
  # checkov:skip=CKV2_AZURE_40:Azure Functions requires shared key acccess
  name                            = "sa${var.fapp_name}func${var.env_suffix}${local.location_suffix}01"
  resource_group_name             = var.af_rg_name
  location                        = var.af_location
  account_kind                    = "StorageV2"
  access_tier                     = "Hot"
  account_replication_type        = "ZRS"
  account_tier                    = "Standard"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  local_user_enabled              = false
}

resource "azurerm_storage_account" "fapp-data" {
  # checkov:skip=CKV_AZURE_59:This is storage account for storing data that will accessed over Internet
  # checkov:skip=CKV2_AZURE_33:This is storage account for storing data that will accessed over Internet
  # checkov:skip=CKV_AZURE_33:Queue service is not used by this account
  # checkov:skip=CKV_AZURE_206:ZRS is good enough for me
  # checkov:skip=CKV2_AZURE_41:FIXME need to better understanding of the SAS expiration setting
  # checkov:skip=CKV2_AZURE_38:Soft delete not required
  # checkov:skip=CKV2_AZURE_1:Account does not contain sensitive data
  name                            = "sa${var.fapp_name}data${var.env_suffix}${local.location_suffix}01"
  resource_group_name             = var.af_rg_name
  location                        = var.af_location
  account_kind                    = "StorageV2"
  access_tier                     = "Hot"
  account_replication_type        = "ZRS"
  account_tier                    = "Standard"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false
  local_user_enabled              = false
}

resource "azurerm_service_plan" "appplan" {
  # checkov:skip=CKV_AZURE_212:Worker count is not applicable for Consumption plan
  # checkov:skip=CKV_AZURE_225:Zone availability requires Premium plans
  name                = "${var.fapp_name}${var.env_suffix}${local.location_suffix}"
  location            = var.af_location
  os_type             = "Windows"
  resource_group_name = var.af_rg_name
  sku_name            = "Y1"
}

resource "random_string" "sharesuffix" {
  length  = 6
  special = false
  upper   = false
}

data "azurerm_client_config" "current" {}

resource "azurerm_windows_function_app" "fapp" {
  # checkov:skip=CKV_AZURE_221:This function needs to be available from Internet for testing purposes
  # checkov:skip=CKV_AZURE_72:FIXME Remote debugging enabled
  name                        = "fa-${var.fapp_name}-${var.env_suffix}-${local.location_suffix}"
  location                    = var.af_location
  resource_group_name         = var.af_rg_name
  service_plan_id             = azurerm_service_plan.appplan.id
  functions_extension_version = "~4"
  https_only                  = true
  site_config {
    application_insights_key               = var.appi_key
    application_insights_connection_string = var.appi_conn_string
    application_stack {
      dotnet_version = "v8.0"
    }
    ftps_state               = "Disabled"
    remote_debugging_enabled = true
    remote_debugging_version = "VS2022"
    use_32_bit_worker        = false
  }
  app_settings = {
    FUNCTIONS_WORKER_RUNTIME                 = "dotnet"
    WEBSITE_CONTENTAZUREFILECONNECTIONSTRING = azurerm_storage_account.fapp-operational.primary_connection_string
    WEBSITE_CONTENTSHARE                     = "${var.fapp_name}-${random_string.sharesuffix.result}"
    KV_NAME                                  = "${var.kv_name}"
    TENANT_ID                                = data.azurerm_client_config.current.tenant_id
  }
  storage_account_name          = azurerm_storage_account.fapp-operational.name
  storage_uses_managed_identity = true
  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_role_assignment" "func_access_to_sa_blobs" {
  scope                = azurerm_storage_account.fapp-data.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_windows_function_app.fapp.identity[0].principal_id
}

resource "azurerm_role_assignment" "func_access_to_sa_table" {
  scope                = azurerm_storage_account.fapp-data.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_windows_function_app.fapp.identity[0].principal_id
}

data "azurerm_key_vault" "secrets_kv" {
  name                = var.kv_name
  resource_group_name = var.kv_rg
}
resource "azurerm_role_assignment" "func_access_to_kv" {
  scope                = data.azurerm_key_vault.secrets_kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_windows_function_app.fapp.identity[0].principal_id
}
