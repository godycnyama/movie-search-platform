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

output "ollama_service_name" {
  value = aws_ecs_service.ollama.name
}

output "pipeline_task_definition_arn" {
  value = aws_ecs_task_definition.pipeline.arn
}

output "runtime_secret_arn" {
  value = aws_secretsmanager_secret.runtime.arn
}
