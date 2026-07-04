# Terraform — AWS ECS Fargate deployment

Deploys the platform to AWS per README §12: the **.NET API** and **MCP server**
on **ECS Fargate** (each autoscaling between **1 and 2 tasks** on CPU),
embeddings served by **Amazon Bedrock**, **RDS PostgreSQL 16** (pgvector),
**ElastiCache Redis**, and a public **ALB** in front of the API. GitHub Actions
deploys through an **OIDC role** — no long-lived keys. The **data pipeline** is
not deployed to AWS; it runs locally via `docker-compose` (see the repo root
`docker-compose.yml`).

```
terraform/
├── main.tf · variables.tf · outputs.tf · versions.tf   # platform composition module
├── modules/
│   ├── networking/    # VPC, public/private subnets, NAT, SGs, VPC Flow Logs
│   ├── ecr/           # api / mcp-server repositories (scan-on-push)
│   ├── secrets/       # generated credentials in Secrets Manager
│   ├── rds/           # PostgreSQL 16 + pgvector, private subnets only
│   ├── elasticache/   # Redis 7 (AUTH + TLS), allkeys-lru
│   ├── alb/           # public ALB, /health target group, optional HTTPS
│   ├── ecs/           # cluster, Cloud Map, task defs, services, autoscaling
│   ├── iam/           # GitHub OIDC provider + deploy role
│   └── monitoring/    # CloudWatch alarms -> SNS
├── environments/
│   ├── dev/           # HTTP ALB, single-AZ db, t4g.micro sizes
│   └── prod/          # Multi-AZ db, deletion protection, t4g.small sizes
└── bootstrap/         # one-time S3 state bucket + DynamoDB lock table
```

## Traffic and data flow

```
internet ──► ALB (:80/:443) ──► api service (:8080, 1-2 tasks)
                                   │ MCP over Cloud Map DNS
                                   ▼
                       mcp-server service (:8000, 1-2 tasks)
                          │ asyncpg + pgvector       │ InvokeModel
                          ▼                          ▼
                RDS PostgreSQL 16            Amazon Bedrock (embeddings)
   api also uses: ElastiCache Redis (cache) + RDS (users table)
```

Secrets (database password, Redis AUTH token, JWT signing key) are **generated
inside the stack** and stored in Secrets Manager; task definitions reference
them via `secrets`/`valueFrom` — no credential ever appears in tfvars, source,
or plain environment variables.

## First-time setup

```bash
# 1. State backend (once per account)
cd terraform/bootstrap
terraform init
terraform apply -var state_bucket_name=<globally-unique-name>

# 2. Dev environment
cd ../environments/dev
cp backend.hcl.example backend.hcl           # fill in bootstrap outputs
cp terraform.tfvars.example terraform.tfvars # fill in repo + state ARNs
terraform init -backend-config=backend.hcl
terraform apply
```

Bootstrap **prod** the same way (`environments/prod`) before the first CD run —
CD promotes images into the prod ECR repositories, so they must already exist.

The first apply creates the ECR repositories before any image exists — the ECS
services will show failed deployments (the circuit breaker stops the thrash)
until the first CD run pushes images and re-applies with a real `image_tag`.

After the apply, wire CI/CD (see `.github/workflows/ci.yml` and `cd.yml`):

- GitHub repo **variables**: `AWS_REGION`, `AWS_DEPLOY_ROLE_ARN` (from the
  `github_deploy_role_arn` output), `TF_STATE_BUCKET`, `TF_LOCK_TABLE`.
- GitHub **environments**: `dev` and `production`, **both with required
  reviewers**. CI publishes the `terraform plan` for each environment as a
  downloadable artifact (and renders it to the run summary); CD then applies
  that exact saved plan, but only after a reviewer approves the environment —
  that approval is where you inspect the plan before anything is applied.

## Day-2 operations

```bash
# The data pipeline runs locally against a target database (not on AWS):
#   docker compose run --rm pipeline
# Point DATABASE_URL at the target RDS instance if loading a deployed database.

# Turn on HTTPS once a certificate exists
terraform apply -var acm_certificate_arn=arn:aws:acm:...
```

## Security posture

- RDS and ElastiCache live in private subnets, reachable only from the tasks SG.
- All storage encrypted (RDS, ElastiCache at-rest + in-transit, S3 state).
- VPC Flow Logs to CloudWatch; ECS Exec enabled for break-glass debugging.
- The deploy role's policy is scoped to this stack's services, **not**
  AdministratorAccess — still, have Security review it before first use, per
  organisation policy.
- Every resource is tagged `Project` / `Environment` / `ManagedBy` via provider
  `default_tags`.
