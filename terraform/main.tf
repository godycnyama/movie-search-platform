# Platform composition: wires the child modules into the deployable stack.
#
#   ALB (public) ──► api (Fargate) ──MCP──► mcp-server (Fargate) ──► Bedrock (embeddings)
#                      │  EF Core (users)        │ asyncpg + pgvector
#                      ▼                         ▼
#                    RDS PostgreSQL 16 (pgvector, private subnets)
#                      + ElastiCache Redis (API response cache)
#
# The data pipeline is not deployed to AWS — it runs locally via docker-compose.

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

module "compute" {
  source = "./modules/compute"

  name_prefix        = local.name_prefix
  environment        = var.environment
  vpc_id             = module.networking.vpc_id
  private_subnet_ids = module.networking.private_subnet_ids
  tasks_sg_id        = module.networking.tasks_security_group_id
  alb_target_group   = module.alb.target_group_arn

  api_image = "${module.ecr.repository_urls["api"]}:${var.image_tag}"
  mcp_image = "${module.ecr.repository_urls["mcp-server"]}:${var.image_tag}"

  api_cpu    = var.api_cpu
  api_memory = var.api_memory
  mcp_cpu    = var.mcp_cpu
  mcp_memory = var.mcp_memory

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

  mcp_transport = var.mcp_transport

  # Embedding backend. The AWS environments use Bedrock; ENV is the environment
  # name so the Python services map dev/prod -> Bedrock.
  env                        = var.environment
  bedrock_region             = var.bedrock_region
  bedrock_embedding_model_id = var.bedrock_embedding_model_id
  embedding_dim              = var.embedding_dim

  jwt_issuer           = var.jwt_issuer
  jwt_audience         = var.jwt_audience
  cors_allowed_origins = var.cors_allowed_origins
  log_level            = var.log_level

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
  cluster_name         = module.compute.cluster_name
  service_names        = [module.compute.api_service_name, module.compute.mcp_service_name]
  xray_service_names   = ["movie-search-api", "mcp-server"]
  alb_arn_suffix       = module.alb.arn_suffix
  target_group_suffix  = module.alb.target_group_arn_suffix
  db_instance_identity = module.rds.identifier
}
