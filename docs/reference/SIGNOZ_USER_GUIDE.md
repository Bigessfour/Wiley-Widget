# SigNoz Observability User Guide for Wiley Widget

## Table of Contents

1. [Introduction](#introduction)
2. [Architecture Overview](#architecture-overview)
3. [Initial Setup](#initial-setup)
4. [Daily Operations](#daily-operations)
5. [Monitoring & Dashboards](#monitoring--dashboards)
6. [Troubleshooting](#troubleshooting)
7. [Best Practices](#best-practices)
8. [Advanced Configuration](#advanced-configuration)

---

## Introduction

SigNoz is an open-source observability platform that provides:

- **Distributed Tracing**: Track requests across services
- **Metrics**: Monitor application and infrastructure performance
- **Logs**: Centralized log aggregation and analysis
- **100% Free**: Self-hosted with no vendor lock-in

### Why SigNoz for Wiley Widget?

- **Comprehensive Coverage**: Automatically instruments .NET, EF Core, HTTP clients
- **Real-time Insights**: Immediate visibility into application behavior
- **Cost-Effective**: No per-seat or data volume charges
- **Privacy**: All data stays on your infrastructure

---

## Architecture Overview

### Component Diagram

```
Wiley Widget Application
        ↓ (OTLP gRPC - Port 4317)
OpenTelemetry Collector
        ↓
ClickHouse Database ← Query Service ← Frontend (Port 3301)
```

### Components

1. **Wiley Widget Application**
   - Instrumented with OpenTelemetry SDK
   - Sends traces, metrics, and logs via OTLP protocol
   - Port: Sends to 4317 (gRPC)

2. **OpenTelemetry Collector**
   - Receives telemetry data from the application
   - Processes and enriches data
   - Forwards to ClickHouse
   - Ports: 4317 (gRPC), 4318 (HTTP), 8888 (metrics)

3. **ClickHouse Database**
   - Stores all telemetry data (traces, metrics, logs)
   - High-performance columnar database
   - Ports: 9000 (native), 8123 (HTTP)

4. **Query Service**
   - Queries ClickHouse for dashboard data
   - Provides API for frontend
   - Port: 8080

5. **Frontend (Web UI)**
   - User-facing dashboard
   - Visualizes traces, metrics, and logs
   - Port: 3301
   - **Access**: http://localhost:3301

6. **Alertmanager**
   - Manages alerts and notifications
   - Port: 9093

---

## Initial Setup

### Prerequisites

- **Docker Desktop**: Installed and running
- **PowerShell 7.5+**: For management scripts
- **4GB RAM**: Available for Docker containers
- **10GB Disk**: For telemetry data storage

### Step 1: Verify Docker Installation

```powershell
docker --version
docker compose version
```

Expected output:

```
Docker version 24.0.x
Docker Compose version v2.x.x
```

### Step 2: Start SigNoz

Navigate to the Wiley Widget directory and run:

```powershell
.\scripts\manage-signoz.ps1 -Action setup -WaitForReady
```

This will:

1. Pull Docker images (~2-3 GB download)
2. Start all SigNoz containers
3. Wait for services to be ready (~2-3 minutes)
4. Display access URLs

**Expected Output:**

```
🔍 SigNoz Management Script
Action: setup
✅ Docker installed: Docker version 24.0.x
✅ Docker Compose file found
📦 Pulling SigNoz Docker images...
🚀 Starting SigNoz containers...
⏳ Waiting for SigNoz to be ready...
✅ Frontend ready
✅ Query Service ready
✅ OTLP gRPC endpoint ready
✅ SigNoz is fully ready!

🎉 SigNoz Self-Hosted Setup Complete!

📊 Access SigNoz Dashboard:
   Web UI: http://localhost:3301

🔗 Wiley Widget Integration:
   OTLP gRPC: http://localhost:4317
   OTLP HTTP: http://localhost:4318
```

### Step 3: Verify Installation

Open your browser and navigate to:

```
http://localhost:3301
```

You should see the SigNoz dashboard. On first launch, you may be prompted to:

1. Create an admin account (optional for local setup)
2. Choose your preferred time zone
3. Complete the onboarding tour

### Step 4: Run Wiley Widget

Start the Wiley Widget application normally. It will automatically begin sending telemetry to SigNoz.

Within 30 seconds, you should see:

- Service "wiley-widget" appearing in the Services tab
- Traces appearing in the Traces tab
- Metrics in the Dashboard tab

---

## Daily Operations

### Starting SigNoz

```powershell
.\scripts\manage-signoz.ps1 -Action start
```

**When to use**: Every time you restart your computer or need to use Wiley Widget.

### Stopping SigNoz

```powershell
.\scripts\manage-signoz.ps1 -Action stop
```

**When to use**: When done working for the day to free up system resources.

### Checking Status

```powershell
.\scripts\manage-signoz.ps1 -Action status
```

**Output shows**:

- Container health status
- Uptime
- Port mappings
- Service URLs

### Viewing Logs

```powershell
.\scripts\manage-signoz.ps1 -Action logs
```

**Shows real-time logs** from all SigNoz containers. Press `Ctrl+C` to exit.

### Restarting SigNoz

```powershell
.\scripts\manage-signoz.ps1 -Action restart
```

**When to use**: After configuration changes or if services become unresponsive.

---

## Monitoring & Dashboards

### Accessing the Dashboard

1. Open browser: http://localhost:3301
2. Navigate using the left sidebar

### Main Sections

#### 1. **Services** Tab

**What it shows**: All instrumented services (wiley-widget, etc.)

**Key Metrics**:

- **Request Rate**: Requests per second
- **Error Rate**: Percentage of failed requests
- **P99 Latency**: 99th percentile response time
- **Apdex Score**: Application performance index

**How to use**:

1. Click on "wiley-widget" service
2. View service-level metrics and traces
3. Identify slow operations
4. Track error patterns

#### 2. **Traces** Tab

**What it shows**: Distributed traces of requests through the application

**Key Features**:

- **Flame Graphs**: Visualize request spans
- **Trace Search**: Filter by service, operation, duration, status
- **Span Details**: See exact timing and tags for each operation

**Common Filters**:

- **Status Code**: `http.status_code=500` (find errors)
- **Duration**: `durationNano > 1000000000` (> 1 second)
- **Service**: `serviceName=wiley-widget`
- **Operation**: `name=BudgetRepository.GetByFiscalYear`

**How to investigate slow requests**:

1. Go to Traces tab
2. Sort by duration (longest first)
3. Click on a slow trace
4. Examine the flame graph to find bottlenecks
5. Look for:
   - Database queries taking too long
   - External API calls timing out
   - Lock contention or retries

#### 3. **Metrics** Tab

**What it shows**: Time-series metrics for infrastructure and application

**Default Metrics Available**:

**Application Metrics**:

- `http.server.duration` - Request duration
- `http.server.request.count` - Total requests
- `db.client.operation.duration` - Database operation time
- `dotnet.process.cpu.time` - CPU usage
- `dotnet.process.memory.usage` - Memory usage

**Infrastructure Metrics** (from hostmetrics receiver):

- `system.cpu.utilization` - CPU usage %
- `system.memory.utilization` - Memory usage %
- `system.disk.io` - Disk I/O
- `system.network.io` - Network I/O

**Creating Custom Dashboards**:

1. Click "Dashboard" → "New Dashboard"
2. Add panels with queries:

   ```
   # Example: Average request duration by endpoint
   avg(http.server.duration) by http.route

   # Example: Error rate
   sum(http.server.request.count{http.status_code >= 400})
   / sum(http.server.request.count) * 100
   ```

#### 4. **Logs** Tab

**What it shows**: Centralized logs from Wiley Widget with structured fields

**Log Integration**: Serilog logs are automatically sent via OpenTelemetry

**Search Examples**:

```
# Find errors
level=error

# Find logs for specific user
user_id=12345

# Find database-related logs
SourceContext=*Repository*

# Find logs in time range
timestamp:[2025-11-07T10:00:00Z TO 2025-11-07T12:00:00Z]
```

**Correlation**: Click on a log entry to see related traces

#### 5. **Alerts** Tab

**What it shows**: Configured alerts and their status

**Creating an Alert**:

1. Go to Alerts → New Alert
2. Choose metric: e.g., `http.server.duration`
3. Set condition: `p99 > 1000ms for 5 minutes`
4. Configure notification channel (Slack, email, webhook)
5. Save and enable

**Common Alert Examples**:

**High Error Rate**:

```yaml
Metric: http.server.request.count{http.status_code >= 500}
Condition: rate > 5 per minute
Threshold: For 3 consecutive minutes
```

**Slow Database Queries**:

```yaml
Metric: db.client.operation.duration
Condition: p95 > 500ms
Threshold: For 5 minutes
```

**High Memory Usage**:

```yaml
Metric: dotnet.process.memory.usage
Condition: > 1GB
Threshold: For 10 minutes
```

---

## Monitoring Wiley Widget Operations

### Key Operations to Monitor

#### 1. **Database Operations**

**Where to look**: Traces tab → Filter by `db.system=sqlserver`

**What to monitor**:

- Query duration (should be < 100ms for simple queries)
- Connection pool usage
- Transaction rollbacks

**Example Trace Tags**:

```
db.system: sqlserver
db.name: WileyWidgetDev
db.operation: SELECT
db.wiley.operation_type: Text
db.connection_type: sql_server
```

**Red flags**:

- Queries > 1 second
- Many retries (indicates transient errors)
- Connection timeout errors

#### 2. **External API Calls**

**Where to look**: Traces tab → Filter by `http.client`

**APIs to monitor**:

- xAI API calls (OpenAI-compatible)
- QuickBooks Online API
- Secret Vault access

**Example metrics**:

```
http.client.request.duration{http.url=*api.x.ai*}
```

**Red flags**:

- Latency > 5 seconds
- High error rates (429 = rate limited, 500 = server error)
- Retry storms

#### 3. **Repository Operations**

**Where to look**: Traces tab → Operation name contains "Repository"

**Example operations**:

- `BudgetRepository.GetByFiscalYear`
- `BudgetRepository.AddAsync`
- `DepartmentRepository.GetAllAsync`

**What to check**:

- Cache hit rate (look for `cache.hit=true` tag)
- Result count (ensure reasonable query results)
- Error handling

#### 4. **Prism Module Loading**

**Where to look**: Traces tab → Filter by `module.`

**What to monitor**:

- Module initialization time
- DI container resolution performance
- Module load order

**Example traces**:

```
module.load: QuickBooksModule
module.status: initialized
module.duration: 250ms
```

#### 5. **Resilience Policies**

**Where to look**: Traces tab → Tags with `resilience.`

**What to monitor**:

- Retry attempts
- Circuit breaker state
- Timeout occurrences

**Example tags**:

```
resilience.policy: DatabaseRetry
resilience.retry_attempt: 2
resilience.circuit_breaker_state: open
```

---

## Troubleshooting

### Issue: SigNoz Containers Won't Start

**Symptoms**:

- `docker compose up` fails
- Containers repeatedly restart
- Health checks failing

**Solutions**:

1. **Check Docker resources**:

   ```powershell
   # Docker Desktop → Settings → Resources
   # Ensure: 4GB RAM, 2 CPUs minimum
   ```

2. **Check port conflicts**:

   ```powershell
   # See if ports are already in use
   netstat -ano | findstr "4317 3301 9000 8123"
   ```

3. **View container logs**:

   ```powershell
   docker compose -f .\docker\docker-compose.signoz.yml logs clickhouse
   docker compose -f .\docker\docker-compose.signoz.yml logs otel-collector
   ```

4. **Clean start**:
   ```powershell
   .\scripts\manage-signoz.ps1 -Action stop
   docker system prune -f
   .\scripts\manage-signoz.ps1 -Action start
   ```

### Issue: No Data Appearing in SigNoz

**Symptoms**:

- Dashboard is empty
- No traces appearing
- Service not listed

**Solutions**:

1. **Verify Wiley Widget is sending data**:
   - Check application logs for "SigNoz telemetry initialized"
   - Look for any OpenTelemetry errors

2. **Check OTel Collector logs**:

   ```powershell
   docker logs signoz-otel-collector | Select-String "error" -Context 5
   ```

3. **Test connectivity**:

   ```powershell
   # From host machine
   Test-NetConnection -ComputerName localhost -Port 4317
   ```

4. **Verify configuration**:
   - Check `appsettings.json` → `SigNoz:Endpoint` = "http://localhost:4317"
   - Ensure `SigNoz:EnableTracing` = true

5. **Enable console exporter** (for debugging):
   - Check application console output for trace data
   - Look for Activity IDs in logs

### Issue: Dashboard is Slow

**Symptoms**:

- Long loading times
- Timeout errors
- Unresponsive UI

**Solutions**:

1. **Check ClickHouse performance**:

   ```powershell
   docker exec -it signoz-clickhouse clickhouse-client --query "SELECT count() FROM signoz.signoz_traces"
   ```

2. **Reduce data retention**:
   - Default: 15 days
   - Adjust in dashboard: Settings → Data Retention

3. **Optimize queries**:
   - Use smaller time ranges
   - Apply filters before loading large datasets

4. **Increase Docker resources**:
   - Docker Desktop → Settings → Resources
   - Increase RAM to 6-8GB if available

### Issue: High Memory Usage

**Symptoms**:

- Docker using > 4GB RAM
- System slowdown
- Out of memory errors

**Solutions**:

1. **Reduce batch size** in `otel-collector-config.yaml`:

   ```yaml
   processors:
     batch:
       send_batch_size: 512 # Reduced from 1024
   ```

2. **Lower memory limit**:

   ```yaml
   processors:
     memory_limiter:
       limit_mib: 256 # Reduced from 512
   ```

3. **Clean old data**:
   ```powershell
   # Delete traces older than 7 days
   docker exec signoz-clickhouse clickhouse-client --query "
   OPTIMIZE TABLE signoz.signoz_traces FINAL"
   ```

### Issue: Connection Refused Errors

**Error**: `Failed to connect to localhost:4317`

**Solutions**:

1. **Verify SigNoz is running**:

   ```powershell
   .\scripts\manage-signoz.ps1 -Action status
   ```

2. **Check firewall**:

   ```powershell
   # Windows Firewall might block localhost connections
   # Add exception for ports 4317, 3301
   ```

3. **Try alternative endpoint**:
   - Change `appsettings.json`:
   ```json
   "SigNoz": {
     "Endpoint": "http://127.0.0.1:4317"  # Instead of localhost
   }
   ```

---

## Best Practices

### 1. Development Workflow

**Morning Routine**:

```powershell
# 1. Start SigNoz
.\scripts\manage-signoz.ps1 -Action start -WaitForReady

# 2. Open dashboard
start http://localhost:3301

# 3. Start Wiley Widget
dotnet run --project WileyWidget.csproj
```

**End of Day**:

```powershell
# Stop SigNoz to free resources
.\scripts\manage-signoz.ps1 -Action stop
```

### 2. Performance Optimization

**Sample Rate in Production**:

```json
// appsettings.Production.json
"SigNoz": {
  "SampleRatio": 0.1  // Only capture 10% of traces
}
```

**Benefits**:

- Reduces overhead
- Lower storage requirements
- Still provides statistical accuracy

**When to use 100% sampling**:

- Development environments
- Debugging specific issues
- Low-traffic applications

### 3. Alert Configuration

**Start with these critical alerts**:

1. **Application Down**:

   ```yaml
   Metric: up{service="wiley-widget"}
   Condition: == 0
   Threshold: For 2 minutes
   ```

2. **High Error Rate**:

   ```yaml
   Metric: http.server.request.count{status_code >= 500}
   Condition: > 5% of requests
   Threshold: For 5 minutes
   ```

3. **Database Connection Pool Exhaustion**:
   ```yaml
   Metric: db.client.connections.usage
   Condition: > 90%
   Threshold: For 3 minutes
   ```

### 4. Dashboard Organization

**Create role-specific dashboards**:

**Developer Dashboard**:

- Recent errors
- Slow database queries
- API latencies
- Exception trends

**Operations Dashboard**:

- System resource usage
- Request throughput
- Error rates by service
- SLA compliance

**Business Dashboard**:

- Request volume by feature
- User activity patterns
- API usage statistics

### 5. Data Retention

**Recommended settings**:

- **Traces**: 7 days (detailed request flows)
- **Metrics**: 30 days (performance trends)
- **Logs**: 14 days (debugging history)

**Why limit retention**:

- Reduces storage costs
- Improves query performance
- Easier to find recent issues

---

## Advanced Configuration

### Custom Attributes

Add application-specific tags to all telemetry:

```csharp
// In SigNozTelemetryService.Initialize()
var resource = ResourceBuilder.CreateDefault()
    .AddService(ServiceName, ServiceVersion)
    .AddAttributes(new[]
    {
        new KeyValuePair<string, object>("environment", environment),
        new KeyValuePair<string, object>("datacenter", "us-east-1"),
        new KeyValuePair<string, object>("deployment.version", "2.1.0"),
        new KeyValuePair<string, object>("team", "platform")
    });
```

### Custom Instrumentation

Add telemetry to specific operations:

```csharp
public async Task ProcessBudget(int fiscalYear)
{
    using var activity = SigNozTelemetryService.ActivitySource
        .StartActivity("ProcessBudget");

    activity?.SetTag("fiscal_year", fiscalYear);
    activity?.SetTag("operation.type", "business_logic");

    try
    {
        // Your business logic here
        var result = await _budgetService.ProcessAsync(fiscalYear);

        activity?.SetTag("result.count", result.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return result;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("exception.type", ex.GetType().Name);
        throw;
    }
}
```

### Filtering Sensitive Data

Prevent logging sensitive information:

```yaml
# otel-collector-config.yaml
processors:
  attributes/sanitize:
    actions:
      - key: http.request.header.authorization
        action: delete
      - key: db.statement
        action: delete # Don't log SQL with PII
      - key: password
        action: delete
```

### Exporting Data

**Export traces to JSON** (for analysis or compliance):

```powershell
# Query ClickHouse directly
docker exec signoz-clickhouse clickhouse-client --query "
SELECT * FROM signoz.signoz_traces
WHERE timestamp >= now() - INTERVAL 1 DAY
FORMAT JSONEachRow" > traces.json
```

### Integration with Grafana

SigNoz can coexist with Grafana for advanced visualization:

1. Add Grafana to docker-compose:

```yaml
grafana:
  image: grafana/grafana:latest
  ports:
    - "3000:3000"
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=admin
```

2. Configure Grafana data source:
   - Type: Prometheus
   - URL: http://signoz-otel-collector:8889

---

## Quick Reference Card

### Essential Commands

| Task         | Command                                       |
| ------------ | --------------------------------------------- |
| Start SigNoz | `.\scripts\manage-signoz.ps1 -Action start`   |
| Stop SigNoz  | `.\scripts\manage-signoz.ps1 -Action stop`    |
| View logs    | `.\scripts\manage-signoz.ps1 -Action logs`    |
| Check status | `.\scripts\manage-signoz.ps1 -Action status`  |
| Restart      | `.\scripts\manage-signoz.ps1 -Action restart` |

### Key URLs

| Service            | URL                           |
| ------------------ | ----------------------------- |
| Dashboard          | http://localhost:3301         |
| Query API          | http://localhost:8080         |
| OTLP gRPC          | http://localhost:4317         |
| OTLP HTTP          | http://localhost:4318         |
| ClickHouse HTTP    | http://localhost:8123         |
| Prometheus Metrics | http://localhost:8889/metrics |

### Configuration Files

| File                         | Purpose                      |
| ---------------------------- | ---------------------------- |
| `appsettings.json`           | Application telemetry config |
| `docker-compose.signoz.yml`  | SigNoz infrastructure        |
| `otel-collector-config.yaml` | Telemetry pipeline           |
| `clickhouse-config.xml`      | Database configuration       |

### Common Queries

**Find slow requests**:

```
durationNano > 1000000000
```

**Find errors**:

```
http.status_code >= 500
```

**Find database operations**:

```
db.system = "sqlserver"
```

**Find specific operation**:

```
name = "BudgetRepository.GetByFiscalYear"
```

---

## Support & Resources

### Official Documentation

- SigNoz Docs: https://signoz.io/docs/
- OpenTelemetry: https://opentelemetry.io/docs/

### Community

- SigNoz Slack: https://signoz.io/slack
- GitHub Issues: https://github.com/SigNoz/signoz

### Internal Resources

- Wiley Widget Documentation: `docs/`
- MCP Integration Guide: `docs/CSHARP_MCP_IMPLEMENTATION.md`
- CI/CD Setup: `docs/CONTINUE_MCP_AGENT_SETUP.md`

---

## Appendix A: Telemetry Data Schema

### Trace Attributes

**Standard Attributes** (from OpenTelemetry):

- `service.name`: "wiley-widget"
- `service.version`: "1.0.0"
- `environment`: "development" | "production"
- `http.method`: GET, POST, etc.
- `http.status_code`: 200, 404, 500, etc.
- `http.url`: Full request URL

**Custom Wiley Widget Attributes**:

- `db.wiley.operation_type`: SQL operation type
- `cache.enabled`: Whether caching is used
- `cache.hit`: Cache hit/miss status
- `fiscal_year`: Budget fiscal year
- `operation.type`: "query" | "command" | "business_logic"

### Metrics

**HTTP Metrics**:

- `http.server.duration`: Request duration in milliseconds
- `http.server.request.count`: Total request count
- `http.server.active_requests`: Currently processing requests

**Database Metrics**:

- `db.client.operation.duration`: Database operation duration
- `db.client.connections.usage`: Connection pool usage
- `db.client.connections.idle`: Idle connections

**Application Metrics**:

- `dotnet.process.cpu.time`: CPU time used
- `dotnet.process.memory.usage`: Memory usage in bytes
- `dotnet.gc.collections`: Garbage collection count

---

## Appendix B: Troubleshooting Flowchart

```
Issue: No data in SigNoz
    ↓
Is Wiley Widget running?
    No → Start application
    Yes ↓
Is SigNoz running?
    No → Run: manage-signoz.ps1 -Action start
    Yes ↓
Check OTel Collector logs
    Errors? → Fix configuration
    No errors ↓
Test connection: Test-NetConnection localhost -Port 4317
    Failed? → Check firewall/Docker networking
    Success ↓
Check appsettings.json SigNoz section
    Endpoint correct? → Verify http://localhost:4317
    EnableTracing = true?
    ↓
Enable console exporter and check app logs
    See trace data? → OTel working, issue with export
    No data? → Check SigNozTelemetryService initialization
```

---

**Document Version**: 1.0
**Last Updated**: November 7, 2025
**Maintainer**: Wiley Widget Development Team
