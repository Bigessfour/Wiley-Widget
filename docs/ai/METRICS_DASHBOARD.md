# AI Metrics Dashboard Configuration

**Version**: 1.0
**Last Updated**: 2026-01-03

## 📊 Overview

This guide provides complete configuration for monitoring WileyWidget's AI ecosystem using:

- **OpenTelemetry** - Metrics collection
- **SigNoz** - Observability platform
- **Grafana** - Dashboards and alerting (optional alternative)

---

## 🔧 SigNoz Configuration (Recommended)

### Installation

```bash
# Install SigNoz with Docker Compose
git clone <https://github.com/SigNoz/signoz.git>
cd signoz/deploy/

docker compose -f docker/clickhouse-setup/docker-compose.yaml up -d
```

### OpenTelemetry Configuration

**appsettings.json**:

```json
{
  "OpenTelemetry": {
    "ServiceName": "WileyWidget",

    "ServiceVersion": "1.0.0",
    "Exporters": {
      "OTLP": {
        "Endpoint": "http://localhost:4317",
        "Protocol": "grpc"
      }
    }
  },
  "SigNoz": {
    "Enabled": true,
    "Endpoint": "http://localhost:4317",
    "ServiceName": "WileyWidget"

### WileyWidget Integration

builder.Services.AddSingleton<SigNozTelemetryService>();

// Metrics are automatically collected:
// - ai.xai.request.duration (histogram)
// - ai.xai.request.total (counter)
// - ai.xai.cache.hits (counter)
// - ai.xai.cache.misses (counter)
// - ai.xai.errors.total (counter)
```

---

## 📈 Key Metrics to Monitor

### 1. Request Metrics

| Metric                        | Type      | Description                 | Alert Threshold |
| ----------------------------- | --------- | --------------------------- | --------------- |
| `ai_request_duration_seconds` | Histogram | Time to complete AI request | p95 > 5s        |
| `ai_request_total`            | Counter   | Total AI requests           | N/A             |
| `ai_concurrent_requests`      | Gauge     | Current concurrent requests | > 8             |
| `ai_request_errors_total`     | Counter   | Failed requests             | Rate > 5%       |

### 2. Cache Metrics

| Metric                  | Type    | Description        | Alert Threshold |
| ----------------------- | ------- | ------------------ | --------------- |
| `ai_cache_hits_total`   | Counter | Cache hits         | N/A             |
| `ai_cache_misses_total` | Counter | Cache misses       | N/A             |
| `ai_cache_hit_rate`     | Gauge   | Hit rate (%)       | < 50%           |
| `ai_cache_size_bytes`   | Gauge   | Cache memory usage | > 100MB         |

### 3. Cost Metrics

| Metric                       | Type    | Description           | Alert Threshold |
| ---------------------------- | ------- | --------------------- | --------------- |
| `ai_prompt_tokens_total`     | Counter | Tokens in prompts     | N/A             |
| `ai_completion_tokens_total` | Counter | Tokens in completions | N/A             |
| `ai_estimated_cost_usd`      | Counter | Estimated API costs   | Daily > $10     |

### 4. Resilience Metrics

| Metric                     | Type    | Description                                          | Alert Threshold |
| -------------------------- | ------- | ---------------------------------------------------- | --------------- |
| `ai_circuit_breaker_state` | Gauge   | Circuit breaker state (0=Closed, 1=Open, 2=HalfOpen) | 1 (Open)        |
| `ai_timeout_count_total`   | Counter | Timeout occurrences                                  | Rate > 10%      |
| `ai_fallback_count_total`  | Counter | Fallback invocations                                 | Rate > 10%      |

---

## 🎨 SigNoz Dashboard Setup

### 1. Create Dashboard

1. Log in to SigNoz: `<http://localhost:3301>`
2. Navigate to **Dashboards** → **New Dashboard**
3. Name: **WileyWidget AI Ecosystem**

### 2. Add Panels

#### Panel 1: Request Rate & Latency

**Query**:

```promql
# Request Rate
rate(ai_request_total[5m])

# P95 Latency

histogram_quantile(0.95, rate(ai_request_duration_seconds_bucket[5m]))

# P99 Latency
histogram_quantile(0.99, rate(ai_request_duration_seconds_bucket[5m]))
```

**Visualization**: Time series graph with dual Y-axis

#### Panel 2: Cache Performance

**Query**:

```promql
# Cache Hit Rate
(
  rate(ai_cache_hits_total[5m]) /
  (rate(ai_cache_hits_total[5m]) + rate(ai_cache_misses_total[5m]))

) * 100
```

**Visualization**: Gauge (target: >60%)

#### Panel 3: Error Rate

**Query**:

```promql
# Error Rate (%)
(
  rate(ai_request_errors_total[5m]) /
  rate(ai_request_total[5m])

) * 100
```

**Visualization**: Stat panel with thresholds (green <5%, yellow 5-10%, red >10%)

#### Panel 4: Cost Tracking

**Query**:

```promql
# Daily Estimated Cost
increase(ai_estimated_cost_usd[24h])

# Token Usage

rate(ai_prompt_tokens_total[1h])
rate(ai_completion_tokens_total[1h])
```

**Visualization**: Bar chart grouped by hour

#### Panel 5: Circuit Breaker Status

**Query**:

```promql
ai_circuit_breaker_state
```

**Visualization**: Stat panel (0=Healthy, 1=Unhealthy, 2=Recovering)

#### Panel 6: Concurrent Requests

**Query**:

```promql
ai_concurrent_requests
```

**Visualization**: Time series with threshold line at 8

---

## 🔔 Alerting Configuration

### SigNoz Alerts

#### Alert 1: High Latency

```yaml
name: AI High Latency
query: histogram_quantile(0.95, rate(ai_request_duration_seconds_bucket[5m])) > 5
duration: 5m
severity: warning

message: "95th percentile AI response time exceeded 5 seconds"
```

#### Alert 2: Low Cache Hit Rate

```yaml
name: AI Low Cache Hit Rate
query: |
  (rate(ai_cache_hits_total[10m]) /
   (rate(ai_cache_hits_total[10m]) + rate(ai_cache_misses_total[10m]))) < 0.5

duration: 10m
severity: warning
message: "AI cache hit rate below 50%"
```

#### Alert 3: High Error Rate

```yaml
name: AI High Error Rate
query: |
  (rate(ai_request_errors_total[5m]) /
   rate(ai_request_total[5m])) > 0.1

duration: 5m
severity: critical
message: "AI error rate exceeded 10%"
```

#### Alert 4: Circuit Breaker Open

```yaml
name: AI Circuit Breaker Open
query: ai_circuit_breaker_state == 1
duration: 1m
severity: critical

message: "AI circuit breaker is OPEN - service unavailable"
```

#### Alert 5: Cost Budget Exceeded

```yaml
name: AI Daily Cost Exceeded
query: increase(ai_estimated_cost_usd[24h]) > 10
duration: 1m
severity: warning

message: "AI daily cost exceeded $10 budget"
```

---

## 🎯 Grafana Configuration (Alternative)

### Prometheus Configuration

**prometheus.yml**:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: "wileywidget"
    static_configs:
      - targets: ["localhost:5000"]
    metrics_path: "/metrics"
```

### Grafana Dashboard JSON

```json
{
  "dashboard": {
    "title": "WileyWidget AI Metrics",
    "panels": [
      {
        "id": 1,
        "title": "AI Request Rate",
        "targets": [
          {
            "expr": "rate(ai_request_total[5m])",
            "legendFormat": "Requests/sec"
          }
        ],
        "type": "graph"
      },
      {
        "id": 2,
        "title": "Cache Hit Rate",
        "targets": [
          {
            "expr": "(rate(ai_cache_hits_total[5m]) / (rate(ai_cache_hits_total[5m]) + rate(ai_cache_misses_total[5m]))) * 100",
            "legendFormat": "Hit Rate %"
          }
        ],
        "type": "gauge",
        "thresholds": [
          { "value": 50, "color": "red" },
          { "value": 60, "color": "yellow" },
          { "value": 70, "color": "green" }
        ]
      }
    ]
  }
}
```

---

## 🚀 Quick Start

### 1. Verify Metrics Endpoint

```bash
# Check metrics are being exported
curl http://localhost:5000/metrics

# Expected output:

# ai_request_duration_seconds_bucket{le="0.5"} 42
# ai_request_total 156
# ai_cache_hits_total 95
# ...
```

### 2. Start SigNoz

```bash

cd signoz/deploy/docker/clickhouse-setup
docker compose up -d
```

### 3. Configure WileyWidget

Ensure **appsettings.json** has SigNoz enabled:

```json
{
  "SigNoz": {
    "Enabled": true,
    "Endpoint": "http://localhost:4317"
  }
}
```

### 4. Import Dashboard

1. Open SigNoz: `<http://localhost:3301>`

2. Create dashboard following panel configurations above
3. Save dashboard

### 5. Verify Data Flow

```bash
# Generate test load
curl -X POST http://localhost:5000/api/ai/insights \
  -H "Content-Type: application/json" \
  -d '{"context": "Test", "question": "What is the budget?"}'


# Check SigNoz dashboard refreshes with new data
```

---

## 📊 Example Queries

### Top 5 Slowest Requests

```promql
topk(5, ai_request_duration_seconds)
```

### Cache Efficiency by Service

```promql

sum(rate(ai_cache_hits_total[5m])) by (service)
/
sum(rate(ai_cache_misses_total[5m])) by (service)
```

### Hourly Token Usage

```promql

increase(ai_prompt_tokens_total[1h])
+ increase(ai_completion_tokens_total[1h])
```

### Cost Per Request

```promql

rate(ai_estimated_cost_usd[5m])
/
rate(ai_request_total[5m])
```

---

## 🛠️ Troubleshooting

### Metrics Not Appearing

1. **Check OpenTelemetry config**:

# Verify endpoint is reachable

curl -v <http://localhost:4317>

```bash
# Verify endpoint is reachable
curl -v http://localhost:4317
```

1. **Check service logs**:

# Look for telemetry initialization

grep "SigNozTelemetryService" logs/startup-\*.txt

```bash

# Look for telemetry initialization
grep "SigNozTelemetryService" logs/startup-*.txt
```

1. **Verify metrics endpoint**:

curl <http://localhost:5000/metrics>

```bash
curl http://localhost:5000/metrics
```

## High Cardinality Issues

If too many unique metric labels:

- Limit label values (e.g., don't use request IDs as labels)
- Use exemplars for high-cardinality data
- Aggregate metrics before export

### Dashboard Not Updating

- Check Prometheus scrape interval (15s default)
- Verify time range in dashboard (use "Last 5 minutes")
- Clear browser cache
- Check SigNoz collector logs: `docker logs signoz-otel-collector`

<http://localhost:3301>
<http://localhost:4317>
<http://localhost:5000/metrics>

---

**Document Version**: 1.0
**Platform**: SigNoz / Grafana + Prometheus
**Maintainer**: WileyWidget Development Team
