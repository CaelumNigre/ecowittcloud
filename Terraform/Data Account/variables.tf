variable "sa_rg_name" {
  description = "Data storage account resource group"
}

variable "sa_location" {
  description = "Data storage account Azure region location"
}

variable "sa_name" {
  description = "Data storage account base resource name"
  default     = "ecowittdata"
}

variable "env_suffix" {
  description = "Resources name environmental suffix"
}
