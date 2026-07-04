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
  description = "Security group allowed to reach Redis (the ECS tasks SG)."
  type        = string
}

variable "node_type" {
  type = string
}

variable "auth_token" {
  type      = string
  sensitive = true
}
