# Configure the Azure provider
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.83.0"
    }
  }
  backend "azurerm" {
    
  }
  required_version = ">= 1.1.0"
}

provider "azurerm" {
  features {

    application_insights {
      disable_generated_rule = true
    }

    resource_group {
      prevent_deletion_if_contains_resources = false      
    }

  }
}

locals {
  location_suffix = var.rg_location == "westeurope" ? "euw" : "other"
}

# resource group for resources deployment  
resource "azurerm_resource_group" "rg" {
  name     = "rg-${var.rg_basename}-${var.env_suffix}-${local.location_suffix}"
  location = var.rg_location
}

module "LAW" {
    count = var.deploy_optional_resources ? 1 : 0
    source = "./LAW"
    law_location = var.rg_location
    law_rg = azurerm_resource_group.rg.name
    env_suffix = var.env_suffix
}
module "AppInsights" {
    count = var.deploy_optional_resources ? 1 : 0
    source = "./App Insights"
    appi_rg = azurerm_resource_group.rg.name
    appi_location = var.rg_location
    appi_workspace =  module.LAW.workspace_id
    env_suffix = var.env_suffix
}

module "KV" {
    source = "./KV"
    kv_rg = azurerm_resource_group.rg.name
    kv_location = var.rg_location     
    env_suffix = var.env_suffix
}

module "AzureFunction" {    
    source = "./Azure Functions"
    af_rg_name = azurerm_resource_group.rg.name
    af_location = var.rg_location    
    env_suffix = var.env_suffix
    # FIXME: will fail if optional resources are not enabled
    appi_key = module.AppInsights.app_insights_key
    appi_conn_string = module.AppInsights.app_insights_connection_string 
}


