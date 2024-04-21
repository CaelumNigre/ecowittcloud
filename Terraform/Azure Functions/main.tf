locals {  
  location_suffix = var.af_location == "westeurope" ? "euw" : "other"    
}

resource "azurerm_storage_account" "fapp-operational" {
  name                  = "sa${var.fapp_name}func${var.env_suffix}${local.location_suffix}01"    
  resource_group_name   = var.af_rg_name
  location              = var.af_location
  account_kind          = "StorageV2"
  access_tier           = "Hot"
  account_replication_type = "LRS"
  account_tier          = "Standard"
}

resource "azurerm_storage_account" "fapp-data" {
  name                  = "sa${var.fapp_name}data${var.env_suffix}${local.location_suffix}01"    
  resource_group_name   = var.af_rg_name
  location              = var.af_location
  account_kind          = "StorageV2"
  access_tier           = "Hot"
  account_replication_type = "LRS"
  account_tier          = "Standard"    
}

resource "azurerm_service_plan" "appplan" {
  name                  = "${var.fapp_name}${var.env_suffix}${local.location_suffix}"
  location              = var.af_location
  os_type               = "Windows"
  resource_group_name   = var.af_rg_name
  sku_name              = "Y1"
}

resource "random_string" "sharesuffix" {
  length           = 6
  special          = false
  upper            = false    
}

resource "azurerm_windows_function_app" "fapp" {
  name                  = "fa-${var.fapp_name}-${var.env_suffix}-${local.location_suffix}"   
  location              = var.af_location
  resource_group_name   = var.af_rg_name
  service_plan_id       = azurerm_service_plan.appplan.id
  functions_extension_version = "~4"
  site_config {
    application_insights_key = var.appi_key
    application_insights_connection_string = var.appi_conn_string
    application_stack {
      dotnet_version = "v8.0"
    }
    ftps_state          = "Disabled"
    remote_debugging_enabled = true
    remote_debugging_version = "VS2022"
    use_32_bit_worker = false    
  }
  app_settings = {
    FUNCTIONS_WORKER_RUNTIME = "dotnet"    
    WEBSITE_CONTENTAZUREFILECONNECTIONSTRING = azurerm_storage_account.fapp-operational.primary_connection_string
    WEBSITE_CONTENTSHARE = "${var.fapp_name}-${random_string.sharesuffix.result}" 
  }
  storage_account_name = azurerm_storage_account.fapp-operational.name
  storage_uses_managed_identity = true
  identity {
    type    = "SystemAssigned"
  }
}

resource "azurerm_role_assignment" "func_access_to_sa_blobs" {
  scope     = azurerm_storage_account.fapp-data.id
  role_definition_name      = "Storage Blob Data Contributor"
  principal_id              = azurerm_windows_function_app.fapp.identity[0].principal_id
}

resource "azurerm_role_assignment" "func_access_to_sa_table" {
  scope     = azurerm_storage_account.fapp-data.id
  role_definition_name      = "Storage Table Data Contributor"
  principal_id              = azurerm_windows_function_app.fapp.identity[0].principal_id
}
