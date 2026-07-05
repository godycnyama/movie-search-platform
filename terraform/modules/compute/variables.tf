variable "name_prefix" {
  description = "Resource name prefix (project-environment)."
  type        = string
}

variable "environment" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "private_subnet_ids" {
  type = list(string)
}

variable "tasks_sg_id" {
  description = "Security group attached to all Fargate tasks."
  type        = string
}

variable "alb_target_group" {
  description = "Target group the api service registers with."
  type        = string
}

# --- Images -----------------------------------------------------------------

variable "api_image" {
  type = string
}

variable "mcp_image" {
  type = string
}

# --- Sizing -----------------------------------------------------------------

variable "api_cpu" {
  type = number
}

variable "api_memory" {
  type = number
}

variable "mcp_cpu" {
  type = number
}

variable "mcp_memory" {
  type = number
}

variable "service_min_count" {
  type = number
}

variable "service_max_count" {
  type = number
}

# --- Backing services ----------------------------------------------------------

variable "db_address" {
  type = string
}

variable "db_port" {
  type = number
}

variable "db_name" {
  description = "Shared application database (movies-<env>), used by the api and mcp-server."
  type        = string
}

variable "db_username" {
  type = string
}

variable "db_password" {
  type      = string
  sensitive = true
}

variable "redis_address" {
  type = string
}

variable "redis_port" {
  type = number
}

variable "redis_auth_token" {
  type      = string
  sensitive = true
}

variable "jwt_signing_key" {
  type      = string
  sensitive = true
}

# --- App configuration -----------------------------------------------------------

variable "mcp_transport" {
  type = string
}

variable "env" {
  description = "Value of ENV passed to the Python services (local | dev | prod)."
  type        = string
}

variable "bedrock_region" {
  description = "Region for the Bedrock runtime; empty string uses the deployment region."
  type        = string
  default     = ""
}

variable "bedrock_embedding_model_id" {
  description = "Bedrock embedding model id (pipeline and MCP server must agree)."
  type        = string
}

variable "embedding_dim" {
  type = number
}

variable "jwt_issuer" {
  type = string
}

variable "jwt_audience" {
  type = string
}

variable "jwt_expiry_minutes" {
  description = "JwtSettings.ExpiryMinutes — access-token lifetime (5–1440)."
  type        = number
  default     = 60
}

# --- .NET API settings (bound via Section__Property) -------------------------------

variable "cors_allowed_origins" {
  description = "CorsSettings.AllowedOrigins — browser origins allowed to call the API."
  type        = list(string)
  default     = []
}

variable "redis_instance_name" {
  description = "RedisSettings.InstanceName — prefix applied to every cache key."
  type        = string
  default     = "movie-search:"
}

variable "redis_default_ttl_seconds" {
  description = "RedisSettings.DefaultTtlSeconds — default cache entry TTL (1–86400)."
  type        = number
  default     = 300
}

variable "redis_abort_on_connect_fail" {
  description = "RedisSettings.AbortOnConnectFail — fail hard if Redis is down at startup."
  type        = bool
  default     = false
}

variable "redis_use_ssl" {
  description = "RedisSettings.UseSsl — encrypt the Redis connection (ElastiCache uses TLS in transit)."
  type        = bool
  default     = true
}

variable "rate_limit_permit" {
  description = "RateLimitSettings.PermitLimit — requests allowed per window."
  type        = number
  default     = 60
}

variable "rate_limit_window_seconds" {
  description = "RateLimitSettings.WindowSeconds — rate-limit window length in seconds."
  type        = number
  default     = 60
}

variable "rate_limit_queue_limit" {
  description = "RateLimitSettings.QueueLimit — extra requests queued before returning 429."
  type        = number
  default     = 0
}

variable "request_timeout_seconds" {
  description = "RequestTimeoutSettings.DefaultTimeoutSeconds — per-request timeout (1–600)."
  type        = number
  default     = 30
}

variable "log_level" {
  type = string
}

variable "log_retention_days" {
  type = number
}
