# BrightData MCP Integration

## Overview
BrightData MCP (Model Context Protocol) server is configured for web data collection and search capabilities.

## Current Status
- ✅ **Local Configuration**: Properly set up with secure environment variables
- ✅ **Security**: Bearer token authentication and HTTPS endpoints implemented
- ❌ **MCP Server Connection**: 404 Not Found (requires account/service activation)

## Quick Setup Check

### 1. Environment Variable
```powershell
# Check if API key is accessible
$env:BRIGHTDATA_API_KEY
```

### 2. Configuration Test
```powershell
# Run the diagnostic script
.\scripts\brightdata-diagnostic.ps1
```

### 3. Security Test
```powershell
# Run the security test
.\scripts\test-brightdata-mcp.ps1
```

## Troubleshooting

### Common Issues

#### 404 Not Found Error
**Cause**: MCP service not activated for your BrightData account
**Solution**:
1. Visit https://brightdata.com/cp/mcp
2. Ensure MCP service is enabled
3. Verify API key has MCP permissions
4. Check subscription tier supports MCP

#### API Key Issues
**Symptoms**: Authentication failures or invalid key errors
**Solution**:
1. Regenerate API key in BrightData dashboard
2. Update machine environment variable
3. Run diagnostic script to verify

#### Service Unavailable
**Symptoms**: Network timeouts or service unavailable errors
**Solution**:
1. Check BrightData status page
2. Verify account has active subscription
3. Contact BrightData support

## Configuration Files
- **MCP Server**: `.vscode/mcp.json` - VS Code MCP server configuration
- **Environment**: Machine environment variable `BRIGHTDATA_API_KEY`
- **Service**: `Services/BrightDataService.cs` - .NET service integration
- **Settings**: `appsettings.json` - Application configuration

## Security Implementation
- ✅ Environment variables for sensitive data
- ✅ Bearer token authentication
- ✅ HTTPS endpoints only
- ✅ API key validation
- ✅ Proper timeout and User-Agent headers
- ✅ Secure header configuration

## Testing Commands

### Basic Connectivity Test
```powershell
# Test MCP server
.\scripts\test-brightdata-mcp.ps1
```

### Comprehensive Diagnostics
```powershell
# Full diagnostic report
.\scripts\brightdata-diagnostic.ps1
```

### Manual API Test
```bash
# Test BrightData API directly
curl -H "Authorization: Bearer YOUR_API_KEY" https://api.brightdata.com/status
```

## Next Steps

### Immediate Actions
1. **Visit BrightData Dashboard**: https://brightdata.com/cp/mcp
2. **Activate MCP Service**: Enable MCP for your account
3. **Verify API Key**: Ensure it has MCP permissions
4. **Test Connection**: Run diagnostic scripts

### If Issues Persist
1. **Contact Support**: BrightData customer support
2. **Review Documentation**: Check the GitHub showcase repository
3. **Alternative Integration**: Consider direct API integration if MCP unavailable

## Documentation Links
- **MCP Control Panel**: https://brightdata.com/cp/mcp
- **VS Code Integration**: https://docs.brightdata.com/mcp-server/integrations/vscode
- **Agent Showcase**: https://github.com/brightdata/brightdata-agent-showcase
- **API Documentation**: https://docs.brightdata.com/

## Support
For issues with BrightData MCP integration:
1. Run the diagnostic script: `.\scripts\brightdata-diagnostic.ps1`
2. Check the troubleshooting section above
3. Contact BrightData support with diagnostic output
4. Review the GitHub showcase repository for examples
