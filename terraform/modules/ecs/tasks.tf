# Task definitions. Sensitive values arrive via the runtime secret's JSON keys
# ("<secret-arn>:<json-key>::"); everything else is plain environment.

locals {
  runtime = aws_secretsmanager_secret.runtime.arn

  log_configuration = {
    for name, group in aws_cloudwatch_log_group.service : name => {
      logDriver = "awslogs"
      options = {
        awslogs-group         = group.name
        awslogs-region        = data.aws_region.current.name
        awslogs-stream-prefix = name
      }
    }
  }
}

# --- .NET API ---------------------------------------------------------------------

resource "aws_ecs_task_definition" "api" {
  family                   = "${var.name_prefix}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = var.api_image
      essential = true

      portMappings = [{ containerPort = 8080, protocol = "tcp" }]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = "http://127.0.0.1:4317" },
        { name = "OTEL_SERVICE_NAME", value = "movie-search-api" },
        # Movie data source: the MCP server, resolved through Cloud Map.
        { name = "McpSettings__ServerUrl", value = "http://${local.mcp_host}:8000" },
        { name = "McpSettings__Transport", value = var.mcp_transport },
        { name = "JwtSettings__Issuer", value = var.jwt_issuer },
        { name = "JwtSettings__Audience", value = var.jwt_audience },
      ]

      dependsOn = [
        { containerName = "aws-otel-collector", condition = "START" }
      ]

      secrets = [
        { name = "PostgresSettings__PostgresConnectionString", valueFrom = "${local.runtime}:postgres_connstring::" },
        { name = "RedisSettings__ConnectionString", valueFrom = "${local.runtime}:redis_connstring::" },
        { name = "JwtSettings__SigningKey", valueFrom = "${local.runtime}:jwt_signing_key::" },
      ]

      healthCheck = {
        command     = ["CMD-SHELL", "curl -fsS http://localhost:8080/health || exit 1"]
        interval    = 15
        timeout     = 5
        retries     = 5
        startPeriod = 60
      }

      logConfiguration = local.log_configuration["api"]
    },
    {
      name      = "aws-otel-collector"
      image     = "public.ecr.aws/aws-observability/aws-otel-collector:v0.40.0"
      essential = false

      command = ["--config=/etc/ecs/ecs-default-config.yaml"]

      healthCheck = {
        command     = ["CMD-SHELL", "wget -q --spider http://localhost:13133/ || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 20
      }

      logConfiguration = local.log_configuration["xray"]
    }
  ])
}

# --- MCP server --------------------------------------------------------------------

resource "aws_ecs_task_definition" "mcp" {
  family                   = "${var.name_prefix}-mcp-server"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.mcp_cpu
  memory                   = var.mcp_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "mcp-server"
      image     = var.mcp_image
      essential = true

      portMappings = [{ containerPort = 8000, protocol = "tcp" }]

      environment = [
        { name = "MCP_HOST", value = "0.0.0.0" },
        { name = "MCP_PORT", value = "8000" },
        { name = "MCP_TRANSPORT", value = var.mcp_transport },
        { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = "http://127.0.0.1:4317" },
        { name = "OTEL_SERVICE_NAME", value = "mcp-server" },
        { name = "OLLAMA_URL", value = local.ollama_url },
        { name = "EMBEDDING_MODEL", value = var.embedding_model },
        { name = "EMBEDDING_DIM", value = tostring(var.embedding_dim) },
        { name = "LOG_LEVEL", value = var.log_level },
      ]

      dependsOn = [
        { containerName = "aws-otel-collector", condition = "START" }
      ]

      secrets = [
        { name = "DATABASE_URL", valueFrom = "${local.runtime}:database_url::" },
      ]

      healthCheck = {
        command     = ["CMD-SHELL", "curl -fsS http://localhost:8000/health || exit 1"]
        interval    = 15
        timeout     = 5
        retries     = 5
        startPeriod = 30
      }

      logConfiguration = local.log_configuration["mcp-server"]
    },
    {
      name      = "aws-otel-collector"
      image     = "public.ecr.aws/aws-observability/aws-otel-collector:v0.40.0"
      essential = false

      command = ["--config=/etc/ecs/ecs-default-config.yaml"]

      healthCheck = {
        command     = ["CMD-SHELL", "wget -q --spider http://localhost:13133/ || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 20
      }

      logConfiguration = local.log_configuration["xray"]
    }
  ])
}

# --- Ollama (embedding backend) -------------------------------------------------------

resource "aws_ecs_task_definition" "ollama" {
  family                   = "${var.name_prefix}-ollama"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.ollama_cpu
  memory                   = var.ollama_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  volume {
    name = "models"

    efs_volume_configuration {
      file_system_id     = aws_efs_file_system.ollama.id
      transit_encryption = "ENABLED"
    }
  }

  container_definitions = jsonencode([
    {
      name      = "ollama"
      image     = var.ollama_image
      essential = true

      portMappings = [{ containerPort = 11434, protocol = "tcp" }]

      # Serve, then make sure the embedding model is present (fast no-op when
      # the EFS cache already holds it; a pull on first boot).
      entryPoint = ["/bin/sh", "-c"]
      command = [
        "ollama serve & sleep 10 && ollama pull ${var.embedding_model}; wait"
      ]

      environment = [
        { name = "OLLAMA_HOST", value = "0.0.0.0" },
      ]

      mountPoints = [
        { sourceVolume = "models", containerPath = "/root/.ollama", readOnly = false }
      ]

      healthCheck = {
        command     = ["CMD-SHELL", "ollama list || exit 1"]
        interval    = 30
        timeout     = 10
        retries     = 5
        startPeriod = 120 # first boot may still be pulling the model
      }

      logConfiguration = local.log_configuration["ollama"]
    }
  ])
}

# --- Pipeline (one-off task; no service) ------------------------------------------------

resource "aws_ecs_task_definition" "pipeline" {
  family                   = "${var.name_prefix}-pipeline"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.pipeline_cpu
  memory                   = var.pipeline_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "pipeline"
      image     = var.pipeline_image
      essential = true

      environment = [
        { name = "OLLAMA_URL", value = local.ollama_url },
        { name = "EMBEDDING_MODEL", value = var.embedding_model },
        { name = "EMBEDDING_DIM", value = tostring(var.embedding_dim) },
        { name = "PIPELINE_VERSION", value = var.pipeline_version },
        { name = "LOG_LEVEL", value = var.log_level },
      ]

      secrets = [
        { name = "DATABASE_URL", valueFrom = "${local.runtime}:database_url::" },
      ]

      logConfiguration = local.log_configuration["pipeline"]
    }
  ])
}
