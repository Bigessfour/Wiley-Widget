# xAI API Key Setup Guide for WileyWidget

## 🚀 Quick Start for Non-Technical Users

This guide will walk you through setting up your xAI API key step-by-step. Don't worry if you're not technical - we've made this as simple as possible!

---

## 📋 What You Need

- ✅ A computer with WileyWidget installed
- ✅ Internet connection
- ✅ A free xAI account (we'll help you create one)
- ✅ About 5 minutes of your time

---

## � Step 1: Create Your xAI Account

### Option A: If You Don't Have an xAI Account
1. **Open your web browser** (Chrome, Firefox, Edge, etc.)
2. **Go to:** https://x.ai
3. **Click** "Sign Up" or "Get Started"
4. **Fill in your details:**
   - Email address
   - Password (make it strong!)
   - Agree to terms
5. **Check your email** for a verification link
6. **Click the link** to activate your account

### Option B: If You Already Have an xAI Account
1. **Go to:** https://x.ai
2. **Click** "Sign In"
3. **Enter your email and password**

---

## � Step 2: Get Your API Key

1. **After signing in**, look for your profile menu (usually top-right corner)
2. **Click on your profile** or "Account Settings"
3. **Find the "API Keys" section** (it might be under "Developer" or "API")
4. **Click** "Create API Key" or "Generate New Key"
5. **Give it a name** like "WileyWidget Budget Analysis"
6. **Click** "Create" or "Generate"
7. **IMPORTANT:** Copy the API key immediately and save it somewhere safe
   - 🔴 **Never share this key with anyone!**
   - 🔴 **Don't email it or post it anywhere!**

---

## ⚙️ Step 3: Configure WileyWidget

### Method 1: Easy Setup (Recommended)

1. **Open WileyWidget** on your computer
2. **Click on the "Settings" tab** at the top
3. **Find the "xAI API Configuration" section**
4. **Click in the "API Key" box**
5. **Paste your API key** (Ctrl+V or right-click → Paste)
6. **Click "Test API Key"** - you should see a green success message
7. **Click "Save Settings"** - your key is now securely stored!

### Method 2: Advanced Setup (For IT Administrators)

If your organization requires environment variables:

1. **Right-click** on "This PC" or "My Computer"
2. **Click** "Properties"
3. **Click** "Advanced system settings"
4. **Click** "Environment Variables"
5. **Under "User variables", click "New"**
6. **Variable name:** `WILEYWIDGET_XAI_API_KEY`
7. **Variable value:** Paste your API key here
8. **Click "OK"** on all windows
9. **Restart WileyWidget**

---

## ✅ Step 4: Verify Everything Works

1. **Go back to WileyWidget**
2. **Click on the "Enterprises" tab**
3. **Click** "Crunch with Grok" button
4. **You should see AI analysis** of your budget data!

---

## 🔒 Security Information

### How Your API Key is Protected

WileyWidget uses **military-grade encryption** to keep your API key safe:

- 🔐 **Encrypted Storage:** Your key is encrypted before being saved
- 🚫 **Never Logged:** Your key is never written to log files
- 🔑 **Secure Access:** Only WileyWidget can read your stored key
- 🗑️ **Safe Deletion:** You can completely remove your key anytime

### Storage Methods (Automatic)

WileyWidget tries these methods in order:

1. **Environment Variable** (most secure for production)
2. **User Secrets** (secure for development)
3. **Encrypted Local File** (fallback option)

---

## 🆘 Troubleshooting Common Issues

### ❌ "API Key Not Found" Error
- **Check if you saved the key** after pasting it
- **Try the "Test API Key" button** to verify it's working
- **Restart WileyWidget** if the key doesn't load
- **Check Windows environment variables** if using advanced setup

### ❌ "Invalid API Key" Error
- **Double-check for typos** when copying from xAI
- **Make sure you copied the entire key** (no missing characters)
- **Try generating a new key** from xAI if the old one is corrupted
- **Check xAI account** for any account issues

### ❌ "Network Connection Failed" Error
- **Check your internet connection**
- **Try again in a few minutes**
- **Check xAI status page** for any outages
- **Verify firewall/proxy settings** aren't blocking the connection

### ❌ "Encryption Error" Error
- **This is rare** - usually indicates a system issue
- **Try removing and re-adding your API key**
- **Check that WileyWidget has proper file permissions**
- **Contact support** if the issue persists

---

## 🔑 Managing Your API Key

### How to Update Your API Key
1. **Get a new key** from xAI (see Step 2 above)
2. **Open WileyWidget Settings**
3. **Paste the new key** in the API Key box
4. **Click "Test API Key"** to verify it works
5. **Click "Save Settings"**
6. **Delete the old key** from your xAI account

### How to Remove Your API Key
1. **Open WileyWidget Settings**
2. **Click "Remove API Key"** button
3. **Confirm the removal**
4. **Your key is completely deleted** from WileyWidget

### How to Check Key Status
1. **Open WileyWidget Settings**
2. **Look at "API Key Status"** - it will show:
   - ✅ **Valid and Active**
   - ❌ **Invalid or Expired**
   - ⚠️ **Not Configured**

---

## 💡 Advanced Configuration

### For Developers and IT Teams

#### Environment Variables (Most Secure)
Set the environment variable for production use:
```
WILEYWIDGET_XAI_API_KEY=your-api-key-here
```

#### User Secrets (Development)
WileyWidget automatically uses .NET user secrets when available.

#### Programmatic Access
```csharp
// Get API key securely
var apiKey = ApiKeyService.Instance.GetApiKey();

// Test API key
var isValid = await ApiKeyService.Instance.TestApiKeyAsync(apiKey);
```

---

## � Security Best Practices

### ✅ Do's
- ✅ **Use strong, unique passwords** for your xAI account
- ✅ **Enable 2FA** on your xAI account when available
- ✅ **Monitor your usage** in xAI dashboard
- ✅ **Rotate keys** every 3-6 months
- ✅ **Use different keys** for different environments

### ❌ Don'ts
- ❌ **Never share your API key** with anyone
- ❌ **Don't email your API key**
- ❌ **Don't commit keys** to version control
- ❌ **Don't store keys** in plain text files
- ❌ **Don't use the same key** across multiple applications

---

## 📊 Understanding Costs

### xAI Pricing
- **Free Tier:** Limited usage for testing
- **Paid Plans:** Starting at $10/month for higher limits
- **Pay-per-use:** ~$0.01 per API call

### Monitoring Usage
1. **Log in to xAI account**
2. **Go to API Keys section**
3. **View usage statistics**
4. **Set up billing alerts**

---

## 📞 Support and Resources

### Getting Help
1. **Check this guide** - most questions are answered here
2. **Visit xAI documentation:** https://docs.x.ai/
3. **Check WileyWidget logs** for technical details
4. **Contact WileyWidget support** for application-specific issues

### Useful Links
- **xAI Developer Documentation:** https://docs.x.ai/
- **xAI Status Page:** Check for outages
- **WileyWidget GitHub:** Report issues or request features
- **.NET User Secrets Documentation:** For advanced setup

---

## 🎉 Success!

**Congratulations!** You've successfully configured your xAI API key with WileyWidget.

### What You Can Do Now
- 🤖 **AI-powered budget analysis** with Grok
- 📊 **Intelligent financial insights**
- 🎯 **Advanced scenario modeling**
- 💰 **Automated budget optimization**

### Next Steps
1. **Try the "Crunch with Grok" feature** on your budget data
2. **Explore different AI analysis options**
3. **Set up regular automated analysis**
4. **Monitor your xAI usage and costs**

---

*Your WileyWidget is now enhanced with powerful AI capabilities! Keep your API key secure and enjoy intelligent budget analysis.* �🤖</content>
<parameter name="filePath">c:\Users\biges\Desktop\Wiley_Widget\docs\xAI-API-Key-Setup-Guide.md
