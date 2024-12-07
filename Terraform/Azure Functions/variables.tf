variable "af_rg_name" {
  description = "Function App resource group"
}

variable "af_location" {
  description = "Function App Azure region location"
}

variable "fapp_name" {
  description = "Function App base resource name"
  default     = "ecowitt"
}

variable "env_suffix" {
  description = "Resources name environmental suffix"
}

variable "appi_key" {
  description = "Application Insights key to be used by Function App"
}

variable "appi_conn_string" {
  description = "Application Insights connection string to be used by Function App"
}

variable "kv_name" {
  description = "Name of Key Vault where secrets are kept to be used by Function App"
}

variable "kv_rg" {
  description = "Resource group name where Key Vault with secrets is located"
}