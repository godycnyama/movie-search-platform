output "api_base_url" {
  value = module.platform.api_base_url
}

output "alb_dns_name" {
  value = module.platform.alb_dns_name
}

output "ecr_repository_urls" {
  value = module.platform.ecr_repository_urls
}

output "ecs_cluster_name" {
  value = module.platform.ecs_cluster_name
}

output "pipeline_task_definition_arn" {
  value = module.platform.pipeline_task_definition_arn
}

output "pipeline_network_configuration" {
  value = module.platform.pipeline_network_configuration
}

output "github_deploy_role_arn" {
  value = module.platform.github_deploy_role_arn
}
