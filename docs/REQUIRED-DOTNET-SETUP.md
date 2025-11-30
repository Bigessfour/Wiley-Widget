**.NET SDK Requirements**


Why: The repository targets `net10.0` and requires MSBuild v18+ to correctly import and run custom targets such as `CompileXaml`.

Quick verification (run from repo root):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\maintenance\verify-dotnet-sdk.ps1
```

If the script fails, install the .NET 10 SDK for your OS:


After installing, re-run the verification script and then run your build (or `dotnet --version` / `dotnet --info` to confirm).

## Backup policy (recommended)

We run an industry-standard SQL Server backup strategy for the WileyWidget database to protect against data loss and enable fast recovery.

- Recovery Time Objective (RTO): aim to restore services within 1 hour for production incidents.
- Recovery Point Objective (RPO): transaction-log backups every 15 minutes to limit data loss to at most 15 minutes.

Recommended backup schedule (example):

- Full backup: every 24 hours (midnight) — keep daily full backups for 14 days.
- Differential backup: every 4 hours (between full backups) — keep last 3–7 differentials.
- Transaction log backup: every 15 minutes — keep log backups for 14 days.

Retention, storage & security:

- Store backups to an offsite location (Azure Blob, S3, or replicated NAS) with encryption at rest.
- Use backup compression to reduce storage and network cost.
- Protect backup credentials: use a service account with just the required rights and rotate keys/credentials quarterly.
- Consider enabling Transparent Data Encryption (TDE) for full-disk protection of backups in case disks are lost.

Operational guidance and verification:

- Automate backups with SQL Agent (preferred on SQL Server) or a Windows PowerShell script running under Task Scheduler.
- Configure alerting for failed backups and successful verification runs.
- Periodically (at least weekly) run restores to a staging environment to validate recovery and test scripts.

Next steps in this repository: we add scripts and tasks to perform full / differential / log backups, an automated restore script for staging, and CI/CD integration for offsite retention.

