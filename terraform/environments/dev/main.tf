# Dev environment: single-AZ database, smallest instance sizes, HTTP-only ALB.

module "platform" {
  source = "../.."

  project_name = "movie-search"
  environment  = "dev"

  image_tag = var.image_tag

  # api + mcp-server autoscale between 1 and 2 tasks.
  service_min_count = 1
  service_max_count = 2

  db_instance_class      = "db.t4g.micro"
  db_multi_az            = false
  db_deletion_protection = false
  db_skip_final_snapshot = true
  redis_node_type        = "cache.t4g.micro"

  acm_certificate_arn  = var.acm_certificate_arn
  cors_allowed_origins = var.cors_allowed_origins

  github_repository           = var.github_repository
  create_github_oidc_provider = var.create_github_oidc_provider
  terraform_state_bucket_arn  = var.terraform_state_bucket_arn
  terraform_lock_table_arn    = var.terraform_lock_table_arn

  alarm_email = var.alarm_email
}
