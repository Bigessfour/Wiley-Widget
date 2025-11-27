# Dashboard Production Deployment Checklist

## Pre-Deployment Validation

### Configuration
- [ ] Update `config/production/appsettings.Production.json` with Dashboard section
- [ ] Verify `Dashboard.CacheDurationSeconds` set to 600 (10 minutes for production)
- [ ] Verify `Dashboard.AutoRefreshIntervalSeconds` set to 60 (1 minute for production)
- [ ] Verify `Dashboard.EnableAutoRefresh` set to `false` (manual refresh by default)
- [ ] Confirm database connection string in production config
- [ ] Validate Syncfusion license key configured (SYNCFUSION_LICENSE_KEY)

### Dependencies
- [ ] Verify Syncfusion.WinForms NuGet packages installed (v25.1.35+)
  - Syncfusion.WinForms.DataGrid
  - Syncfusion.WinForms.Chart
  - Syncfusion.Windows.Forms.Gauge
  - Syncfusion.Pdf.WinForms (for PDF export)
  - Syncfusion.XlsIO.WinForms (for Excel export)
- [ ] Verify CommunityToolkit.Mvvm (v8.2+)
- [ ] Verify Microsoft.Extensions.Configuration (v9.0+)

### Database
- [ ] Verify `AppDbContext` migrations applied
- [ ] Verify test data exists for:
  - Departments table (at least 1 department)
  - Funds table (at least 1 fund)
  - BudgetEntries table (FY 2026 data)
  - Transactions table (revenue & expense records)
  - MunicipalAccounts table (active accounts)
  - Invoices table (test invoices)
- [ ] Run database health check: `dotnet ef database update --project WileyWidget.Data`
- [ ] Verify backup strategy configured (BackupOnStartup: true in production)

### File System Permissions
- [ ] Verify application has write access to export directory (default: user Documents folder)
- [ ] Test export path permissions: Create test file in `%USERPROFILE%\Documents`
- [ ] Verify logs directory writable: `logs/production/`

### Build Validation
- [ ] Clean solution: `dotnet clean WileyWidget.sln`
- [ ] Build in Release configuration: `dotnet build WileyWidget.sln --configuration Release --no-restore`
- [ ] Verify zero build errors
- [ ] Run Trunk validation: `trunk check --ci`
- [ ] Verify zero critical issues in Problems panel

### Test Validation
- [ ] Run all unit tests: `dotnet test --filter FullyQualifiedName~Dashboard --configuration Release`
- [ ] Verify all 14 DashboardE2ETests pass
- [ ] Run DashboardServiceTests (11 tests)
- [ ] Manual UI test: Launch dashboard, verify load
- [ ] Manual export test: Export to PDF and Excel, verify files created

## Deployment Steps

### 1. Application Deployment
- [ ] Publish application: `dotnet publish WileyWidget.WinForms/WileyWidget.WinForms.csproj --configuration Release --output ./publish`
- [ ] Copy published files to production server/workstation
- [ ] Copy `config/production/appsettings.Production.json` to deployment directory
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` or `DOTNET_ENVIRONMENT=Production` environment variable

### 2. Database Setup
- [ ] Backup production database (if applicable)
- [ ] Run migrations: `dotnet ef database update --project WileyWidget.Data --connection "production_connection_string"`
- [ ] Verify data integrity: Run `SELECT COUNT(*) FROM BudgetEntries WHERE FiscalYear = 2026`

### 3. Configuration
- [ ] Update connection string in production appsettings.json
- [ ] Configure dashboard settings:
  - Cache duration (recommended: 600s for production)
  - Auto-refresh interval (recommended: 60s or disabled)
  - Max metrics displayed (recommended: 10)
- [ ] Set Syncfusion license key in environment variable or config

### 4. Security
- [ ] Verify secrets management configured (see secrets/README.md)
- [ ] Validate critical secrets present:
  - Database connection string encrypted
  - Syncfusion license key secured
- [ ] Review file system permissions (minimum required)
- [ ] Enable audit logging (Security.EnableSecurityAuditLogging: true)

## Post-Deployment Validation

### Smoke Tests
- [ ] Launch application
- [ ] Navigate to Dashboard via toolbar button
- [ ] Verify dashboard loads without errors (all 4 gauges visible)
- [ ] Verify metrics grid populates (10 rows expected)
- [ ] Verify chart displays revenue trend
- [ ] Click Refresh button - verify LastUpdated timestamp changes
- [ ] Test export to PDF:
  - Click Export button
  - Select PDF format
  - Save to Documents folder
  - Verify file created and opens correctly
- [ ] Test export to Excel:
  - Click Export button
  - Select Excel format
  - Verify file created and opens in Excel
- [ ] Test auto-refresh toggle:
  - Check "Auto-refresh (30s)" checkbox
  - Wait 31 seconds
  - Verify LastUpdated timestamp changes
  - Uncheck checkbox
  - Verify no further updates

### Performance Tests
- [ ] Monitor memory usage during dashboard load (< 200MB expected)
- [ ] Measure dashboard load time (target: < 2 seconds)
- [ ] Verify cache effectiveness:
  - First load: ~1-2 seconds
  - Cached load: < 500ms
- [ ] Test concurrent users (if multi-user scenario)
- [ ] Monitor database query performance (< 1 second for all queries)

### Error Handling
- [ ] Test with no database connection (should show error message)
- [ ] Test export to read-only directory (should show error dialog)
- [ ] Test with invalid fiscal year (should handle gracefully)
- [ ] Verify error logging to `logs/production/errors-.log`

### Monitoring
- [ ] Verify Serilog logging to production log files
- [ ] Check `logs/production/wiley-widget-.log` for startup messages
- [ ] Verify no unhandled exceptions in error log
- [ ] Configure Application Insights (if enabled)
- [ ] Set up SigNoz monitoring (if configured)

## Rollback Plan

### If deployment fails:
1. Stop application
2. Restore previous application version from backup
3. Restore database from backup (if migrations were run)
4. Verify previous version operational
5. Document failure reason for investigation

### Rollback commands:
```powershell
# Stop application processes
Get-Process -Name WileyWidget* | Stop-Process -Force

# Restore application directory
Copy-Item -Path ".\backup\WileyWidget.WinForms" -Destination ".\production" -Recurse -Force

# Restore database (if needed)
# Invoke-Sqlcmd -InputFile ".\backup\database_backup.sql" -ServerInstance "production_server"
```

## Production Monitoring

### Daily Checks
- [ ] Review error logs: `logs/production/errors-.log`
- [ ] Monitor cache hit rate (should be > 80%)
- [ ] Check dashboard load times (should be < 2 seconds)
- [ ] Verify export functionality working

### Weekly Checks
- [ ] Review performance metrics
- [ ] Check disk space for log files
- [ ] Validate backup retention (90 days configured)
- [ ] Review user feedback/issues

### Monthly Checks
- [ ] Performance benchmarking
- [ ] Security audit log review
- [ ] Configuration review and optimization
- [ ] Update dependencies (Syncfusion, .NET security patches)

## Support Contacts

- **Development Team**: [Team Contact]
- **Database Admin**: [DBA Contact]
- **Infrastructure**: [Infra Contact]
- **Syncfusion Support**: https://www.syncfusion.com/support

## Known Issues & Workarounds

### Issue 1: Export fails with "Access Denied"
**Workaround**: Ensure application has write permissions to export directory. Run as administrator if needed.

### Issue 2: Dashboard loads slowly on first access
**Expected Behavior**: First load queries database and populates cache. Subsequent loads use cache (5-minute TTL).

### Issue 3: Auto-refresh consumes bandwidth
**Recommendation**: Disable auto-refresh in production (Dashboard.EnableAutoRefresh: false) and use manual refresh.

## Version Information

- **Dashboard Version**: 1.0.0 (Production Ready)
- **Last Updated**: November 26, 2025
- **.NET Version**: 9.0
- **Syncfusion Version**: 25.1.35+
- **EF Core Version**: 9.0

## Deployment Sign-Off

- [ ] Developer sign-off: ______________________ Date: ______
- [ ] QA sign-off: ______________________ Date: ______
- [ ] Operations sign-off: ______________________ Date: ______
- [ ] Business owner sign-off: ______________________ Date: ______

---

**Deployment Status**: ☐ Pending | ☐ In Progress | ☐ Completed | ☐ Rolled Back

**Deployment Date**: _______________

**Deployed By**: _______________

**Production URL/Server**: _______________
