# Generated credentials, held in Secrets Manager — never in tfvars or source.
# The composed runtime configuration (connection strings) lives in the ecs
# module; this module owns the raw credential material.

resource "random_password" "db" {
  length  = 32
  special = false # avoid characters that break URL-encoded DSNs
}

resource "random_password" "redis_auth" {
  length  = 32
  special = false # ElastiCache AUTH tokens are alphanumeric-safe
}

resource "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

resource "aws_secretsmanager_secret" "db_password" {
  name        = "${var.name_prefix}/db-password"
  description = "PostgreSQL master password."
}

resource "aws_secretsmanager_secret_version" "db_password" {
  secret_id     = aws_secretsmanager_secret.db_password.id
  secret_string = random_password.db.result
}

resource "aws_secretsmanager_secret" "redis_auth" {
  name        = "${var.name_prefix}/redis-auth-token"
  description = "ElastiCache Redis AUTH token."
}

resource "aws_secretsmanager_secret_version" "redis_auth" {
  secret_id     = aws_secretsmanager_secret.redis_auth.id
  secret_string = random_password.redis_auth.result
}

resource "aws_secretsmanager_secret" "jwt_signing_key" {
  name        = "${var.name_prefix}/jwt-signing-key"
  description = "Symmetric signing key for API-issued JWTs."
}

resource "aws_secretsmanager_secret_version" "jwt_signing_key" {
  secret_id     = aws_secretsmanager_secret.jwt_signing_key.id
  secret_string = random_password.jwt_signing_key.result
}
