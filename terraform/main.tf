# Platform composition: wires the child modules into the deployable stack.
#
#   ALB (public) ──► api (Fargate) ──MCP──► mcp-server (Fargate) ──► Ollama (Fargate)
#                      │  EF Core (users)        │ asyncpg + pgvector
#                      ▼                         ▼
#                    RDS PostgreSQL 16 (pgvector, private subnets)
#                      + ElastiCache Redis (API response cache)
#
# The pipeline runs as a one-off ECS task (CD triggers it on demand).

locals {
  name_prefix = "${var.project_name}-${var.environment}"
}

module "networking" {
  source = "./modules/networking"

  name_prefix        = local.name_prefix
  vpc_cidr           = var.vpc_cidr
  az_count           = var.az_count
  log_retention_days = var.log_retention_days
}

module "ecr" {
  source = "./modules/ecr"

  name_prefix = local.name_prefix
}

module "secrets" {
  source = "./modules/secrets"

  name_prefix = local.name_prefix
}

module "rds" {
  source = "./modules/rds"

  name_prefix            = local.name_prefix
  vpc_id                 = module.networking.vpc_id
  private_subnet_ids     = module.networking.private_subnet_ids
  ingress_sg_id          = module.networking.tasks_security_group_id
  db_name                = var.db_name
  db_username            = var.db_username
  db_password            = module.secrets.db_password
  instance_class         = var.db_instance_class
  allocated_storage      = var.db_allocated_storage
  multi_az               = var.db_multi_az
  deletion_protection    = var.db_deletion_protection
  skip_final_snapshot    = var.db_skip_final_snapshot
  backup_retention_days  = var.environment == "prod" ? 7 : 1
  performance_monitoring = var.environment == "prod"
}

module "elasticache" {
  source = "./modules/elasticache"

  name_prefix        = local.name_prefix
  vpc_id             = module.networking.vpc_id
  private_subnet_ids = module.networking.private_subnet_ids
  ingress_sg_id      = module.networking.tasks_security_group_id
  node_type          = var.redis_node_type
  auth_token         = module.secrets.redis_auth_token
}

module "alb" {
  source = "./modules/alb"

  name_prefix         = local.name_prefix
  vpc_id              = module.networking.vpc_id
  public_subnet_ids   = module.networking.public_subnet_ids
  security_group_id   = module.networking.alb_security_group_id
  target_port         = 8080
  health_check_path   = "/health"
  acm_certificate_arn = var.acm_certificate_arn
}

module "ecs" {
  source = "./modules/ecs"

  name_prefix        = local.name_prefix
  environment        = var.environment
  vpc_id             = module.networking.vpc_id
  private_subnet_ids = module.networking.private_subnet_ids
  tasks_sg_id        = module.networking.tasks_security_group_id
  alb_target_group   = module.alb.target_group_arn

  api_image      = "${module.ecr.repository_urls["api"]}:${var.image_tag}"
  mcp_image      = "${module.ecr.repository_urls["mcp-server"]}:${var.image_tag}"
  pipeline_image = "${module.ecr.repository_urls["pipeline"]}:${var.image_tag}"
  ollama_image   = var.ollama_image

  api_cpu         = var.api_cpu
  api_memory      = var.api_memory
  mcp_cpu         = var.mcp_cpu
  mcp_memory      = var.mcp_memory
  ollama_cpu      = var.ollama_cpu
  ollama_memory   = var.ollama_memory
  pipeline_cpu    = var.pipeline_cpu
  pipeline_memory = var.pipeline_memory

  service_min_count = var.service_min_count
  service_max_count = var.service_max_count

  db_address       = module.rds.address
  db_port          = module.rds.port
  db_name          = var.db_name
  db_username      = var.db_username
  db_password      = module.secrets.db_password
  redis_address    = module.elasticache.primary_endpoint_address
  redis_port       = module.elasticache.port
  redis_auth_token = module.secrets.redis_auth_token
  jwt_signing_key  = module.secrets.jwt_signing_key

  mcp_transport    = var.mcp_transport
  embedding_model  = var.embedding_model
  embedding_dim    = var.embedding_dim
  pipeline_version = var.pipeline_version
  jwt_issuer       = var.jwt_issuer
  jwt_audience     = var.jwt_audience
  log_level        = var.log_level

  log_retention_days = var.log_retention_days
}

module "iam" {
  source = "./modules/iam"

  name_prefix                 = local.name_prefix
  managed_role_prefix         = var.project_name
  github_repository           = var.github_repository
  create_github_oidc_provider = var.create_github_oidc_provider
  ecr_repository_arns         = values(module.ecr.repository_arns)
  terraform_state_bucket_arn  = var.terraform_state_bucket_arn
  terraform_lock_table_arn    = var.terraform_lock_table_arn
}

module "monitoring" {
  source = "./modules/monitoring"

  name_prefix          = local.name_prefix
  alarm_email          = var.alarm_email
  cluster_name         = module.ecs.cluster_name
  service_names        = [module.ecs.api_service_name, module.ecs.mcp_service_name]
  alb_arn_suffix       = module.alb.arn_suffix
  target_group_suffix  = module.alb.target_group_arn_suffix
  db_instance_identity = module.rds.identifier
}
