variable "aws_region" {
  description = "Deployment region."
  type        = string
  default     = "eu-west-1"
}

variable "image_tag" {
  description = "Image tag to deploy (CD passes the commit SHA)."
  type        = string
}

variable "acm_certificate_arn" {
  description = "Optional ACM certificate; enables HTTPS on the ALB."
  type        = string
  default     = null
}

variable "github_repository" {
  description = "GitHub org/name allowed to deploy via OIDC (empty disables the role)."
  type        = string
  default     = ""
}

variable "create_github_oidc_provider" {
  description = "Create the GitHub OIDC provider (false if the account already has one)."
  type        = bool
  default     = true
}

variable "terraform_state_bucket_arn" {
  description = "ARN of the separately-managed S3 state bucket."
  type        = string
  default     = ""
}

variable "terraform_lock_table_arn" {
  description = "ARN of the separately-managed DynamoDB lock table."
  type        = string
  default     = ""
}

variable "alarm_email" {
  description = "Optional email for CloudWatch alarm notifications."
  type        = string
  default     = ""
}
