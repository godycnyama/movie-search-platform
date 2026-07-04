# ECS Fargate cluster hosting three services (api, mcp-server, ollama) plus the
# one-off pipeline task definition. Internal traffic is addressed through a
# Cloud Map private DNS namespace:
#
#   api ──http──► mcp-server.<ns>:8000 ──http──► ollama.<ns>:11434
#
# Connection strings and credentials are injected from a single Secrets Manager
# "runtime" secret (JSON keys referenced per container) — nothing sensitive in
# task-definition environment blocks.

data "aws_region" "current" {}

resource "aws_ecs_cluster" "this" {
  name = "${var.name_prefix}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# --- Service discovery ----------------------------------------------------------

resource "aws_service_discovery_private_dns_namespace" "this" {
  name        = "${var.name_prefix}.internal"
  description = "Internal DNS for api -> mcp-server -> ollama."
  vpc         = var.vpc_id
}

resource "aws_service_discovery_service" "mcp" {
  name = "mcp-server"

  dns_config {
    namespace_id   = aws_service_discovery_private_dns_namespace.this.id
    routing_policy = "MULTIVALUE"

    dns_records {
      type = "A"
      ttl  = 10
    }
  }
}

resource "aws_service_discovery_service" "ollama" {
  name = "ollama"

  dns_config {
    namespace_id   = aws_service_discovery_private_dns_namespace.this.id
    routing_policy = "MULTIVALUE"

    dns_records {
      type = "A"
      ttl  = 10
    }
  }
}

locals {
  mcp_host    = "mcp-server.${aws_service_discovery_private_dns_namespace.this.name}"
  ollama_host = "ollama.${aws_service_discovery_private_dns_namespace.this.name}"
  ollama_url  = "http://ollama.${aws_service_discovery_private_dns_namespace.this.name}:11434"
}

# --- Logs -----------------------------------------------------------------------

resource "aws_cloudwatch_log_group" "service" {
  for_each = toset(["api", "mcp-server", "ollama", "pipeline"])

  name              = "/ecs/${var.name_prefix}/${each.key}"
  retention_in_days = var.log_retention_days
}

# --- Runtime secret (composed connection strings) ----------------------------------

resource "aws_secretsmanager_secret" "runtime" {
  name        = "${var.name_prefix}/runtime"
  description = "Composed connection strings + credentials injected into task definitions."
}

resource "aws_secretsmanager_secret_version" "runtime" {
  secret_id = aws_secretsmanager_secret.runtime.id

  secret_string = jsonencode({
    # .NET API (Npgsql). RDS enforces TLS.
    postgres_connstring = "Host=${var.db_address};Port=${var.db_port};Database=${var.db_name};Username=${var.db_username};Password=${var.db_password};SSL Mode=Require;Trust Server Certificate=true"
    # Python services (asyncpg / psycopg).
    database_url = "postgresql://${var.db_username}:${var.db_password}@${var.db_address}:${var.db_port}/${var.db_name}?sslmode=require"
    # StackExchange.Redis — ElastiCache has AUTH + TLS in transit.
    redis_connstring = "${var.redis_address}:${var.redis_port},password=${var.redis_auth_token},ssl=true,abortConnect=false"
    jwt_signing_key  = var.jwt_signing_key
  })
}

# --- IAM ------------------------------------------------------------------------

data "aws_iam_policy_document" "task_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "execution" {
  name               = "${var.name_prefix}-ecs-execution"
  assume_role_policy = data.aws_iam_policy_document.task_assume.json
}

resource "aws_iam_role_policy_attachment" "execution" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

data "aws_iam_policy_document" "read_runtime_secret" {
  statement {
    actions   = ["secretsmanager:GetSecretValue"]
    resources = [aws_secretsmanager_secret.runtime.arn]
  }
}

resource "aws_iam_role_policy" "execution_secrets" {
  name   = "read-runtime-secret"
  role   = aws_iam_role.execution.id
  policy = data.aws_iam_policy_document.read_runtime_secret.json
}

# Task role shared by the app containers: no AWS API access beyond ECS Exec
# (SSM channels) for debugging. Widen per-service if a service ever needs AWS APIs.
resource "aws_iam_role" "task" {
  name               = "${var.name_prefix}-ecs-task"
  assume_role_policy = data.aws_iam_policy_document.task_assume.json
}

data "aws_iam_policy_document" "ecs_exec" {
  statement {
    actions = [
      "ssmmessages:CreateControlChannel",
      "ssmmessages:CreateDataChannel",
      "ssmmessages:OpenControlChannel",
      "ssmmessages:OpenDataChannel",
    ]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "ecs_exec" {
  name   = "ecs-exec"
  role   = aws_iam_role.task.id
  policy = data.aws_iam_policy_document.ecs_exec.json
}

# --- EFS: Ollama model cache -------------------------------------------------------

resource "aws_efs_file_system" "ollama" {
  creation_token = "${var.name_prefix}-ollama-models"
  encrypted      = true

  lifecycle_policy {
    transition_to_ia = "AFTER_30_DAYS"
  }

  tags = { Name = "${var.name_prefix}-ollama-models" }
}

resource "aws_efs_mount_target" "ollama" {
  count = length(var.private_subnet_ids)

  file_system_id  = aws_efs_file_system.ollama.id
  subnet_id       = var.private_subnet_ids[count.index]
  security_groups = [var.tasks_sg_id] # tasks SG self-ingress covers NFS 2049
}
