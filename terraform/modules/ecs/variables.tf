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
  description = "Security group attached to all Fargate tasks (and the EFS mount targets)."
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

variable "pipeline_image" {
  type = string
}

variable "ollama_image" {
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

variable "ollama_cpu" {
  type = number
}

variable "ollama_memory" {
  type = number
}

variable "pipeline_cpu" {
  type = number
}

variable "pipeline_memory" {
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
  type = string
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

variable "embedding_model" {
  type = string
}

variable "embedding_dim" {
  type = number
}

variable "pipeline_version" {
  type = string
}

variable "jwt_issuer" {
  type = string
}

variable "jwt_audience" {
  type = string
}

variable "log_level" {
  type = string
}

variable "log_retention_days" {
  type = number
}
