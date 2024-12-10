locals {
  location_suffix = var.sa_location == "westeurope" ? "euw" : "other"
}

resource "azurerm_storage_account" "data_sa" {
  # checkov:skip=CKV_AZURE_43:False positive
  # checkov:skip=CKV_AZURE_59:This is storage account for storing data that will accessed over Internet
  # checkov:skip=CKV2_AZURE_33:This is storage account for storing data that will accessed over Internet
  # checkov:skip=CKV_AZURE_33:Queue service is not used by this account
  # checkov:skip=CKV_AZURE_206:ZRS is good enough for me
  # checkov:skip=CKV2_AZURE_41:FIXME need to better understanding of the SAS expiration setting
  # checkov:skip=CKV2_AZURE_38:Soft delete not required
  # checkov:skip=CKV2_AZURE_1:Account does not contain sensitive data
  name                            = "sa${var.sa_name}${var.env_suffix}${local.location_suffix}01"
  resource_group_name             = var.sa_rg_name
  location                        = var.sa_location
  account_kind                    = "StorageV2"
  access_tier                     = "Hot"
  account_replication_type        = "ZRS"
  account_tier                    = "Standard"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false
  local_user_enabled              = false
}

resource "azurerm_storage_container" "config_blob_container" {
  # checkov:skip=CKV2_AZURE_21:Irrelevant
  name                  = "config"
  storage_account_id    = azurerm_storage_account.data_sa.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "data_blob_container" {
  # checkov:skip=CKV2_AZURE_21:Irrelevant
  name                  = "data"
  storage_account_id    = azurerm_storage_account.data_sa.id
  container_access_type = "private"
}

