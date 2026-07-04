variable "name_prefix" {
  description = "Resource name prefix (project-environment)."
  type        = string
}

variable "alarm_email" {
  description = "Optional email subscription for the alarm topic."
  type        = string
}

variable "cluster_name" {
  type = string
}

variable "service_names" {
  description = "ECS services to alarm on (api, mcp-server)."
  type        = list(string)
}

variable "xray_service_names" {
  description = "OpenTelemetry/X-Ray service names expected in trace documents."
  type        = list(string)
  default     = []
}

variable "alb_arn_suffix" {
  type = string
}

variable "target_group_suffix" {
  type = string
}

variable "db_instance_identity" {
  type = string
}
