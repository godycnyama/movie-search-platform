# RDS PostgreSQL 16 in private subnets. pgvector ships with RDS PostgreSQL —
# the extension itself is created by the pipeline's Alembic migrations
# (CREATE EXTENSION vector), not here. Never publicly accessible.

resource "aws_db_subnet_group" "this" {
  name       = "${var.name_prefix}-db"
  subnet_ids = var.private_subnet_ids

  tags = { Name = "${var.name_prefix}-db" }
}

resource "aws_security_group" "this" {
  name        = "${var.name_prefix}-rds"
  description = "PostgreSQL from the Fargate tasks only."
  vpc_id      = var.vpc_id

  ingress {
    description     = "PostgreSQL from ECS tasks"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [var.ingress_sg_id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.name_prefix}-rds" }
}

resource "aws_db_instance" "this" {
  identifier     = "${var.name_prefix}-postgres"
  engine         = "postgres"
  engine_version = "16"

  instance_class        = var.instance_class
  allocated_storage     = var.allocated_storage
  max_allocated_storage = var.allocated_storage * 2
  storage_type          = "gp3"
  storage_encrypted     = true

  db_name  = var.db_name
  username = var.db_username
  password = var.db_password
  port     = 5432

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [aws_security_group.this.id]
  publicly_accessible    = false
  multi_az               = var.multi_az

  backup_retention_period   = var.backup_retention_days
  deletion_protection       = var.deletion_protection
  skip_final_snapshot       = var.skip_final_snapshot
  final_snapshot_identifier = var.skip_final_snapshot ? null : "${var.name_prefix}-postgres-final"

  performance_insights_enabled = var.performance_monitoring
  auto_minor_version_upgrade   = true
  apply_immediately            = true

  tags = { Name = "${var.name_prefix}-postgres" }
}
