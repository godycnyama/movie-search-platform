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

      environment = concat([
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = "http://127.0.0.1:4317" },
        { name = "OTEL_SERVICE_NAME", value = "movie-search-api" },
        # Movie data source: the MCP server, resolved through Cloud Map.
        { name = "McpSettings__ServerUrl", value = "http://${local.mcp_host}:8000" },
        { name = "McpSettings__Transport", value = var.mcp_transport },
        { name = "JwtSettings__Issuer", value = var.jwt_issuer },
        { name = "JwtSettings__Audience", value = var.jwt_audience },
        { name = "JwtSettings__ExpiryMinutes", value = tostring(var.jwt_expiry_minutes) },
        # RedisSettings — connection string arrives via the runtime secret; these tune it.
        { name = "RedisSettings__InstanceName", value = var.redis_instance_name },
        { name = "RedisSettings__DefaultTtlSeconds", value = tostring(var.redis_default_ttl_seconds) },
        { name = "RedisSettings__AbortOnConnectFail", value = tostring(var.redis_abort_on_connect_fail) },
        { name = "RedisSettings__UseSsl", value = tostring(var.redis_use_ssl) },
        # RateLimitSettings / RequestTimeoutSettings.
        { name = "RateLimitSettings__PermitLimit", value = tostring(var.rate_limit_permit) },
        { name = "RateLimitSettings__WindowSeconds", value = tostring(var.rate_limit_window_seconds) },
        { name = "RateLimitSettings__QueueLimit", value = tostring(var.rate_limit_queue_limit) },
        { name = "RequestTimeoutSettings__DefaultTimeoutSeconds", value = tostring(var.request_timeout_seconds) },
        ], [
        # CorsSettings.AllowedOrigins is a bound array: Section__AllowedOrigins__<index>.
        for i, origin in var.cors_allowed_origins :
        { name = "CorsSettings__AllowedOrigins__${i}", value = origin }
      ])

      dependsOn = [
        { containerName = "aws-otel-collector", condition = "START" }
      ]

      secrets = [
        { name = "PostgresSettings__PostgresConnectionString", valueFrom = "${local.runtime}:postgres_connstring::" },
        { name = "RedisSettings__ConnectionString", valueFrom = "${local.runtime}:redis_connstring::" },
        { name = "JwtSettings__SigningKey", valueFrom = "${local.runtime}:jwt_signing_key::" },
      ]

      healthCheck = {
        # Liveness: replace only genuinely dead tasks. A transient dependency outage
        # must not trigger a restart storm — the ALB target group uses /health/ready
        # to drain traffic from a task whose dependencies are down.
        command     = ["CMD-SHELL", "curl -fsS http://localhost:8080/health/live || exit 1"]
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
        # Embedding backend: Amazon Bedrock (ENV drives dev/prod -> Bedrock).
        { name = "ENV", value = var.env },
        { name = "BEDROCK_REGION", value = local.bedrock_region },
        { name = "BEDROCK_EMBEDDING_MODEL_ID", value = var.bedrock_embedding_model_id },
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

# The data pipeline is not deployed to AWS — it runs locally via docker-compose
# (see docker-compose.yml). No ECS task definition exists for it here.
