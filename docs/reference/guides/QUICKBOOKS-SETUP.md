# QuickBooks Development Section - Registration Guide

## ✅ Your Credentials (Already Configured)

- **Client ID**: `ABWlf3T7raiKwVV8ILahdlGP7E5pblC6pH1i6lXZQoU6wloEOm`
- **Client Secret**: `fe9vsxTE6EhwGI14NAHF9w7CvvcQlr0wWJmkv9Wi`
- **Environment**: `sandbox` (Development)

---

## 📝 Complete Your Development Registration

### **What You Need to Fill In (Development Section)**

#### 1. **Redirect URIs**

Add these exact URLs (you can add multiple):

```
http://localhost:8080/callback
http://127.0.0.1:8080/callback
```

**Important**:

- Use `http://` not `https://` for local development
- Include the port `:8080`
- Path must be `/callback`

#### 2. **App Host Domain** (if required)

- Leave **BLANK** or use: `127.0.0.1`
- **DO NOT** use `localhost` alone (causes errors)

#### 3. **Launch URL** (if required)

```
http://localhost:8080/
```

#### 4. **Disconnect URL** (if required)

```
http://localhost:8080/disconnect
```

#### 5. **Privacy Policy URL**

For sandbox/development, use a placeholder:

```
http://localhost:8080/privacy.html
```

Or if you have a website: `https://yourdomain.com/privacy`

#### 6. **End-User License Agreement (EULA)**

For sandbox/development, use a placeholder:

```
http://localhost:8080/terms.html
```

Or if you have a website: `https://yourdomain.com/terms`

---

## 🎯 App Configuration Options

### **App Category**

- ✅ Select: **Accounting** or **Business Management**

### **Regulated Industries**

- ✅ **Government/Municipality** (for Town of Wiley)

### **App Hosting**

- ✅ **On-premise** (Desktop application)

---

## 🔒 Production vs Development

### **Development (Current Setup)**

- **Client ID**: `ABWlf3T7raiKwVV8ILahdlGP7E5pblC6pH1i6lXZQoU6wloEOm`
- **Environment**: Sandbox
- **Purpose**: Testing and development
- **Redirect**: `http://localhost:8080/callback`

### **Production (Future Setup)**

- Will have **different** Client ID and Secret
- **Environment**: Production
- **Purpose**: Live customer data
- **Redirect**: Will need public URL (e.g., `https://yourdomain.com/callback`)

**⚠️ NEVER mix production and development credentials!**

---

## ✅ Verification Checklist

After completing the registration, verify:

- [ ] Client ID and Secret saved in environment variables
- [ ] Redirect URIs added to Development section
- [ ] Privacy policy URL added
- [ ] Terms/EULA URL added
- [ ] App category selected
- [ ] App hosting type selected

---

## 🚀 Testing Your Configuration

### Step 1: Test Connection

```powershell
.\test-quickbooks-connection.ps1
```

### Step 2: Run Wiley Widget

```powershell
dotnet run --project WileyWidget.csproj
```

### Step 3: Authorize in App

1. Launch Wiley Widget
2. Go to **Settings** → **QuickBooks** section
3. Click **"Connect to QuickBooks"** button
4. You'll be redirected to Intuit login
5. Log in with your **Intuit Developer account**
6. Authorize the app
7. You'll be redirected back to `localhost:8080/callback`
8. Wiley Widget will save the tokens

### Step 4: Verify Connection

- Check that QuickBooks status shows "Connected"
- Try loading customers or accounts
- Check logs for any errors

---

## 🐛 Common Issues & Solutions

### Issue: "Invalid Redirect URI"

**Solution**: Make sure you added the EXACT redirect URI to the Development section:

- Must be `http://localhost:8080/callback` (not `https://`)
- Must include port number
- Must match exactly what's in your app

### Issue: "Invalid Client"

**Solution**:

- Verify Client ID and Secret are correct
- Check that you're using **Development** credentials, not Production
- Restart PowerShell to reload environment variables

### Issue: "Realm ID Required"

**Solution**:

- Realm ID is obtained AFTER first OAuth authorization
- It will be saved automatically after you authorize
- The default sandbox company Realm ID is: `9341455168020461`

### Issue: Environment Variables Not Loading

**Solution**:

```powershell
# Reload environment variables
$env:QBO_CLIENT_ID = [Environment]::GetEnvironmentVariable('QBO_CLIENT_ID', 'User')
$env:QBO_CLIENT_SECRET = [Environment]::GetEnvironmentVariable('QBO_CLIENT_SECRET', 'User')
$env:QBO_ENVIRONMENT = [Environment]::GetEnvironmentVariable('QBO_ENVIRONMENT', 'User')

# Verify
Write-Output "Client ID: $env:QBO_CLIENT_ID"
Write-Output "Environment: $env:QBO_ENVIRONMENT"
```

---

## 📚 Additional Resources

- **QuickBooks Sandbox**: <https://developer.intuit.com/app/developer/sandbox>
- **OAuth 2.0 Guide**: <https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/oauth-2.0>
- **API Explorer**: <https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account>

---

## 🔐 Security Best Practices

1. **Never commit credentials to Git**
   - Credentials are stored in User environment variables
   - Add `appsettings.Development.json` to `.gitignore` if you store them there

2. **Use separate credentials for production**
   - Development credentials should ONLY access sandbox data
   - Production credentials are obtained separately

3. **Rotate credentials regularly**
   - If credentials are ever exposed, regenerate them in the Intuit Developer Portal

4. **Store production credentials in Azure Key Vault**
   - Wiley Widget already has Azure Key Vault integration configured
   - See `docs/Secrets.md` for details

---

## 📞 Need Help?

- **Intuit Developer Support**: <https://help.developer.intuit.com/>
- **Developer Forums**: <https://help.developer.intuit.com/s/>
- **QuickBooks API Reference**: <https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account>
