output "function_app_name" {
  description = "Name of function app which we use in this environment"
  value       = module.AzureFunction.func_app_name
}
