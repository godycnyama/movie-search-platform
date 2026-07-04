# Outputs surfaced to the environments (and from there to CD).

output "alb_dns_name" {
  description = "Public DNS name of the API load balancer."
  value       = module.alb.dns_name
}

output "api_base_url" {
  description = "Base URL of the deployed API."
  value       = var.acm_certificate_arn == null ? "http://${module.alb.dns_name}" : "https://${module.alb.dns_name}"
}

output "ecr_repository_urls" {
  description = "ECR repository URLs keyed by image name (api, mcp-server, pipeline)."
  value       = module.ecr.repository_urls
}

output "ecs_cluster_name" {
  description = "ECS cluster name (used by CD to run the one-off pipeline task)."
  value       = module.ecs.cluster_name
}

output "pipeline_task_definition_arn" {
  description = "Task definition ARN for the one-off data pipeline run."
  value       = module.ecs.pipeline_task_definition_arn
}

output "pipeline_network_configuration" {
  description = "Network settings for `aws ecs run-task` (private subnets + tasks SG)."
  value = {
    subnets         = module.networking.private_subnet_ids
    security_groups = [module.networking.tasks_security_group_id]
  }
}

output "github_deploy_role_arn" {
  description = "IAM role GitHub Actions assumes via OIDC (empty when github_repository is unset)."
  value       = module.iam.deploy_role_arn
}

output "db_endpoint" {
  description = "RDS endpoint (host:port)."
  value       = module.rds.endpoint
}
