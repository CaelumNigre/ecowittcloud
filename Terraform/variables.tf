variable "rg_location" {
    default = "westeurope"
}

variable "rg_basename"
{
    default = "ecowitt"
}

variable "env_suffix" {    
    default = "test"
    type = string
    nullable = false
    description = "This is a variable used to distinguish between test and production environment. The default value is 'test'"
}

variable "deploy_optional_resources" {
  default = true
  type = bool
  description = "Should optional resources (AppInsights, Log Analytics workspace) be deployed to cloud. The default value is false"
}
