# ElastiCache Redis 7 for the API's response cache. AUTH + TLS in transit; the
# API's StackExchange.Redis connection string carries password= and ssl=true
# (composed in the ecs module). Single node — the cache is disposable.

resource "aws_elasticache_subnet_group" "this" {
  name       = "${var.name_prefix}-redis"
  subnet_ids = var.private_subnet_ids
}

resource "aws_security_group" "this" {
  name        = "${var.name_prefix}-redis"
  description = "Redis from the Fargate tasks only."
  vpc_id      = var.vpc_id

  ingress {
    description     = "Redis from ECS tasks"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [var.ingress_sg_id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.name_prefix}-redis" }
}

resource "aws_elasticache_replication_group" "this" {
  replication_group_id = "${var.name_prefix}-redis"
  description          = "API response cache (allkeys-lru semantics via parameter group)."

  engine         = "redis"
  engine_version = "7.1"
  node_type      = var.node_type

  num_cache_clusters   = 1
  parameter_group_name = aws_elasticache_parameter_group.this.name

  subnet_group_name  = aws_elasticache_subnet_group.this.name
  security_group_ids = [aws_security_group.this.id]

  port                       = 6379
  auth_token                 = var.auth_token
  transit_encryption_enabled = true
  at_rest_encryption_enabled = true

  automatic_failover_enabled = false
  snapshot_retention_limit   = 0 # pure cache — nothing worth snapshotting
  apply_immediately          = true
}

resource "aws_elasticache_parameter_group" "this" {
  name   = "${var.name_prefix}-redis7"
  family = "redis7"

  # Mirrors the docker-compose redis tuning: bounded memory, LRU eviction.
  parameter {
    name  = "maxmemory-policy"
    value = "allkeys-lru"
  }
}
