# Inputs of the platform composition module (called from environments/{dev,prod}).

variable "project_name" {
  description = "Project slug used in resource names and the Project tag."
  type        = string
  default     = "movie-search"
}

variable "environment" {
  description = "Environment name (dev, prod); part of every resource name."
  type        = string
}

# --- Networking ---------------------------------------------------------------

variable "vpc_cidr" {
  description = "CIDR block of the VPC."
  type        = string
  default     = "10.60.0.0/16"
}

variable "az_count" {
  description = "Number of availability zones (public+private subnet pairs)."
  type        = number
  default     = 2
}

# --- Images -------------------------------------------------------------------

variable "image_tag" {
  description = "Tag of the api/mcp-server images to deploy (CD passes the commit SHA)."
  type        = string
}

# --- ECS sizing ---------------------------------------------------------------

variable "api_cpu" {
  description = "Fargate CPU units for the .NET API task."
  type        = number
  default     = 512
}

variable "api_memory" {
  description = "Fargate memory (MiB) for the .NET API task."
  type        = number
  default     = 1024
}

variable "mcp_cpu" {
  description = "Fargate CPU units for the MCP server task."
  type        = number
  default     = 512
}

variable "mcp_memory" {
  description = "Fargate memory (MiB) for the MCP server task."
  type        = number
  default     = 1024
}

variable "service_min_count" {
  description = "Autoscaling floor for the api and mcp-server services."
  type        = number
  default     = 1
}

variable "service_max_count" {
  description = "Autoscaling ceiling for the api and mcp-server services."
  type        = number
  default     = 2
}

# --- Database -----------------------------------------------------------------

variable "db_name" {
  description = "PostgreSQL database name."
  type        = string
  default     = "movies"
}

variable "db_username" {
  description = "PostgreSQL master username."
  type        = string
  default     = "movies"
}

variable "db_instance_class" {
  description = "RDS instance class."
  type        = string
  default     = "db.t4g.micro"
}

variable "db_allocated_storage" {
  description = "RDS allocated storage (GiB)."
  type        = number
  default     = 20
}

variable "db_multi_az" {
  description = "Whether the RDS instance is Multi-AZ."
  type        = bool
  default     = false
}

variable "db_deletion_protection" {
  description = "Whether RDS deletion protection is enabled (turn on in prod)."
  type        = bool
  default     = false
}

variable "db_skip_final_snapshot" {
  description = "Skip the final snapshot on destroy (dev convenience; keep false in prod)."
  type        = bool
  default     = true
}

# --- Cache --------------------------------------------------------------------

variable "redis_node_type" {
  description = "ElastiCache node type for the Redis cache."
  type        = string
  default     = "cache.t4g.micro"
}

# --- Application configuration --------------------------------------------------

variable "mcp_transport" {
  description = "MCP HTTP transport for server and API client (sse or streamable-http)."
  type        = string
  default     = "streamable-http"

  validation {
    condition     = contains(["sse", "streamable-http"], var.mcp_transport)
    error_message = "mcp_transport must be sse or streamable-http."
  }
}

variable "bedrock_region" {
  description = "Region for the Bedrock runtime; empty string uses the deployment region."
  type        = string
  default     = ""
}

variable "bedrock_embedding_model_id" {
  description = "Bedrock embedding model id (Titan Text Embeddings V2 = 1024 dims). Pipeline and MCP server must agree."
  type        = string
  default     = "amazon.titan-embed-text-v2:0"
}

variable "embedding_dim" {
  description = "Embedding dimensionality; must match the model + pgvector vector(N) column (Titan v2=1024)."
  type        = number
  default     = 1024
}

variable "jwt_issuer" {
  description = "JWT issuer claim."
  type        = string
  default     = "movie-search-platform"
}

variable "jwt_audience" {
  description = "JWT audience claim."
  type        = string
  default     = "movie-search-clients"
}

variable "log_level" {
  description = "Log level for the Python services."
  type        = string
  default     = "INFO"
}

# --- Edge ---------------------------------------------------------------------

variable "acm_certificate_arn" {
  description = "Optional ACM certificate ARN. When set, the ALB serves HTTPS on 443 and redirects HTTP; when null, HTTP-only (dev)."
  type        = string
  default     = null
}

# --- CI/CD (GitHub OIDC) --------------------------------------------------------

variable "github_repository" {
  description = "GitHub repository (org/name) allowed to assume the deploy role. Empty string disables the deploy role."
  type        = string
  default     = ""
}

variable "create_github_oidc_provider" {
  description = "Create the GitHub Actions OIDC provider (set false if the account already has one)."
  type        = bool
  default     = true
}

variable "terraform_state_bucket_arn" {
  description = "ARN of the S3 state bucket the deploy role may read/write (separately managed)."
  type        = string
  default     = ""
}

variable "terraform_lock_table_arn" {
  description = "ARN of the DynamoDB lock table the deploy role may use (separately managed)."
  type        = string
  default     = ""
}

# --- Observability ---------------------------------------------------------------

variable "log_retention_days" {
  description = "CloudWatch log retention for service logs and flow logs."
  type        = number
  default     = 30
}

variable "alarm_email" {
  description = "Optional email address subscribed to the alarm SNS topic."
  type        = string
  default     = ""
}
