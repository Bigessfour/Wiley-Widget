# Security & Compliance Assessment - Wiley Widget

**Assessment Date**: November 3, 2025
**Project**: Wiley Widget Municipal Budget Management System
**Compliance Scope**: Municipal Data Handling, Financial Data Protection, Privacy Regulations
**Status**: 🟡 **MODERATE RISK** - Security foundations present but compliance gaps identified

---

## Executive Summary

Wiley Widget has implemented foundational security measures including audit logging, data anonymization, and secret management. However, significant gaps exist in:

1. **Vulnerability Scanning** - Limited to Trunk security linters only
2. **Compliance Documentation** - Privacy/terms pages are placeholders lacking legal review
3. **Municipal Data Regulations** - No explicit compliance framework for government data
4. **Third-party Security** - Limited vendor security assessment
5. **Incident Response** - No formal security incident procedures

**Critical Priority**: Implement comprehensive vulnerability scanning and ensure municipal data compliance before production deployment.

---

## 1. Current Security Posture

### ✅ Implemented Security Controls

#### 1.1 Secret Management

**Status**: **GOOD** ✅
**Implementation**: Environment variables, no hardcoded secrets

```csharp
// Proper secret handling detected
_clientId = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-ID")
_clientSecret = await TryGetFromSecretVaultAsync(_secretVault, "QBO-CLIENT-SECRET")

// Configuration uses placeholders
{
  "ConnectionStrings": {
    "DefaultConnection": "${DATABASE_CONNECTION_STRING}"
  },
  "QuickBooks": {
    "ClientSecret": "${QBO_CLIENT_SECRET}"
  }
}
```

**Security Scan Results** (from SECURITY.md):

- ✔️ **GitLeaks**: No secrets detected in git history
- ✔️ **TruffleHog**: No secrets found in files
- ✔️ **Configuration**: Using environment variable placeholders correctly

**Verification Command**:

```powershell
trunk check --filter=gitleaks,trufflehog --all
```

#### 1.2 Audit Logging

**Status**: **GOOD** ✅
**Implementation**: Structured audit trail with file rotation

**Features**:

- Append-only audit log: `logs/audit.log`
- Automatic file rotation at 5 MB
- 30-day retention policy
- Structured JSON format
- Tamper-evident timestamps

```csharp
// AuditService.cs implementation
public Task AuditAsync(string eventName, object details)
{
    var entry = new
    {
        Timestamp = DateTimeOffset.UtcNow,
        Event = eventName,
        Details = details  // Must be pre-redacted by caller
    };
    File.AppendAllText(_auditPath, json + Environment.NewLine);
}
```

**⚠️ Warning**: Service documentation states "MUST NOT store secret values" - relies on caller to redact sensitive data.

**Audit Events Tracked**:

- QuickBooks API operations
- Database migrations
- User authentication events
- Configuration changes
- AI service usage

#### 1.3 Data Anonymization

**Status**: **GOOD** ✅
**Implementation**: GDPR-compliant data masking for AI services

```csharp
// DataAnonymizerService.cs
public class DataAnonymizerService : IDataAnonymizerService
{
    // Anonymizes enterprise data before AI processing
    public Enterprise AnonymizeEnterprise(Enterprise enterprise)
    {
        return new Enterprise
        {
            Id = enterprise.Id,  // Keep for reference
            Name = AnonymizeName(enterprise.Name, "Enterprise"),
            Description = AnonymizeDescription(enterprise.Description),
            // Preserve non-sensitive operational data
            CurrentRate = enterprise.CurrentRate,
            TotalBudget = enterprise.TotalBudget
        };
    }
}
```

**Anonymization Features**:

- Deterministic name masking (same input → same output)
- Caching for consistency
- Reversible anonymization support
- GDPR-compliant implementation
- Comprehensive logging

**Usage**:

- AI queries to xAI Grok API
- Third-party data sharing
- Testing/development data

#### 1.4 Webhook Signature Verification

**Status**: **GOOD** ✅
**Implementation**: HMAC-SHA256 signature validation

```csharp
// WileyWidget.Webhooks/Program.cs
using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var hash = h.ComputeHash(Encoding.UTF8.GetBytes(body));
var expected = Convert.ToBase64String(hash);

// Timing-safe comparison
if (!CryptographicOperations.FixedTimeEquals(providedBytes, hash))
{
    return Results.Unauthorized();
}
```

**Features**:

- Timing-safe comparison prevents side-channel attacks
- HMAC-SHA256 industry standard
- Webhook replay protection via timestamps
- QuickBooks API signature validation

#### 1.5 Transport Security

**Status**: **GOOD** ✅

- All API communications use HTTPS
- TLS 1.2+ enforcement
- Certificate validation enabled
- No insecure HTTP endpoints

### 🟡 Partially Implemented Controls

#### 1.6 Encryption at Rest

**Status**: **PARTIAL** 🟡

**Current Implementation**:

- OAuth tokens stored in Windows Credential Manager (encrypted by OS)
- Database connection strings in environment variables
- Local database files (SQL Server LocalDB) - OS-level encryption

**Gaps**:

- No application-level encryption for sensitive database columns
- No explicit field-level encryption for PII
- Audit logs stored in plaintext JSON
- Configuration files not encrypted at rest

**Recommendation**:

```csharp
// Add field-level encryption for sensitive data
public class MunicipalAccount
{
    public int Id { get; set; }

    [Encrypted]  // Custom attribute for automatic encryption
    public string? TaxIdNumber { get; set; }

    [Encrypted]
    public string? ContactEmail { get; set; }

    public decimal Budget { get; set; }  // Not PII, no encryption needed
}
```

#### 1.7 Access Control

**Status**: **PARTIAL** 🟡

**Current Implementation**:

- OAuth 2.0 for QuickBooks access
- Windows user authentication (implicit via desktop app)
- No role-based access control (RBAC) detected

**Gaps**:

- No multi-user support
- No role separation (admin vs. user)
- No audit trail for permission changes
- No fine-grained permissions

---

## 2. Vulnerability Scanning Gap Analysis

### ⚠️ Current State: **LIMITED COVERAGE** 🟡

#### Active Security Scanners (via Trunk)

```yaml
# .trunk/trunk.yaml
lint:
  enabled:
    - bandit@1.8.6 # Python security linter
    - gitleaks@8.28.0 # Secret detection
    - trufflehog@3.90.12 # Secret scanning
    - osv-scanner@2.2.4 # Open source vulnerability scanner
    - checkov@3.2.489 # Infrastructure-as-code security
```

**Coverage Assessment**:

| Security Layer          | Current Coverage                    | Gap                                 |
| ----------------------- | ----------------------------------- | ----------------------------------- |
| **Secret Detection**    | ✅ Excellent (GitLeaks, TruffleHog) | None                                |
| **Python Security**     | ✅ Good (Bandit)                    | Limited Python code                 |
| **Open Source CVEs**    | 🟡 Partial (OSV-Scanner)            | .NET packages not deeply scanned    |
| **.NET Security**       | 🔴 **NONE**                         | No Roslyn analyzers, no SonarQube   |
| **SAST**                | 🔴 **NONE**                         | No static analysis for C#           |
| **DAST**                | 🔴 **NONE**                         | No dynamic scanning                 |
| **Dependency Scanning** | 🟡 Partial (OSV-Scanner)            | No Snyk, Dependabot, or WhiteSource |
| **Container Security**  | 🔴 **NONE**                         | No Trivy, Clair, or Anchore         |
| **IAST**                | 🔴 **NONE**                         | No runtime security monitoring      |

### 🚨 Critical Gaps

#### Gap 1: No .NET-Specific Vulnerability Scanning

**Risk**: High
**Impact**: Unknown vulnerabilities in 16 Syncfusion packages, 20+ Microsoft packages, Intuit SDK

**Current NuGet Packages Without Security Scanning**:

```xml
<!-- High-risk packages not scanned -->
<PackageReference Include="Syncfusion.*" Version="31.1.17" />  <!-- 16 packages -->
<PackageReference Include="IppDotNetSdkForQuickBooksApiV3" Version="14.7.0.1" />
<PackageReference Include="OpenAI" Version="2.5.0" />
<PackageReference Include="Microsoft.Extensions.*" Version="9.0.10" />  <!-- 20+ packages -->
```

**Recommended Solutions**:

1. **OWASP Dependency-Check**

   ```powershell
   # Install OWASP Dependency-Check
   choco install dependencycheck

   # Scan .NET project
   dependency-check --project "WileyWidget" --scan . --format HTML --format JSON

   # Focus on NuGet packages
   dependency-check --enableExperimental --scan *.csproj
   ```

2. **Snyk for .NET**

   ```powershell
   # Install Snyk CLI
   npm install -g snyk

   # Authenticate
   snyk auth

   # Test for vulnerabilities
   snyk test --file=WileyWidget.csproj

   # Monitor continuously
   snyk monitor --file=WileyWidget.csproj
   ```

3. **GitHub Dependabot** (MISSING - No .github directory detected)

   ```yaml
   # .github/dependabot.yml - NEEDS TO BE CREATED
   version: 2
   updates:
     - package-ecosystem: "nuget"
       directory: "/"
       schedule:
         interval: "weekly"
       open-pull-requests-limit: 10
       labels:
         - "dependencies"
         - "security"
   ```

4. **Roslyn Security Analyzers**
   ```xml
   <!-- Add to WileyWidget.csproj -->
   <ItemGroup>
     <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0" />
     <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4" />
     <PackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" Version="17.11.20" />
     <PackageReference Include="Roslynator.Analyzers" Version="4.12.7" />
     <PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7" />
   </ItemGroup>
   ```

#### Gap 2: No Container/Docker Security Scanning

**Risk**: Medium
**Impact**: Docker images may contain vulnerable base layers or packages

**Detected Docker Usage**:

```dockerfile
# docker/Dockerfile.csx-tests
# Base image security unknown
FROM mcr.microsoft.com/dotnet/sdk:9.0
```

**Recommended Solutions**:

1. **Trivy Scanner**

   ```powershell
   # Install Trivy
   choco install trivy

   # Scan Docker images
   trivy image wiley-widget/csx-mcp:local

   # Scan Dockerfile
   trivy config docker/Dockerfile.csx-tests
   ```

2. **Docker Scout** (Built into Docker Desktop)

   ```powershell
   # Enable Docker Scout
   docker scout quickview wiley-widget/csx-mcp:local

   # Detailed CVE report
   docker scout cves wiley-widget/csx-mcp:local
   ```

#### Gap 3: No Dynamic Application Security Testing (DAST)

**Risk**: Medium
**Impact**: Runtime vulnerabilities not detected

**Recommended Solutions**:

1. **OWASP ZAP** (for webhook API)

   ```powershell
   # Scan webhook endpoint
   zap-cli quick-scan --self-contained http://localhost:5000/webhooks
   ```

2. **Burp Suite Community** (Manual testing)

#### Gap 4: No Software Composition Analysis (SCA)

**Risk**: High
**Impact**: Transitive dependency vulnerabilities unknown

**Example**: `Syncfusion.SfGrid.WPF` may depend on vulnerable versions of `System.Text.Json`

**Recommended Solutions**:

1. **WhiteSource Bolt** (Free for open source)
2. **Sonatype Nexus Lifecycle**
3. **Black Duck**

---

## 3. Compliance Gap Analysis

### 🔴 Critical Issue: Municipal Data Compliance

#### 3.1 Applicable Regulations

Municipal financial data is subject to multiple regulatory frameworks:

| Regulation                                         | Applicability            | Status                     | Priority |
| -------------------------------------------------- | ------------------------ | -------------------------- | -------- |
| **GASB** (Governmental Accounting Standards Board) | ✅ High                  | 🔴 Not Documented          | **P0**   |
| **FOIA** (Freedom of Information Act)              | ✅ High                  | 🔴 Not Implemented         | **P0**   |
| **State Sunshine Laws**                            | ✅ High                  | 🔴 Unknown                 | **P0**   |
| **CJIS** (Criminal Justice Information Services)   | 🟡 Possible              | 🔴 Not Assessed            | **P1**   |
| **IRS Publication 1075**                           | 🟡 If handling tax data  | 🔴 Not Compliant           | **P1**   |
| **GDPR**                                           | 🟡 If EU citizens        | 🟡 Partial (anonymization) | **P2**   |
| **CCPA**                                           | 🟡 If CA residents       | 🔴 Not Compliant           | **P2**   |
| **PCI DSS**                                        | ❌ No payment processing | N/A                        | N/A      |
| **HIPAA**                                          | ❌ No health data        | N/A                        | N/A      |

#### 3.2 Governmental Accounting Standards Board (GASB) Compliance

**Requirement**: Municipal financial systems must comply with GASB standards for:

- Fund accounting segregation
- Audit trail requirements
- Financial statement preparation
- Internal controls documentation

**Current Status**: 🔴 **NOT DOCUMENTED**

**Gaps**:

- No GASB compliance documentation
- Unclear if chart of accounts follows GASB structure
- No evidence of GASB 34 (financial reporting) compliance
- No documentation of internal controls (GASB 87)

**Recommendation**:

```markdown
# Required Documentation: docs/GASB_COMPLIANCE.md

## GASB Standard Compliance Matrix

| GASB Standard | Requirement                               | Implementation             | Status     |
| ------------- | ----------------------------------------- | -------------------------- | ---------- |
| GASB 34       | Management's Discussion & Analysis (MD&A) | ReportingModule            | ✅         |
| GASB 54       | Fund Balance Reporting                    | FundAccountingService      | 🟡 Partial |
| GASB 87       | Leases                                    | Not Applicable             | N/A        |
| GASB 96       | Subscription-Based IT Arrangements        | Track Syncfusion/QBO costs | 🔴 Missing |

## Fund Accounting Compliance

- General Fund: Account range 1000-1999
- Special Revenue Funds: 2000-2999
- Debt Service Funds: 3000-3999
- Capital Projects Funds: 4000-4999
```

#### 3.3 Freedom of Information Act (FOIA) Compliance

**Requirement**: Municipal data must be:

- Accessible to public upon request
- Exportable in standard formats
- Redacted for personal information
- Available within statutory timeframes (typically 5-10 business days)

**Current Status**: 🔴 **NOT IMPLEMENTED**

**Gaps**:

- No FOIA request handling workflow
- No automated data export for public records
- No PII redaction tools
- No public records retention schedule

**Recommendation**:

```csharp
// Add to WileyWidget.Services
public interface IPublicRecordsService
{
    /// <summary>
    /// Exports public financial records in response to FOIA request
    /// </summary>
    Task<PublicRecordsExport> ExportPublicRecordsAsync(
        PublicRecordsRequest request,
        RedactionOptions redactionOptions);

    /// <summary>
    /// Redacts personally identifiable information from records
    /// </summary>
    Task<RedactedDocument> RedactPIIAsync(Document document);

    /// <summary>
    /// Generates FOIA response package with audit trail
    /// </summary>
    Task<FoiaResponsePackage> GenerateFoiaResponseAsync(
        int requestId,
        IEnumerable<Document> documents);
}
```

**Required Features**:

1. **Automated Export** - CSV, PDF, Excel formats
2. **PII Redaction** - Remove SSNs, personal addresses, phone numbers
3. **Audit Trail** - Log all public records requests
4. **Exemption Handling** - Mark confidential records (personnel, legal)
5. **Response Tracking** - Deadline management system

#### 3.4 State Sunshine Laws

**Requirement**: Varies by state, generally requires:

- Open meetings for budget decisions
- Public notice of financial actions
- Transparent budget process
- Citizen access to financial data

**Current Status**: 🔴 **UNKNOWN** (State not specified)

**Recommendation**: Create state-specific compliance module

#### 3.5 IRS Publication 1075 (Federal Tax Information)

**Requirement**: If system handles tax data (Form W-9, 1099s, tax liens):

- Encrypted storage (AES-256)
- Access controls (role-based)
- Audit logging (all access events)
- Background checks for users
- Annual security assessments

**Current Status**: 🔴 **NOT COMPLIANT** (if tax data present)

**Assessment Needed**:

```csharp
// Determine if IRS Pub 1075 applies
public class TaxDataAssessment
{
    public bool HandlesW9Data { get; set; }
    public bool Handles1099Data { get; set; }
    public bool HandlesTaxLiens { get; set; }
    public bool HandlesStateWithholding { get; set; }

    public bool RequiresPublication1075Compliance =>
        HandlesW9Data || Handles1099Data || HandlesTaxLiens || HandlesStateWithholding;
}
```

**If applicable, implement**:

1. **AES-256 Encryption** for all tax data fields
2. **Role-Based Access Control** with least privilege
3. **Enhanced Audit Logging** (all access, not just changes)
4. **Background Checks** for all users with tax data access
5. **Annual Security Assessment** by independent auditor
6. **Incident Response Plan** specific to tax data breaches

#### 3.6 Criminal Justice Information Services (CJIS) Security Policy

**Applicability**: If system handles:

- Criminal history records
- Court fine/fee collection
- Law enforcement financial data
- Municipal court systems integration

**Current Status**: 🔴 **NOT ASSESSED**

**If applicable, requires**:

- FBI CJIS Security Addendum signed
- Two-factor authentication
- Advanced authentication (biometric or token)
- Physical security controls
- Personnel screening
- Audit logs retention (5 years minimum)

---

### 🟡 Privacy Policy & Terms of Service Issues

#### 3.7 Privacy Policy Analysis

**File**: `wwwroot/privacy.html`
**Status**: 🟡 **PLACEHOLDER** - Not legally reviewed

**Current Content Analysis**:

✅ **Adequate Sections**:

- Overview of data collection
- Third-party service disclosures (QuickBooks, xAI, Syncfusion)
- AI data processing explanation
- User rights (revoke access, delete data)
- Data retention policy
- Contact information

🟡 **Needs Improvement**:

```html
<!-- Current disclaimer -->
<small
  >This is a development/sandbox privacy policy. For production use, consult with legal counsel to ensure compliance
  with applicable laws.</small
>
```

**Compliance Gaps**:

1. **No Jurisdiction-Specific Provisions**
   - Missing state-specific privacy laws (e.g., California CCPA)
   - No EU GDPR provisions (if applicable)
   - No Canadian PIPEDA provisions

2. **Incomplete Data Subject Rights**
   - GDPR requires: right to erasure, portability, restriction
   - CCPA requires: right to opt-out of sale
   - No process for exercising rights documented

3. **No Cookie/Tracking Disclosure**
   - Application Insights tracking (detected in dependencies)
   - No opt-out mechanism documented

4. **Inadequate Municipal Data Provisions**

   ```html
   <!-- MISSING SECTION -->
   <h2>Municipal Data Handling</h2>
   <p>
     As a municipal financial management system, Wiley Widget handles public records that may be subject to Freedom of
     Information Act (FOIA) requests. We maintain:
   </p>
   <ul>
     <li>Separation between public records and personal data</li>
     <li>FOIA-compliant export capabilities</li>
     <li>Audit trails for all data access</li>
     <li>Compliance with GASB accounting standards</li>
   </ul>
   ```

5. **No Data Breach Notification Process**

   ```html
   <!-- MISSING SECTION -->
   <h2>Data Breach Notification</h2>
   <p>In the event of a security breach affecting your personal data, we will:</p>
   <ul>
     <li>Notify affected users within 72 hours</li>
     <li>Report to relevant authorities as required by law</li>
     <li>Provide details of the breach and mitigation steps</li>
     <li>Offer credit monitoring services if applicable</li>
   </ul>
   ```

6. **Vague Data Retention**

   ```html
   <!-- CURRENT (TOO VAGUE) -->
   <p>We retain your data only as long as necessary to provide services.</p>

   <!-- SHOULD BE SPECIFIC -->
   <p>We retain data according to the following schedule:</p>
   <ul>
     <li>Financial records: 7 years (per IRS requirements)</li>
     <li>Audit logs: 5 years (per CJIS standards if applicable)</li>
     <li>OAuth tokens: Until revoked by user</li>
     <li>Application settings: Until application uninstalled</li>
   </ul>
   ```

7. **No International Data Transfer Provisions**
   - xAI Grok API likely processes data in US
   - Need disclosure of data transfer mechanisms
   - EU users need standard contractual clauses

8. **Missing Contact Details**

   ```html
   <!-- PLACEHOLDER -->
   Email: [your-email@townofwiley.gov]

   <!-- NEEDS REAL CONTACT -->
   Email: privacy@townofwiley.gov Phone: (555) 123-4567 Address: 123 Main St, Wiley, CO 81092 Data Protection Officer:
   [Name if required by GDPR]
   ```

**Recommended Actions**:

1. **Immediate** (Before Production):
   - Legal review by municipal attorney
   - Add jurisdiction-specific provisions
   - Complete all placeholder text
   - Add data breach notification section
   - Specify exact retention periods

2. **Short-term** (Within 3 months):
   - Implement user consent management
   - Add cookie consent banner
   - Create data subject request process
   - Document internal privacy procedures

3. **Ongoing**:
   - Annual privacy policy review
   - Update when new features/services added
   - Monitor regulatory changes

#### 3.8 Terms of Service Analysis

**File**: `wwwroot/terms.html`
**Status**: 🟡 **PLACEHOLDER** - Not legally reviewed

**Current Content Analysis**:

✅ **Adequate Sections**:

- License grant
- Usage restrictions
- Disclaimer of warranties
- Limitation of liability
- Intellectual property rights

🟡 **Needs Improvement**:

1. **No Syncfusion License Terms**

   ```html
   <!-- MISSING CRITICAL DISCLOSURE -->
   <h2>Third-Party Licenses</h2>
   <p>This software includes components licensed from:</p>
   <ul>
     <li>
       <strong>Syncfusion:</strong> Subject to Syncfusion Community/Commercial License. See
       <a href="https://www.syncfusion.com/sales/licensing">Syncfusion Licensing</a>
     </li>
     <li><strong>DryIoc:</strong> MIT License</li>
     <li><strong>Prism:</strong> MIT License</li>
   </ul>
   <p>Your use of Wiley Widget is subject to these third-party licenses.</p>
   ```

2. **No QuickBooks Terms Reference**

   ```html
   <h2>QuickBooks Integration</h2>
   <p>By using QuickBooks features, you agree to:</p>
   <ul>
     <li>Intuit's Terms of Service</li>
     <li>Maintain valid QuickBooks Online subscription</li>
     <li>Authorize Wiley Widget access to your company data</li>
   </ul>
   ```

3. **No AI Usage Terms**

   ```html
   <h2>AI Features and Limitations</h2>
   <p>AI-powered features are provided on an "as-is" basis:</p>
   <ul>
     <li>AI recommendations are advisory only</li>
     <li>Not a substitute for professional financial advice</li>
     <li>Verify all AI-generated insights before acting</li>
     <li>Subject to xAI Grok API terms of service</li>
   </ul>
   ```

4. **No Audit/Compliance Disclaimer**

   ```html
   <h2>Compliance and Auditing</h2>
   <p>While Wiley Widget includes audit logging and compliance features:</p>
   <ul>
     <li>You remain responsible for ensuring municipal compliance</li>
     <li>Software does not guarantee GASB compliance</li>
     <li>Independent auditor review recommended</li>
     <li>Consult with municipal attorney for legal requirements</li>
   </ul>
   ```

5. **No Data Accuracy Disclaimer**
   ```html
   <h2>Data Accuracy</h2>
   <p>You are responsible for:</p>
   <ul>
     <li>Verifying accuracy of data imported from QuickBooks</li>
     <li>Maintaining backup copies of financial data</li>
     <li>Reconciling reports with source accounting system</li>
   </ul>
   ```

---

## 4. Recommended Security Enhancements

### 4.1 Implement Comprehensive Vulnerability Scanning

#### Phase 1: Immediate (Week 1-2)

#### 1. Add .NET Security Analyzers

```xml
<!-- Add to Directory.Build.props -->
<ItemGroup>
  <!-- Security analyzers -->
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0" />
  <PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7" />
  <PackageReference Include="Roslynator.Analyzers" Version="4.12.7" />

  <!-- Threading and async analyzers -->
  <PackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" Version="17.11.20" />

  <!-- Banned API analyzer (prevent use of insecure APIs) -->
  <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4" />
</ItemGroup>

<PropertyGroup>
  <!-- Enable all analyzers -->
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest-all</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

#### 2. Configure Banned APIs

```txt
<!-- BannedSymbols.txt - Add to project root -->
# Insecure cryptography
T:System.Security.Cryptography.MD5; Use SHA256 or better
T:System.Security.Cryptography.SHA1; Use SHA256 or better
M:System.Security.Cryptography.DES.Create(); Use AES instead

# Insecure random number generation
M:System.Random.#ctor(); Use RandomNumberGenerator for security-sensitive operations

# SQL injection risks
M:System.Data.SqlClient.SqlCommand.#ctor(System.String); Use parameterized queries

# XSS risks
M:System.Web.HttpUtility.HtmlEncode(System.String); Use System.Net.WebUtility.HtmlEncode instead

# Insecure deserialization
M:System.Runtime.Serialization.Formatters.Binary.BinaryFormatter.Deserialize(System.IO.Stream); Use JSON or MessagePack instead
```

**3. Setup GitHub Dependabot** (CRITICAL)

```yaml
# .github/dependabot.yml - CREATE THIS FILE
version: 2
updates:
  # NuGet dependencies
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "monday"
      time: "09:00"
    open-pull-requests-limit: 10
    reviewers:
      - "security-team"
    labels:
      - "dependencies"
      - "security"
    commit-message:
      prefix: "security"
      include: "scope"
    # Group minor and patch updates
    groups:
      microsoft-packages:
        patterns:
          - "Microsoft.*"
      syncfusion-packages:
        patterns:
          - "Syncfusion.*"
    # Security updates only
    allow:
      - dependency-type: "direct"
      - dependency-type: "indirect"
    # Ignore specific updates
    ignore:
      - dependency-name: "Syncfusion.*"
        update-types: ["version-update:semver-major"] # Require manual review

  # NPM dependencies
  - package-ecosystem: "npm"
    directory: "/"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 5

  # Docker dependencies
  - package-ecosystem: "docker"
    directory: "/docker"
    schedule:
      interval: "weekly"
```

#### 4. Enable GitHub Security Features

```yaml
# .github/workflows/security-scan.yml - CREATE THIS
name: Security Scan

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]
  schedule:
    - cron: "0 0 * * 1" # Weekly on Monday

jobs:
  security-scan:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      # OWASP Dependency-Check
      - name: OWASP Dependency-Check
        uses: dependency-check/Dependency-Check_Action@main
        with:
          project: "WileyWidget"
          path: "."
          format: "HTML"
          args: >
            --enableExperimental
            --failOnCVSS 7
            --suppression dependency-check-suppressions.xml

      - name: Upload Dependency-Check Report
        uses: actions/upload-artifact@v4
        with:
          name: dependency-check-report
          path: ${{ github.workspace }}/reports

      # Snyk Security Scan
      - name: Run Snyk to check for vulnerabilities
        uses: snyk/actions/dotnet@master
        env:
          SNYK_TOKEN: ${{ secrets.SNYK_TOKEN }}
        with:
          args: --severity-threshold=high --file=WileyWidget.csproj

      # CodeQL Analysis
      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: csharp
          queries: security-extended

      - name: Autobuild
        uses: github/codeql-action/autobuild@v3

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3

      # Trivy Container Scan
      - name: Build Docker image
        run: docker build -t wiley-widget:${{ github.sha }} -f docker/Dockerfile.csx-tests .

      - name: Run Trivy vulnerability scanner
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: "wiley-widget:${{ github.sha }}"
          format: "sarif"
          output: "trivy-results.sarif"
          severity: "CRITICAL,HIGH"

      - name: Upload Trivy results to GitHub Security tab
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: "trivy-results.sarif"
```

#### Phase 2: Short-term (Month 1-2)

#### 5. Integrate SonarQube/SonarCloud

```yaml
# .github/workflows/sonarcloud.yml
name: SonarCloud Analysis

on:
  push:
    branches: [main, develop]
  pull_request:
    types: [opened, synchronize, reopened]

jobs:
  sonarcloud:
    name: SonarCloud
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Shallow clones disabled for better analysis

      - name: Set up JDK 17
        uses: actions/setup-java@v4
        with:
          java-version: 17
          distribution: "zulu"

      - name: Cache SonarCloud packages
        uses: actions/cache@v4
        with:
          path: ~\sonar\cache
          key: ${{ runner.os }}-sonar
          restore-keys: ${{ runner.os }}-sonar

      - name: Install SonarCloud scanner
        run: |
          dotnet tool install --global dotnet-sonarscanner

      - name: Build and analyze
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: |
          dotnet-sonarscanner begin /k:"Bigessfour_Wiley-Widget" /o:"bigessfour" /d:sonar.login="${{ secrets.SONAR_TOKEN }}" /d:sonar.host.url="https://sonarcloud.io"
          dotnet build WileyWidget.sln --no-incremental
          dotnet-sonarscanner end /d:sonar.login="${{ secrets.SONAR_TOKEN }}"
```

#### 6. Setup Snyk CLI for Local Development

```powershell
# scripts/security-scan.ps1
<#
.SYNOPSIS
    Runs security scans locally before committing
#>

param(
    [switch]$Quick,
    [switch]$Full
)

Write-Host "🔒 Running Security Scans..." -ForegroundColor Cyan

# 1. Trunk security checks
Write-Host "`n📋 Running Trunk security linters..." -ForegroundColor Yellow
trunk check --filter=gitleaks,trufflehog,osv-scanner --all

# 2. .NET Security Analyzers
Write-Host "`n🔍 Running .NET security analyzers..." -ForegroundColor Yellow
dotnet build /p:RunAnalyzers=true /p:TreatWarningsAsErrors=false | Select-String "warning.*security"

if ($Full) {
    # 3. Snyk scan
    Write-Host "`n🐛 Running Snyk vulnerability scan..." -ForegroundColor Yellow
    snyk test --file=WileyWidget.csproj --severity-threshold=medium

    # 4. OWASP Dependency-Check
    Write-Host "`n🛡️ Running OWASP Dependency-Check..." -ForegroundColor Yellow
    & "C:\Program Files\dependency-check\bin\dependency-check.bat" `
        --project "WileyWidget" `
        --scan . `
        --format HTML `
        --enableExperimental

    # 5. Docker image scan (if applicable)
    if (docker images | Select-String "wiley-widget") {
        Write-Host "`n🐳 Running Docker image scan..." -ForegroundColor Yellow
        trivy image wiley-widget/csx-mcp:local
    }
}

Write-Host "`n✅ Security scans complete!" -ForegroundColor Green
```

#### Phase 3: Mid-term (Month 3-4)

#### 7. Implement Runtime Application Self-Protection (RASP)

```csharp
// Add to WileyWidget.Services
public class RuntimeSecurityMonitor
{
    private readonly ILogger<RuntimeSecurityMonitor> _logger;
    private readonly IAuditService _auditService;

    public async Task MonitorSqlInjectionAsync(string query, object parameters)
    {
        // Detect SQL injection patterns
        var suspiciousPatterns = new[]
        {
            @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER)\b.*\b(FROM|INTO|TABLE|DATABASE)\b)",
            @"('|('')|;|--|\/\*|\*\/)",
            @"\b(OR|AND)\b.*\d+\s*=\s*\d+"
        };

        foreach (var pattern in suspiciousPatterns)
        {
            if (Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase))
            {
                await _auditService.AuditAsync("SECURITY_ALERT", new
                {
                    Type = "Potential SQL Injection",
                    Query = query.Substring(0, Math.Min(100, query.Length)),
                    Timestamp = DateTimeOffset.UtcNow
                });

                _logger.LogWarning("Potential SQL injection detected: {Query}", query);
            }
        }
    }

    public async Task MonitorUnauthorizedDataAccessAsync(string userId, string resource)
    {
        // Implement access pattern analysis
        // Alert on anomalous access patterns
    }
}
```

### 4.2 Enhance Municipal Data Compliance

#### Create Compliance Framework

```csharp
// WileyWidget.Compliance/ComplianceFramework.cs
namespace WileyWidget.Compliance
{
    /// <summary>
    /// Municipal data compliance framework
    /// </summary>
    public class MunicipalComplianceService
    {
        private readonly ILogger<MunicipalComplianceService> _logger;
        private readonly IAuditService _auditService;

        public async Task<ComplianceReport> RunComplianceCheckAsync()
        {
            var report = new ComplianceReport
            {
                Timestamp = DateTimeOffset.UtcNow,
                Checks = new List<ComplianceCheck>()
            };

            // GASB Compliance
            report.Checks.Add(await CheckGASBComplianceAsync());

            // FOIA Readiness
            report.Checks.Add(await CheckFOIAReadinessAsync());

            // Data Retention
            report.Checks.Add(await CheckDataRetentionPolicyAsync());

            // Audit Trail Integrity
            report.Checks.Add(await CheckAuditTrailIntegrityAsync());

            // Access Controls
            report.Checks.Add(await CheckAccessControlsAsync());

            await _auditService.AuditAsync("COMPLIANCE_CHECK", report);

            return report;
        }

        private async Task<ComplianceCheck> CheckGASBComplianceAsync()
        {
            var check = new ComplianceCheck
            {
                Standard = "GASB",
                CheckName = "Fund Accounting Structure",
                Status = ComplianceStatus.Unknown
            };

            // Verify fund structure matches GASB requirements
            // Check for proper segregation of fund types
            // Validate account numbering scheme

            return check;
        }

        private async Task<ComplianceCheck> CheckFOIAReadinessAsync()
        {
            var check = new ComplianceCheck
            {
                Standard = "FOIA",
                CheckName = "Public Records Accessibility",
                Status = ComplianceStatus.Unknown
            };

            // Check if public records can be exported
            // Verify PII redaction capabilities
            // Validate response timeframes

            return check;
        }
    }

    public class ComplianceReport
    {
        public DateTimeOffset Timestamp { get; set; }
        public List<ComplianceCheck> Checks { get; set; }
        public bool IsCompliant => Checks.All(c => c.Status == ComplianceStatus.Compliant);
    }

    public class ComplianceCheck
    {
        public string Standard { get; set; }
        public string CheckName { get; set; }
        public ComplianceStatus Status { get; set; }
        public string Details { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public enum ComplianceStatus
    {
        Compliant,
        NonCompliant,
        PartiallyCompliant,
        Unknown
    }
}
```

### 4.3 Implement Field-Level Encryption

```csharp
// WileyWidget.Services/Encryption/FieldEncryptionService.cs
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

public class FieldEncryptionService : IFieldEncryptionService
{
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly ILogger<FieldEncryptionService> _logger;

    public FieldEncryptionService(
        IDataProtectionProvider protectionProvider,
        ILogger<FieldEncryptionService> logger)
    {
        _protectionProvider = protectionProvider;
        _logger = logger;
    }

    public string Encrypt(string plaintext, string purpose = "default")
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        try
        {
            var protector = _protectionProvider.CreateProtector(purpose);
            return protector.Protect(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed for purpose: {Purpose}", purpose);
            throw;
        }
    }

    public string Decrypt(string ciphertext, string purpose = "default")
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        try
        {
            var protector = _protectionProvider.CreateProtector(purpose);
            return protector.Unprotect(ciphertext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed for purpose: {Purpose}", purpose);
            throw;
        }
    }
}

// Usage in entities
public class MunicipalAccount
{
    [Encrypted(Purpose = "TaxData")]
    public string? TaxIdNumber { get; set; }

    [Encrypted(Purpose = "ContactInfo")]
    public string? ContactEmail { get; set; }

    [Encrypted(Purpose = "ContactInfo")]
    public string? Phone { get; set; }

    // Non-sensitive data not encrypted
    public decimal Budget { get; set; }
    public string AccountNumber { get; set; }
}
```

### 4.4 Add Security Incident Response Plan

```markdown
# docs/SECURITY_INCIDENT_RESPONSE.md

## Security Incident Response Plan

### 1. Incident Classification

| Severity          | Examples                                              | Response Time        |
| ----------------- | ----------------------------------------------------- | -------------------- |
| **P0 - Critical** | Data breach, unauthorized access to production        | Immediate (< 1 hour) |
| **P1 - High**     | Vulnerability exploitation attempt, malware detection | < 4 hours            |
| **P2 - Medium**   | Suspicious activity, policy violation                 | < 24 hours           |
| **P3 - Low**      | Security configuration issue, false positive          | < 1 week             |

### 2. Response Team

| Role                   | Responsibilities                     | Contact       |
| ---------------------- | ------------------------------------ | ------------- |
| **Incident Commander** | Overall response coordination        | [Name, Phone] |
| **Technical Lead**     | Investigation and remediation        | [Name, Phone] |
| **Legal Counsel**      | Regulatory compliance, notifications | [Name, Phone] |
| **Communications**     | Stakeholder notifications            | [Name, Phone] |
| **Municipal IT**       | Infrastructure support               | [Name, Phone] |

### 3. Response Procedures

#### Phase 1: Detection & Triage (0-1 hour)

- [ ] Incident detected and logged
- [ ] Severity classified
- [ ] Incident Commander notified
- [ ] Initial containment measures applied
- [ ] Response team assembled

#### Phase 2: Investigation (1-4 hours)

- [ ] Scope of breach determined
- [ ] Affected systems identified
- [ ] Attack vector analyzed
- [ ] Evidence collected and preserved
- [ ] Root cause identified

#### Phase 3: Containment (4-8 hours)

- [ ] Affected systems isolated
- [ ] Malicious activity stopped
- [ ] Access credentials rotated
- [ ] Vulnerabilities patched
- [ ] Monitoring enhanced

#### Phase 4: Notification (8-72 hours)

- [ ] Legal review completed
- [ ] Regulatory notifications sent
- [ ] Affected users notified
- [ ] Public disclosure (if required)
- [ ] Credit monitoring offered (if PII breach)

#### Phase 5: Recovery (1-2 weeks)

- [ ] Systems restored from backup
- [ ] Data integrity verified
- [ ] Services resumed
- [ ] Additional security controls deployed
- [ ] Post-incident review conducted

### 4. Notification Requirements

| Regulation                | Notification Timeline      | Recipients                             |
| ------------------------- | -------------------------- | -------------------------------------- |
| **State Data Breach Law** | 72 hours (varies by state) | Affected individuals, Attorney General |
| **GDPR**                  | 72 hours                   | Supervisory authority, data subjects   |
| **CCPA**                  | Without unreasonable delay | Attorney General, consumers            |
| **IRS Pub 1075**          | 24 hours                   | IRS, Treasury Inspector General        |
| **CJIS**                  | Immediately                | FBI CJIS, state CJIS                   |

### 5. Communication Templates

[Include breach notification letter templates]
[Include regulatory report templates]
[Include internal communication templates]
```

---

## 5. Action Plan & Roadmap

### Phase 1: Critical Risk Mitigation (Weeks 1-4)

**Priority**: P0
**Budget**: $0 (use existing tools)
**Effort**: 1 developer × 4 weeks

#### Week 1-2: Vulnerability Scanning Setup

- [ ] Add .NET security analyzers to all projects
- [ ] Configure banned APIs list
- [ ] Setup GitHub Dependabot
- [ ] Enable GitHub security alerts
- [ ] Run initial OWASP Dependency-Check
- [ ] Document findings in security backlog

**Deliverable**: Security scanning infrastructure operational

#### Week 3-4: Compliance Documentation

- [ ] Legal review of privacy policy
- [ ] Legal review of terms of service
- [ ] Create GASB compliance documentation
- [ ] Document FOIA readiness procedures
- [ ] Identify applicable state regulations
- [ ] Create security incident response plan

**Deliverable**: Production-ready legal documentation

### Phase 2: Enhanced Security (Months 2-3)

**Priority**: P1
**Budget**: $5,000 (Snyk Pro, SonarCloud)
**Effort**: 2 developers × 6 weeks

#### Month 2: SAST/DAST Implementation

- [ ] Setup SonarCloud integration
- [ ] Setup Snyk for .NET
- [ ] Implement local security scan script
- [ ] Add security scans to CI/CD pipeline
- [ ] Create security dashboard
- [ ] Address high/critical findings

**Deliverable**: Continuous security scanning operational

#### Month 3: Compliance Framework

- [ ] Implement MunicipalComplianceService
- [ ] Create FOIA export functionality
- [ ] Add PII redaction tools
- [ ] Implement field-level encryption for sensitive data
- [ ] Enhanced audit logging
- [ ] Compliance report generation

**Deliverable**: Municipal data compliance framework

### Phase 3: Advanced Security (Months 4-6)

**Priority**: P2
**Budget**: $15,000 (penetration testing, security audit)
**Effort**: 2 developers × 8 weeks + external consultants

#### Month 4-5: Security Hardening

- [ ] Implement runtime security monitoring
- [ ] Add anomaly detection
- [ ] Setup security information and event management (SIEM)
- [ ] Implement advanced threat detection
- [ ] Container security hardening
- [ ] Deploy web application firewall (if applicable)

**Deliverable**: Advanced security monitoring

#### Month 6: Security Assessment

- [ ] Third-party penetration testing
- [ ] Security code review by external auditor
- [ ] Compliance audit (GASB, FOIA, state-specific)
- [ ] Vulnerability remediation
- [ ] Final security documentation
- [ ] Security certification (if pursuing)

**Deliverable**: Security assessment report, remediation plan

---

## 6. Monitoring & Metrics

### Security KPIs

```csharp
public class SecurityMetrics
{
    // Vulnerability Metrics
    public int TotalVulnerabilities { get; set; }
    public int CriticalVulnerabilities { get; set; }
    public int HighVulnerabilities { get; set; }
    public TimeSpan AverageTimeToRemediate { get; set; }

    // Scanning Coverage
    public decimal CodeCoverageBySecurityScans { get; set; }  // Percentage
    public int DependenciesScanned { get; set; }
    public int OutdatedDependencies { get; set; }

    // Incident Metrics
    public int SecurityIncidentsThisMonth { get; set; }
    public TimeSpan AverageIncidentResponseTime { get; set; }
    public int FalsePositives { get; set; }

    // Compliance Metrics
    public int ComplianceChecksPerformed { get; set; }
    public decimal ComplianceScore { get; set; }  // Percentage
    public int FOIARequestsProcessed { get; set; }
    public TimeSpan AverageFOIAResponseTime { get; set; }

    // Audit Metrics
    public long AuditEventsLogged { get; set; }
    public int FailedAccessAttempts { get; set; }
    public int SuspiciousActivities { get; set; }
}
```

### Monthly Security Review Checklist

- [ ] Review vulnerability scan results
- [ ] Update dependencies with security patches
- [ ] Review audit logs for suspicious activity
- [ ] Test backup and recovery procedures
- [ ] Review access control lists
- [ ] Update security documentation
- [ ] Conduct security awareness training
- [ ] Review compliance status
- [ ] Test incident response procedures
- [ ] Update threat model

---

## 7. Cost Estimate

| Item                         | One-Time Cost | Annual Cost      |
| ---------------------------- | ------------- | ---------------- |
| **Tools & Services**         |               |                  |
| Snyk Pro (5 users)           | -             | $4,000           |
| SonarCloud (private repo)    | -             | $1,200           |
| OWASP Dependency-Check       | $0            | $0               |
| Trivy Container Scanner      | $0            | $0               |
| GitHub Advanced Security     | -             | $0 (included)    |
| **Professional Services**    |               |                  |
| Legal review (privacy/terms) | $5,000        | -                |
| Security audit               | $15,000       | $15,000          |
| Penetration testing          | $10,000       | $10,000          |
| Compliance audit (municipal) | $8,000        | $8,000           |
| **Development Effort**       |               |                  |
| Implementation (12 weeks)    | $60,000       | -                |
| Ongoing maintenance          | -             | $30,000          |
| **Training**                 |               |                  |
| Security training            | $2,000        | $2,000           |
| **TOTAL**                    | **$100,000**  | **$70,200/year** |

---

## 8. References & Resources

### Regulations & Standards

- [GASB Standards](https://www.gasb.org/)
- [FOIA Guide](https://www.foia.gov/)
- [IRS Publication 1075](https://www.irs.gov/pub/irs-pdf/p1075.pdf)
- [CJIS Security Policy](https://www.fbi.gov/services/cjis/cjis-security-policy-resource-center)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)

### Security Tools

- [OWASP Dependency-Check](https://owasp.org/www-project-dependency-check/)
- [Snyk](https://snyk.io/)
- [SonarQube](https://www.sonarqube.org/)
- [Trivy](https://aquasecurity.github.io/trivy/)
- [GitHub Advanced Security](https://docs.github.com/en/code-security)

### Internal Documentation

- `SECURITY.md` - Current security policy
- `docs/syncfusion-license-setup.md` - License management
- `docs/DEPENDENCY_RISK_ASSESSMENT.md` - Dependency analysis
- `docs/KEYVAULT_FIX_GUIDE.md` - Secret management

---

## Document History

| Date       | Version | Author        | Changes                                                |
| ---------- | ------- | ------------- | ------------------------------------------------------ |
| 2025-11-03 | 1.0     | AI Assessment | Initial comprehensive security & compliance assessment |

---

**Next Review Date**: December 3, 2025
**Review Frequency**: Monthly for Phase 1-2, Quarterly after Phase 3
**Owner**: Security Team / Technical Lead
