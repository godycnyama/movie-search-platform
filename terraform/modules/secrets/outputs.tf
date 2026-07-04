output "db_password" {
  value     = random_password.db.result
  sensitive = true
}

output "redis_auth_token" {
  value     = random_password.redis_auth.result
  sensitive = true
}

output "jwt_signing_key" {
  value     = random_password.jwt_signing_key.result
  sensitive = true
}
