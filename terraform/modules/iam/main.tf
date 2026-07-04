# GitHub Actions -> AWS via OIDC: no long-lived keys. The deploy role trusts
# only the configured repository and can push images + apply this stack.
#
# NOTE for security review: `terraform apply` inherently needs wide permissions
# over the services this stack manages. The policy below is scoped to those
# services (and to project-prefixed IAM roles), not AdministratorAccess, but it
# should still be reviewed against organisation policy before first use.

data "aws_caller_identity" "current" {}

locals {
  create_role = var.github_repository != ""
}

resource "aws_iam_openid_connect_provider" "github" {
  count = local.create_role && var.create_github_oidc_provider ? 1 : 0

  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]
  # GitHub's OIDC root CA thumbprint; AWS now validates against trusted roots,
  # but the argument remains required by the provider.
  thumbprint_list = ["6938fd4d98bab03faadb97b34396831e3780aea1"]
}

locals {
  oidc_provider_arn = local.create_role ? (
    var.create_github_oidc_provider
    ? aws_iam_openid_connect_provider.github[0].arn
    : "arn:aws:iam::${data.aws_caller_identity.current.account_id}:oidc-provider/token.actions.githubusercontent.com"
  ) : ""
}

data "aws_iam_policy_document" "deploy_assume" {
  count = local.create_role ? 1 : 0

  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_repository}:*"]
    }
  }
}

resource "aws_iam_role" "deploy" {
  count = local.create_role ? 1 : 0

  name               = "${var.name_prefix}-github-deploy"
  assume_role_policy = data.aws_iam_policy_document.deploy_assume[0].json
}

data "aws_iam_policy_document" "deploy" {
  count = local.create_role ? 1 : 0

  # Image publishing.
  statement {
    sid       = "EcrLogin"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }

  statement {
    sid = "EcrPush"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:BatchGetImage",
      "ecr:CompleteLayerUpload",
      "ecr:DescribeImages",
      "ecr:DescribeRepositories",
      "ecr:GetDownloadUrlForLayer",
      "ecr:InitiateLayerUpload",
      "ecr:PutImage",
      "ecr:UploadLayerPart",
    ]
    resources = var.ecr_repository_arns
  }

  # Terraform plan/apply over the services this stack manages.
  statement {
    sid = "ManagePlatform"
    actions = [
      "application-autoscaling:*",
      "cloudwatch:*",
      "ec2:*",
      "ecr:CreateRepository",
      "ecr:DeleteLifecyclePolicy",
      "ecr:DeleteRepository",
      "ecr:GetLifecyclePolicy",
      "ecr:ListTagsForResource",
      "ecr:PutLifecyclePolicy",
      "ecr:TagResource",
      "ecr:UntagResource",
      "ecs:*",
      "elasticache:*",
      "elasticfilesystem:*",
      "elasticloadbalancing:*",
      "logs:*",
      "rds:*",
      "secretsmanager:*",
      "servicediscovery:*",
      "sns:*",
    ]
    resources = ["*"]
  }

  # IAM is restricted to this project's roles and the OIDC provider read.
  statement {
    sid = "ManageProjectRoles"
    actions = [
      "iam:AttachRolePolicy",
      "iam:CreateRole",
      "iam:DeleteRole",
      "iam:DeleteRolePolicy",
      "iam:DetachRolePolicy",
      "iam:GetRole",
      "iam:GetRolePolicy",
      "iam:ListAttachedRolePolicies",
      "iam:ListInstanceProfilesForRole",
      "iam:ListRolePolicies",
      "iam:PassRole",
      "iam:PutRolePolicy",
      "iam:TagRole",
      "iam:UntagRole",
      "iam:UpdateAssumeRolePolicy",
    ]
    # Project-wide (not environment-scoped) so the one deploy role can apply
    # both the dev and prod stacks.
    resources = ["arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/${var.managed_role_prefix}-*"]
  }

  statement {
    sid = "ReadOidcProvider"
    actions = [
      "iam:GetOpenIDConnectProvider",
      "iam:ListOpenIDConnectProviders",
    ]
    resources = ["*"]
  }

  # Remote state.
  dynamic "statement" {
    for_each = var.terraform_state_bucket_arn != "" ? [1] : []

    content {
      sid = "TerraformState"
      actions = [
        "s3:GetObject",
        "s3:ListBucket",
        "s3:PutObject",
      ]
      resources = [
        var.terraform_state_bucket_arn,
        "${var.terraform_state_bucket_arn}/*",
      ]
    }
  }

  dynamic "statement" {
    for_each = var.terraform_lock_table_arn != "" ? [1] : []

    content {
      sid = "TerraformLock"
      actions = [
        "dynamodb:DeleteItem",
        "dynamodb:GetItem",
        "dynamodb:PutItem",
      ]
      resources = [var.terraform_lock_table_arn]
    }
  }
}

resource "aws_iam_role_policy" "deploy" {
  count = local.create_role ? 1 : 0

  name   = "deploy-platform"
  role   = aws_iam_role.deploy[0].id
  policy = data.aws_iam_policy_document.deploy[0].json
}
