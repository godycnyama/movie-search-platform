variable "name_prefix" {
  description = "Resource name prefix (project-environment)."
  type        = string
}

variable "vpc_id" {
  type = string
}

variable "public_subnet_ids" {
  type = list(string)
}

variable "security_group_id" {
  type = string
}

variable "target_port" {
  type = number
}

variable "health_check_path" {
  type = string
}

variable "acm_certificate_arn" {
  description = "Optional certificate; enables HTTPS + HTTP redirect when set."
  type        = string
  default     = null
}
