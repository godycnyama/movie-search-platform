# Prod environment: Multi-AZ database with deletion protection and a final
# snapshot, larger instances, distinct CIDR. Same 1-2 task autoscaling band.

module "platform" {
  source = "../.."

  project_name = "movie-search"
  environment  = "prod"

  vpc_cidr = "10.61.0.0/16"

  image_tag = var.image_tag

  # api + mcp-server autoscale between 1 and 2 tasks.
  service_min_count = 1
  service_max_count = 2

  db_instance_class      = "db.t4g.small"
  db_multi_az            = true
  db_deletion_protection = true
  db_skip_final_snapshot = false
  redis_node_type        = "cache.t4g.small"

  acm_certificate_arn = var.acm_certificate_arn

  github_repository = var.github_repository
  # The dev stack (same account) already created the account-wide provider.
  create_github_oidc_provider = var.create_github_oidc_provider
  terraform_state_bucket_arn  = var.terraform_state_bucket_arn
  terraform_lock_table_arn    = var.terraform_lock_table_arn

  alarm_email = var.alarm_email
}
