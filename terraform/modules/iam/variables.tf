variable "name_prefix" {
  description = "Resource name prefix (project-environment)."
  type        = string
}

variable "managed_role_prefix" {
  description = "IAM role name prefix the deploy role may manage (the project name, spanning environments)."
  type        = string
}

variable "github_repository" {
  description = "GitHub org/name allowed to assume the deploy role; empty disables it."
  type        = string
}

variable "create_github_oidc_provider" {
  description = "Create the OIDC provider (false if the account already has one)."
  type        = bool
}

variable "ecr_repository_arns" {
  description = "Repositories the deploy role may push to."
  type        = list(string)
}

variable "terraform_state_bucket_arn" {
  description = "State bucket ARN (empty to omit the statement)."
  type        = string
}

variable "terraform_lock_table_arn" {
  description = "Lock table ARN (empty to omit the statement)."
  type        = string
}
