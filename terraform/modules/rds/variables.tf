variable "name_prefix" {
  description = "Resource name prefix (project-environment)."
  type        = string
}

variable "vpc_id" {
  type = string
}

variable "private_subnet_ids" {
  type = list(string)
}

variable "ingress_sg_id" {
  description = "Security group allowed to reach PostgreSQL (the ECS tasks SG)."
  type        = string
}

variable "db_username" {
  type = string
}

variable "db_password" {
  type      = string
  sensitive = true
}

variable "instance_class" {
  type = string
}

variable "allocated_storage" {
  type = number
}

variable "multi_az" {
  type = bool
}

variable "deletion_protection" {
  type = bool
}

variable "skip_final_snapshot" {
  type = bool
}

variable "backup_retention_days" {
  type = number
}

variable "performance_monitoring" {
  description = "Enable Performance Insights."
  type        = bool
}
