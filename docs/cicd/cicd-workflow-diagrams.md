# WileyWidget CI/CD Workflow Diagrams

## 🔄 Complete Development Workflow

```mermaid
graph TD
    A[👨‍💻 Developer] --> B[Code Changes]
    B --> C[Local Quality Checks]
    C --> D[Commit to Feature Branch]
    D --> E[Push to GitHub]
    E --> F[GitHub Actions Trigger]
    F --> G[CI Pipeline]
    G --> H{Checks Pass?}
    H -->|Yes| I[Create Pull Request]
    H -->|No| J[Fix Issues]
    J --> B
    I --> K[Code Review]
    K --> L[Merge to Main]
    L --> M[Release Pipeline]
    M --> N[Deploy to Production]
```

## 🏗️ CI Pipeline Flow

```mermaid
graph TD
    A[Push/PR to Main] --> B[Checkout Code]
    B --> C[Setup Environment]
    C --> D[Cache Dependencies]
    D --> E[Build Application]
    E --> F[Run Unit Tests]
    F --> G[Generate Coverage]
    G --> H{Coverage ≥70%?}
    H -->|No| I[Fail Pipeline]
    H -->|Yes| J[Run UI Tests]
    J --> K[Security Scan]
    K --> L[Upload Artifacts]
    L --> M[Pipeline Complete]
```

## 🚀 Release Pipeline Flow

```mermaid
graph TD
    A[Manual Trigger] --> B[Input Version]
    B --> C[Update Version Files]
    C --> D[Build Release]
    D --> E[Create Self-Contained EXE]
    E --> F[Package Artifacts]
    F --> G[Create GitHub Release]
    G --> H[Upload Assets]
    H --> I[Release Complete]
```

## 🔧 Quality Gates Flow

```mermaid
graph TD
    A[Code Commit] --> B[Pre-commit Hook]
    B --> C{Trunk Check}
    C -->|Fail| D[Block Commit]
    C -->|Pass| E[Commit Allowed]
    E --> F[Pre-push Hook]
    F --> G{Full Validation}
    G -->|Fail| H[Block Push]
    G -->|Pass| I[Push Allowed]
    I --> J[CI Pipeline]
    J --> K{All Checks}
    K -->|Fail| L[Block Merge]
    K -->|Pass| M[Merge Allowed]
```

## ☁️ Azure Deployment Flow

```mermaid
graph TD
    A[Deployment Trigger] --> B[Azure CLI Login]
    B --> C[Select Subscription]
    C --> D[Resource Group Check]
    D --> E{Create Resources?}
    E -->|Yes| F[Create SQL Database]
    E -->|No| G[Update Existing]
    F --> H[Configure Firewall]
    G --> H
    H --> I[Test Connection]
    I --> J{Connection OK?}
    J -->|No| K[Troubleshoot]
    J -->|Yes| L[Deploy Application]
    K --> I
    L --> M[Update Configuration]
    M --> N[Deployment Complete]
```

## 🔄 Dynamic Firewall Management

```mermaid
graph TD
    A[IP Change Detected] --> B[Get Current Public IP]
    B --> C[Check Existing Rules]
    C --> D{IP Already Allowed?}
    D -->|Yes| E[No Action Needed]
    D -->|No| F[Create New Rule]
    F --> G[Set Rule Name with Timestamp]
    G --> H[Apply Firewall Rule]
    H --> I{Application Days > 7?}
    I -->|Yes| J[Remove Old Rules]
    I -->|No| K[Keep Recent Rules]
    J --> L[Log Cleanup]
    K --> L
    L --> M[Test Database Connection]
    M --> N{Connection OK?}
    N -->|Yes| O[Update Successful]
    N -->|No| P[Troubleshoot Connection]
    P --> M
```

## 📊 Monitoring & Alerting Flow

```mermaid
graph TD
    A[Application Running] --> B[Collect Metrics]
    B --> C[Performance Data]
    B --> D[Error Logs]
    B --> E[Security Events]
    C --> F[Azure Application Insights]
    D --> F
    E --> F
    F --> G{Analyze Data}
    G --> H{Threshold Exceeded?}
    H -->|Yes| I[Send Alert]
    H -->|No| J[Continue Monitoring]
    I --> K[Notify Team]
    K --> L{Action Required?}
    L -->|Yes| M[Investigate Issue]
    L -->|No| J
    M --> N[Resolve Problem]
    N --> O[Update Monitoring]
    O --> J
```

## 🔐 Security Scanning Flow

```mermaid
graph TD
    A[Code Changes] --> B[TruffleHog Scan]
    B --> C{Secrets Found?}
    C -->|Yes| D[Block Pipeline]
    C -->|No| E[Checkov IaC Scan]
    E --> F{Security Issues?}
    F -->|Yes| G[Generate Report]
    F -->|No| H[Dependency Scan]
    G --> I[Review Findings]
    I --> J{Issues Critical?}
    J -->|Yes| D
    J -->|No| H
    H --> K{Vulnerabilities?}
    K -->|Yes| L[Update Dependencies]
    K -->|No| M[Security Checks Pass]
    L --> A
```

---

## 📈 Key Performance Indicators (KPIs)

### Development Velocity

- **Lead Time**: Time from commit to production
- **Deployment Frequency**: Deployments per day
- **Change Failure Rate**: Failed deployments percentage
- **Mean Time to Recovery**: Time to fix production issues

### Quality Metrics

- **Test Coverage**: Percentage of code covered by tests
- **Build Success Rate**: Percentage of successful builds
- **Security Scan Results**: Number of vulnerabilities found
- **Code Quality Score**: Based on linting results

### Operational Metrics

- **Uptime**: Application availability percentage
- **Performance**: Response times and throughput
- **Error Rate**: Application error percentage
- **Resource Usage**: CPU, memory, and storage utilization

---

_These diagrams provide a visual representation of the CI/CD processes and can be used for training, documentation, and process improvement._
