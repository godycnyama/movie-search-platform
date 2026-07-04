# Public Application Load Balancer for the .NET API. HTTP-only until an ACM
# certificate ARN is supplied, at which point 443 serves traffic and 80
# redirects (README §12).

resource "aws_lb" "this" {
  name               = "${var.name_prefix}-alb"
  load_balancer_type = "application"
  internal           = false
  subnets            = var.public_subnet_ids
  security_groups    = [var.security_group_id]

  drop_invalid_header_fields = true

  tags = { Name = "${var.name_prefix}-alb" }
}

resource "aws_lb_target_group" "api" {
  name        = "${var.name_prefix}-api"
  port        = var.target_port
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip" # Fargate awsvpc tasks register by IP

  deregistration_delay = 30

  health_check {
    path                = var.health_check_path
    matcher             = "200"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 5
  }

  tags = { Name = "${var.name_prefix}-api" }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.this.arn
  port              = 80
  protocol          = "HTTP"

  # Without a certificate: serve traffic. With one: redirect to HTTPS.
  dynamic "default_action" {
    for_each = var.acm_certificate_arn == null ? [1] : []

    content {
      type             = "forward"
      target_group_arn = aws_lb_target_group.api.arn
    }
  }

  dynamic "default_action" {
    for_each = var.acm_certificate_arn == null ? [] : [1]

    content {
      type = "redirect"

      redirect {
        port        = "443"
        protocol    = "HTTPS"
        status_code = "HTTP_301"
      }
    }
  }
}

resource "aws_lb_listener" "https" {
  count = var.acm_certificate_arn == null ? 0 : 1

  load_balancer_arn = aws_lb.this.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.acm_certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}
