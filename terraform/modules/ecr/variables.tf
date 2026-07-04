variable "name_prefix" {
  description = "Resource name prefix (project-environment)."
  type        = string
}

variable "image_names" {
  description = "Repository names created under the prefix."
  type        = list(string)
  default     = ["api", "mcp-server", "pipeline"]
}
