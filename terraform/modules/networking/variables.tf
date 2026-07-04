variable "name_prefix" {
  description = "Resource name prefix (project-environment)."
  type        = string
}

variable "vpc_cidr" {
  description = "CIDR block of the VPC."
  type        = string
}

variable "az_count" {
  description = "Number of availability zones."
  type        = number
}

variable "log_retention_days" {
  description = "Retention for the VPC flow log group."
  type        = number
}
