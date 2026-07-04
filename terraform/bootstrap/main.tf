# One-time bootstrap: the S3 bucket + DynamoDB table that hold Terraform's
# remote state and locks for the environments. Run once with local state:
#
#   cd terraform/bootstrap
#   terraform init && terraform apply -var state_bucket_name=<globally-unique-name>
#
# Then wire the outputs into environments/<env>/backend.hcl.

terraform {
  required_version = ">= 1.9.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.80"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "movie-search"
      Environment = "shared"
      ManagedBy   = "terraform"
    }
  }
}

variable "aws_region" {
  description = "Region for the state resources."
  type        = string
  default     = "eu-west-1"
}

variable "state_bucket_name" {
  description = "Globally unique name for the Terraform state bucket."
  type        = string
}

variable "lock_table_name" {
  description = "Name of the DynamoDB lock table."
  type        = string
  default     = "movie-search-terraform-locks"
}

resource "aws_s3_bucket" "state" {
  bucket = var.state_bucket_name
}

resource "aws_s3_bucket_versioning" "state" {
  bucket = aws_s3_bucket.state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "state" {
  bucket = aws_s3_bucket.state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "aws:kms"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "state" {
  bucket = aws_s3_bucket.state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_dynamodb_table" "locks" {
  name         = var.lock_table_name
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "LockID"

  attribute {
    name = "LockID"
    type = "S"
  }
}

output "state_bucket_name" {
  value = aws_s3_bucket.state.bucket
}

output "state_bucket_arn" {
  value = aws_s3_bucket.state.arn
}

output "lock_table_name" {
  value = aws_dynamodb_table.locks.name
}

output "lock_table_arn" {
  value = aws_dynamodb_table.locks.arn
}
