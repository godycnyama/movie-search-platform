output "cluster_name" {
  value = aws_ecs_cluster.this.name
}

output "cluster_arn" {
  value = aws_ecs_cluster.this.arn
}

output "api_service_name" {
  value = aws_ecs_service.api.name
}

output "mcp_service_name" {
  value = aws_ecs_service.mcp.name
}

output "runtime_secret_arn" {
  value = aws_secretsmanager_secret.runtime.arn
}
