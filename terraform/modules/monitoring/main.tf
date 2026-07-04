# CloudWatch alarms -> SNS. Complements the in-cluster Prometheus/Grafana stack
# (monitoring/) which handles application-level metrics locally; these alarms
# cover the AWS-managed signals in the deployed environments.

resource "aws_sns_topic" "alarms" {
  name = "${var.name_prefix}-alarms"
}

resource "aws_sns_topic_subscription" "email" {
  count = var.alarm_email != "" ? 1 : 0

  topic_arn = aws_sns_topic.alarms.arn
  protocol  = "email"
  endpoint  = var.alarm_email
}

# --- ECS services: CPU / memory ------------------------------------------------------

resource "aws_cloudwatch_metric_alarm" "service_cpu" {
  for_each = toset(var.service_names)

  alarm_name          = "${each.key}-cpu-high"
  alarm_description   = "Sustained CPU above 85% — the service is likely pinned at max capacity."
  namespace           = "AWS/ECS"
  metric_name         = "CPUUtilization"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 85
  comparison_operator = "GreaterThanThreshold"

  dimensions = {
    ClusterName = var.cluster_name
    ServiceName = each.key
  }

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]
}

resource "aws_cloudwatch_metric_alarm" "service_memory" {
  for_each = toset(var.service_names)

  alarm_name          = "${each.key}-memory-high"
  alarm_description   = "Sustained memory above 85% — risk of OOM task kills."
  namespace           = "AWS/ECS"
  metric_name         = "MemoryUtilization"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 85
  comparison_operator = "GreaterThanThreshold"

  dimensions = {
    ClusterName = var.cluster_name
    ServiceName = each.key
  }

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]
}

# --- ALB: errors and target health ------------------------------------------------------

resource "aws_cloudwatch_metric_alarm" "alb_5xx" {
  alarm_name          = "${var.name_prefix}-alb-5xx"
  alarm_description   = "Elevated 5xx responses from the API."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "HTTPCode_Target_5XX_Count"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 10
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions = {
    LoadBalancer = var.alb_arn_suffix
  }

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]
}

resource "aws_cloudwatch_metric_alarm" "alb_unhealthy_hosts" {
  alarm_name          = "${var.name_prefix}-alb-unhealthy-hosts"
  alarm_description   = "One or more API targets failing the /health check."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "UnHealthyHostCount"
  statistic           = "Maximum"
  period              = 60
  evaluation_periods  = 5
  threshold           = 0
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"

  dimensions = {
    LoadBalancer = var.alb_arn_suffix
    TargetGroup  = var.target_group_suffix
  }

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]
}

# --- RDS -----------------------------------------------------------------------------

resource "aws_cloudwatch_metric_alarm" "rds_cpu" {
  alarm_name          = "${var.name_prefix}-rds-cpu-high"
  alarm_description   = "Database CPU above 85% for 10 minutes."
  namespace           = "AWS/RDS"
  metric_name         = "CPUUtilization"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  threshold           = 85
  comparison_operator = "GreaterThanThreshold"

  dimensions = {
    DBInstanceIdentifier = var.db_instance_identity
  }

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]
}

resource "aws_cloudwatch_metric_alarm" "rds_storage" {
  alarm_name          = "${var.name_prefix}-rds-storage-low"
  alarm_description   = "Under 2 GiB of free database storage."
  namespace           = "AWS/RDS"
  metric_name         = "FreeStorageSpace"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 1
  threshold           = 2 * 1024 * 1024 * 1024
  comparison_operator = "LessThanThreshold"

  dimensions = {
    DBInstanceIdentifier = var.db_instance_identity
  }

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]
}
