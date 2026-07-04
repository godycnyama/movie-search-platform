# Services + autoscaling. api and mcp-server scale between min and max on CPU;
# ollama runs a single task (its model cache lives on EFS, and query embedding
# is fast once the model is resident).

resource "aws_ecs_service" "ollama" {
  name            = "${var.name_prefix}-ollama"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.ollama.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  enable_execute_command = true

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.tasks_sg_id]
    assign_public_ip = false
  }

  service_registries {
    registry_arn = aws_service_discovery_service.ollama.arn
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }
}

resource "aws_ecs_service" "mcp" {
  name            = "${var.name_prefix}-mcp-server"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.mcp.arn
  desired_count   = var.service_min_count
  launch_type     = "FARGATE"

  enable_execute_command = true

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.tasks_sg_id]
    assign_public_ip = false
  }

  service_registries {
    registry_arn = aws_service_discovery_service.mcp.arn
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  lifecycle {
    ignore_changes = [desired_count] # autoscaling owns it after creation
  }
}

resource "aws_ecs_service" "api" {
  name            = "${var.name_prefix}-api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.service_min_count
  launch_type     = "FARGATE"

  enable_execute_command            = true
  health_check_grace_period_seconds = 90

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.tasks_sg_id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = var.alb_target_group
    container_name   = "api"
    container_port   = 8080
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  lifecycle {
    ignore_changes = [desired_count] # autoscaling owns it after creation
  }
}

# --- Autoscaling (min/max from variables; CPU target tracking) -------------------------

locals {
  scaled_services = {
    api = aws_ecs_service.api.name
    mcp = aws_ecs_service.mcp.name
  }
}

resource "aws_appautoscaling_target" "service" {
  for_each = local.scaled_services

  service_namespace  = "ecs"
  resource_id        = "service/${aws_ecs_cluster.this.name}/${each.value}"
  scalable_dimension = "ecs:service:DesiredCount"
  min_capacity       = var.service_min_count
  max_capacity       = var.service_max_count
}

resource "aws_appautoscaling_policy" "service_cpu" {
  for_each = aws_appautoscaling_target.service

  name               = "${each.value.resource_id}-cpu"
  policy_type        = "TargetTrackingScaling"
  service_namespace  = each.value.service_namespace
  resource_id        = each.value.resource_id
  scalable_dimension = each.value.scalable_dimension

  target_tracking_scaling_policy_configuration {
    target_value       = 60
    scale_in_cooldown  = 300
    scale_out_cooldown = 60

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
  }
}

resource "aws_appautoscaling_policy" "service_memory" {
  for_each = aws_appautoscaling_target.service

  name               = "${each.value.resource_id}-memory"
  policy_type        = "TargetTrackingScaling"
  service_namespace  = each.value.service_namespace
  resource_id        = each.value.resource_id
  scalable_dimension = each.value.scalable_dimension

  target_tracking_scaling_policy_configuration {
    target_value       = 70
    scale_in_cooldown  = 300
    scale_out_cooldown = 60

    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageMemoryUtilization"
    }
  }
}
