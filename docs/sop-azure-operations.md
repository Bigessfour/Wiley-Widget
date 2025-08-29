# WileyWidget Standard Operating Procedures
# Azure Operations Safety Protocol

## Document Control
- **Document ID**: SOP-AZURE-001
- **Version**: 1.0
- **Effective Date**: August 29, 2025
- **Review Date**: September 29, 2025
- **Author**: WileyWidget Development Team
- **Purpose**: Establish safe Azure operations procedures for all team members

## 1. Purpose and Scope

### 1.1 Purpose
This Standard Operating Procedure (SOP) establishes mandatory safety protocols for all Azure operations within the WileyWidget project. The goal is to prevent data loss, configuration errors, and service disruptions while enabling safe learning and development.

### 1.2 Scope
This SOP applies to:
- All Azure resource management activities
- Database operations (Azure SQL)
- Azure CLI usage
- GitHub Copilot Chat Azure assistance
- Development and production environments

### 1.3 Target Audience
- Novice Azure developers
- Experienced developers working on Azure components
- DevOps engineers
- Project maintainers

## 2. Safety Principles

### 2.1 Core Safety Rules
**MANDATORY COMPLIANCE REQUIRED**

1. **Safe Scripts Only**: All Azure operations must use approved safe scripts
2. **Dry Run First**: Every operation must be tested with `-DryRun` before execution
3. **Backup Before Change**: Automatic backups required before any destructive operations
4. **Status Verification**: Check system status before and after operations
5. **Educational Approach**: Learn while maintaining safety

### 2.2 Forbidden Practices
**STRICTLY PROHIBITED**

- Direct Azure CLI commands without safe script alternatives
- Operations without dry-run testing
- Changes without prior backups
- Unsupervised destructive operations
- Bypassing safety protocols

## 3. Approved Azure Operations

### 3.1 Safe Script Operations
**ONLY these methods are approved for Azure operations:**

#### Status Operations (Safe, Read-Only)
```powershell
# Check Azure account and resource status
.\scripts\azure-safe-operations.ps1 -Operation status

# List all Azure resources
.\scripts\azure-safe-operations.ps1 -Operation list
```

#### Database Operations (Safe, Read-Only)
```powershell
# Test database connection
.\scripts\azure-safe-operations.ps1 -Operation connect

# Create database backup (safe copy)
.\scripts\azure-safe-operations.ps1 -Operation backup
```

#### Dry Run Testing (MANDATORY)
```powershell
# Test ANY operation before execution
.\scripts\azure-safe-operations.ps1 -Operation [operation] -DryRun
```

### 3.2 GitHub Copilot Chat Integration
**Approved Copilot interaction patterns:**

#### Safe Questions
```
✅ "How do I safely check my Azure database connection?"
✅ "Show me how to create a backup using the safe script"
✅ "Explain Azure Resource Groups in simple terms"
✅ "What would happen if I run this command? Explain first"
```

#### Educational Questions
```
✅ "I'm new to Azure - help me understand [concept]"
✅ "Show me the safe way to [operation]"
✅ "Explain this Azure error in simple terms"
```

#### Prohibited Questions
```
❌ "Delete my Azure database"
❌ "Run this az sql db delete command"
❌ "Execute this Azure CLI command for me"
```

## 4. Operational Procedures

### 4.1 Pre-Operation Checklist
**COMPLETE ALL ITEMS BEFORE any Azure operation:**

- [ ] Review this SOP document
- [ ] Verify safe script availability
- [ ] Check current Azure status
- [ ] Create backup if operation is destructive
- [ ] Test operation with `-DryRun`
- [ ] Confirm understanding of operation impact
- [ ] Have recovery plan ready

### 4.2 Standard Operation Workflow

#### Step 1: Status Check
```powershell
# Always start with status check
.\scripts\azure-safe-operations.ps1 -Operation status
```

#### Step 2: Dry Run Test
```powershell
# Test the planned operation
.\scripts\azure-safe-operations.ps1 -Operation [operation] -DryRun
```

#### Step 3: Backup (if needed)
```powershell
# Create backup before destructive operations
.\scripts\azure-safe-operations.ps1 -Operation backup
```

#### Step 4: Execute Operation
```powershell
# Execute only after testing and understanding
.\scripts\azure-safe-operations.ps1 -Operation [operation]
```

#### Step 5: Verify Results
```powershell
# Confirm operation success
.\scripts\azure-safe-operations.ps1 -Operation status
```

### 4.3 Emergency Procedures

#### If Operation Fails
1. **Don't Panic**: Most Azure issues are recoverable
2. **Check Status**: `.\scripts\azure-safe-operations.ps1 -Operation status`
3. **Review Logs**: Check Azure portal activity logs
4. **Contact Support**: Use Azure support or community forums
5. **Document Incident**: Record what happened for learning

#### Data Recovery
1. **Check Backups**: Verify backup availability
2. **Use Azure Tools**: Leverage Azure backup/restore features
3. **Seek Help**: Ask in Azure communities or support
4. **Learn from Incident**: Document lessons learned

## 5. Training and Certification

### 5.1 Required Training
All team members must complete:
- [ ] Azure Safety Protocol training
- [ ] Safe Script operation training
- [ ] Dry-run procedure training
- [ ] Emergency response training

### 5.2 Certification Requirements
- [ ] Demonstrate safe script usage
- [ ] Complete dry-run exercises
- [ ] Pass safety protocol quiz
- [ ] Shadow experienced operator

### 5.3 Ongoing Education
- [ ] Weekly Azure safety reminders
- [ ] Monthly protocol reviews
- [ ] Quarterly certification renewal
- [ ] Continuous learning resources

## 6. Monitoring and Compliance

### 6.1 Operation Logging
All Azure operations are automatically logged:
- Operation type and timestamp
- User identification
- Success/failure status
- Dry-run vs. actual execution
- Error messages and recovery actions

### 6.2 Compliance Monitoring
- [ ] Regular audit of Azure operations
- [ ] Safety protocol compliance checks
- [ ] Training completion verification
- [ ] Incident response effectiveness

### 6.3 Performance Metrics
- [ ] Operation success rate
- [ ] Safety incident frequency
- [ ] Training completion rates
- [ ] User satisfaction scores

## 7. Roles and Responsibilities

### 7.1 Novice Developer
- Follow all safety protocols
- Use safe scripts exclusively
- Complete required training
- Report safety concerns immediately

### 7.2 Experienced Developer
- Mentor novice developers
- Review safety procedures
- Assist with complex operations
- Maintain safety documentation

### 7.3 Project Lead
- Ensure protocol compliance
- Review safety incidents
- Approve procedure updates
- Manage training programs

## 8. Document Maintenance

### 8.1 Review Schedule
- **Monthly**: Safety protocol effectiveness
- **Quarterly**: Complete procedure review
- **Annually**: Comprehensive audit and update

### 8.2 Update Procedures
1. **Identify Need**: Safety incident or technology change
2. **Draft Update**: Create proposed changes
3. **Review Process**: Team review and approval
4. **Training**: Update training materials
5. **Implementation**: Roll out changes safely

### 8.3 Version Control
- All changes tracked in Git
- Version history maintained
- Previous versions archived
- Change rationale documented

## 9. References and Resources

### 9.1 Key Documents
- [Azure Safety Guide](docs/azure-novice-guide.md)
- [Copilot Azure Examples](docs/copilot-azure-examples.md)
- [Development Guide](docs/development-guide.md)
- [Project Plan](.vscode/project-plan.md)

### 9.2 External Resources
- Microsoft Azure Documentation
- Azure CLI Reference
- Azure Community Forums
- Microsoft Learn: Azure Fundamentals

### 9.3 Emergency Contacts
- Azure Support: https://azure.microsoft.com/support
- Microsoft Learn Community
- Project Lead: [contact information]

## 10. Approval and Sign-off

### 10.1 Document Approval
This SOP has been reviewed and approved by:
- [ ] Project Lead
- [ ] Development Team
- [ ] Safety Officer

### 10.2 Training Acknowledgment
All team members must acknowledge reading and understanding this SOP:
- [ ] I have read and understood the Azure Safety Protocol
- [ ] I agree to follow all safety procedures
- [ ] I will complete required training
- [ ] I will report safety concerns immediately

**Signed**: ___________________________ **Date**: ____________

---

## APPENDIX A: Quick Reference Guide

### Emergency Commands
```powershell
# Check system status
.\scripts\azure-safe-operations.ps1 -Operation status

# Test database connection
.\scripts\azure-safe-operations.ps1 -Operation connect

# Create emergency backup
.\scripts\azure-safe-operations.ps1 -Operation backup
```

### Daily Operations
```powershell
# Morning status check
.\scripts\azure-safe-operations.ps1 -Operation status

# Before any work
.\scripts\azure-safe-operations.ps1 -Operation connect

# End of day verification
.\scripts\azure-safe-operations.ps1 -Operation status
```

### Learning Resources
- [Azure for Beginners](docs/azure-novice-guide.md)
- [Copilot Integration](docs/copilot-azure-examples.md)
- [Microsoft Learn](https://learn.microsoft.com/azure)

---

**REMEMBER: Safety is not optional. These procedures protect both your work and your learning journey.**
