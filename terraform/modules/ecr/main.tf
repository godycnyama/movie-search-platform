# One ECR repository per built image. Images are scanned on push; the lifecycle
# policy keeps the last 10 tagged images so rollbacks stay possible without
# unbounded storage growth.

resource "aws_ecr_repository" "this" {
  for_each = toset(var.image_names)

  name                 = "${var.name_prefix}/${each.key}"
  image_tag_mutability = "MUTABLE" # `latest` is re-pointed by CD; SHA tags are never reused

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }
}

resource "aws_ecr_lifecycle_policy" "this" {
  for_each = aws_ecr_repository.this

  repository = each.value.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep the last 10 images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 10
        }
        action = { type = "expire" }
      }
    ]
  })
}
