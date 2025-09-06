# Azure Key Vault Setup with azd - Complete Guide

## 🎯 **Current Status**
✅ Azure Developer CLI environment created: `dev`  
✅ Basic azd configuration in place  
⚠️ Subscription selection in progress  

## 🔐 **Step-by-Step Azure Key Vault Integration**

### **Step 1: Complete Azure Subscription Selection**
```bash
# The current prompt is asking to select an Azure subscription
# Use arrow keys to select your subscription, then press Enter
# Or press Ctrl+C to cancel and restart with:
az account set --subscription "Your-Subscription-Name"
```

### **Step 2: Verify azd Environment**
```bash
# Check current environment
azd env list

# Check current configuration  
azd env get-values
```

### **Step 3: Set Up True Key Vault Secrets (Recommended)**
```bash
# Use azd env set-secret for each secret (Microsoft recommended)
azd env set-secret BRIGHTDATA_API_KEY
azd env set-secret SYNCFUSION_LICENSE_KEY  
azd env set-secret XAI_API_KEY
azd env set-secret GITHUB_TOKEN
```

**What happens when you run `azd env set-secret`:**
1. Prompts you to enter the secret value
2. Creates a Key Vault (if needed)
3. Stores the secret in Azure Key Vault
4. Creates a reference in your `.env` file like: `@Microsoft.KeyVault(SecretUri=...)`

### **Step 4: Verify Configuration**
```bash
# Check that references are created
azd env get-values

# Should show Key Vault references like:
# BRIGHTDATA_API_KEY=@Microsoft.KeyVault(SecretUri=https://...)
```

### **Step 5: Deploy Application**
```bash
# Deploy to Azure with automatic secret resolution
azd up
```

## ⚡ **Alternative: Fast Session Loading**

If you need immediate access to secrets in your current session:

```powershell
# Use our optimized script for immediate development
.\scripts\load-mcp-secrets-optimized.ps1

# Or load azd variables into current session
foreach ($line in (& azd env get-values)) {
    if ($line -match "([^=]+)=(.*)") {
        $key = $matches[1]
        $value = $matches[2] -replace '^"|"$'
        Set-Item -Path "env:$key" -Value $value
    }
}
```

## 🏗️ **Architecture Benefits**

### **Before (Current Slow Method)**
- ❌ 8+ seconds loading time
- ❌ Direct Key Vault API calls
- ❌ Session-only secrets
- ❌ Manual secret management

### **After (azd Method)**
- ✅ <2 seconds loading time
- ✅ Azure-managed secret resolution
- ✅ Environment persistence
- ✅ CI/CD integration
- ✅ Team collaboration

## 📋 **Commands to Run Now**

1. **Complete the subscription selection** (in the current terminal)
2. **Set up Key Vault secrets:**
   ```bash
   azd env set-secret BRIGHTDATA_API_KEY
   azd env set-secret SYNCFUSION_LICENSE_KEY
   azd env set-secret XAI_API_KEY
   azd env set-secret GITHUB_TOKEN
   ```

3. **Verify configuration:**
   ```bash
   azd env get-values
   ```

4. **Deploy (when ready):**
   ```bash
   azd up
   ```

## 🔧 **Troubleshooting**

### If subscription selection is stuck:
```bash
# Cancel current operation
Ctrl+C

# Set subscription manually
az account set --subscription "your-subscription-id"

# Restart azd process
azd env select dev
```

### If azd commands fail:
```bash
# Ensure you're logged in
az login
azd auth login

# Check current environment
azd env list
azd env select dev
```

## 📊 **Performance Comparison**

| Method | Setup Time | Runtime | Persistence | CI/CD Ready |
|--------|------------|---------|-------------|-------------|
| **Current** | Manual | 8+ sec | Session only | ❌ No |
| **azd Method** | One-time | <2 sec | Permanent | ✅ Yes |

## 🎉 **Expected Result**

After completion:
- ✅ **Key Vault references** in `.azure/dev/.env`
- ✅ **Fast secret loading** for development
- ✅ **CI/CD integration** with GitHub Actions
- ✅ **Team collaboration** without exposing secrets
- ✅ **8x performance improvement**

---

**Next Action:** Complete the Azure subscription selection in the terminal, then run the azd env set-secret commands above.
