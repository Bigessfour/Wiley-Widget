# GitHub MCP Server Setup Guide

## ✅ Configuration Added

The GitHub MCP server has been configured in `.vscode/settings.json` with the following setup:

```json
"github": {
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-github"],
  "env": {
    "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_TOKEN}"
  }
}
```

## 📋 Requirements

### 1. GitHub Personal Access Token (PAT)

You need to create a GitHub PAT with appropriate permissions:

#### Step 1: Create Token on GitHub

1. Go to: https://github.com/settings/tokens
2. Click "Generate new token" → "Generate new token (classic)"
3. Set token name: `WileyWidget-Dev`
4. Set expiration: 90 days (or as needed)

#### Step 2: Select Scopes

✅ Select these scopes for full functionality:

- **repo** (full control of private repositories)
  - repo:status
  - repo_deployment
  - public_repo
  - repo:invite
- **workflow** (update GitHub Action workflows)
- **admin:repo_hook** (manage repository hooks)
- **admin:org_hook** (manage organization hooks)
- **gist** (access to gists)
- **delete_repo** (delete repositories if needed)

Or for minimal/safe access, use:

- **repo:status** (read-only access)
- **public_repo** (public repository access)
- **gist** (gist access)

#### Step 3: Copy Token

- Click "Generate token"
- **COPY THE TOKEN IMMEDIATELY** (you won't see it again)

### 2. Set Environment Variable

#### Option A: System Environment Variable (Windows)

```powershell
# Run PowerShell as Administrator
[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "your_token_here", "User")

# Restart VS Code after setting
```

#### Option B: User Environment Variable (.env file)

Create a `.env` file in the workspace root:

```
GITHUB_TOKEN=your_token_here
```

Then in `.vscode/settings.json`, update to:

```json
"env": {
  "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_TOKEN}"
}
```

#### Option C: VS Code User Settings

1. Open **File → Preferences → Settings**
2. Search for "terminal.integrated.env.windows"
3. Click "Edit in settings.json"
4. Add:

```json
"terminal.integrated.env.windows": {
  "GITHUB_TOKEN": "your_token_here"
}
```

**⚠️ WARNING:** Do not commit tokens to Git. Add `.env` to `.gitignore`:

```
# .gitignore
.env
*.env
.env.local
.env.*.local
```

## 🚀 Activation & Verification

### Step 1: Restart VS Code

After setting the environment variable, **completely close and reopen VS Code**.

### Step 2: Test MCP Server

```powershell
# In VS Code terminal, verify token is accessible
$env:GITHUB_TOKEN
# Should output: (your token value if set correctly)
```

### Step 3: Use in Copilot Chat

In the Copilot Chat panel, you can now use GitHub operations:

```
@github list-issues Bigessfour/Wiley-Widget
@github list-pull-requests Bigessfour/Wiley-Widget
@github get-pull-request Bigessfour/Wiley-Widget 1
```

### Step 4: Check MCP Server Status

In VS Code:

1. Open Command Palette: **Ctrl+Shift+P**
2. Search: "MCP Servers"
3. You should see "github" listed as ACTIVE

## 📡 Available GitHub Operations

Once configured, you can use these operations:

### Repository Operations

```
@github list-repositories
@github get-repository Bigessfour/Wiley-Widget
@github list-issues Bigessfour/Wiley-Widget
@github list-pull-requests Bigessfour/Wiley-Widget
```

### Pull Request Operations

```
@github get-pull-request Bigessfour/Wiley-Widget <PR_NUMBER>
@github list-review-requests Bigessfour/Wiley-Widget <PR_NUMBER>
@github create-pull-request Bigessfour/Wiley-Widget --base main --head fix/branch
@github update-pull-request Bigessfour/Wiley-Widget <PR_NUMBER> --state closed
```

### Issue Operations

```
@github list-issues Bigessfour/Wiley-Widget --state open
@github create-issue Bigessfour/Wiley-Widget --title "Bug" --body "Description"
@github update-issue Bigessfour/Wiley-Widget <ISSUE_NUMBER> --state closed
```

### Review Operations

```
@github list-reviews Bigessfour/Wiley-Widget <PR_NUMBER>
@github create-review Bigessfour/Wiley-Widget <PR_NUMBER>
```

## 🔐 Security Best Practices

1. **Never commit tokens to Git**

   ```bash
   git check-ignore .env  # Should output .env
   ```

2. **Use minimal scope tokens**
   - Don't use repo:full if you only need public_repo access
   - Regularly rotate tokens (every 90 days recommended)

3. **Monitor token usage**
   - Check GitHub → Settings → Developer settings → Personal access tokens
   - Review "Last used" date regularly

4. **Revoke tokens if compromised**
   - Go to https://github.com/settings/tokens
   - Click "Delete" next to compromised token

## 🐛 Troubleshooting

### Token Not Recognized

```powershell
# Check if environment variable is set
Get-ChildItem env:GITHUB_TOKEN

# If not set, set it manually in current PowerShell session
$env:GITHUB_TOKEN = "your_token_here"

# Then reload VS Code
```

### MCP Server Not Starting

1. Check VS Code output: **View → Output → Copilot**
2. Look for error messages about GitHub MCP
3. Verify token is valid: https://github.com/settings/tokens
4. Restart VS Code completely

### Permission Denied Errors

- Token scope is insufficient
- Go to https://github.com/settings/tokens
- Regenerate token with required scopes
- Update GITHUB_TOKEN environment variable

### Command Not Found

If you get "npx: command not found":

1. Install Node.js from https://nodejs.org/ (LTS recommended)
2. Restart VS Code
3. Try again

## ✅ Verification Checklist

- [ ] GitHub PAT created at https://github.com/settings/tokens
- [ ] Token copied to secure location
- [ ] GITHUB_TOKEN environment variable set (Windows)
- [ ] VS Code restarted after setting token
- [ ] MCP server shows as ACTIVE in VS Code
- [ ] `.env` file added to `.gitignore`
- [ ] Token has required scopes (at minimum: repo, gist)
- [ ] Tested with simple GitHub command in Copilot Chat

## 📖 Usage Examples

### Check PR Status

In Copilot Chat:

```
@github get-pull-request Bigessfour/Wiley-Widget 1

Can you check the status of our QuickBooks PR?
```

### List All PRs

```
@github list-pull-requests Bigessfour/Wiley-Widget

Show me all open pull requests for the WileyWidget project
```

### Create Issue

```
@github create-issue Bigessfour/Wiley-Widget --title "Feature Request" --body "Add dashboard widget"

Help me create an issue for the new dashboard feature
```

## 🎯 Integration with Your Workflow

Now you can:

1. **View PR status** directly in Copilot Chat
2. **Check CI/CD results** without leaving IDE
3. **Manage issues and PRs** from development environment
4. **Automate GitHub operations** via chat commands
5. **Get context** about the repository while coding

## Support & Documentation

- **GitHub MCP Docs:** https://modelcontextprotocol.io/docs/server-implementations/github
- **VS Code MCP Support:** https://code.visualstudio.com/docs/editor/github-copilot
- **GitHub API Docs:** https://docs.github.com/en/rest

---

**Status:** ✅ GitHub MCP Server Configured and Ready to Use

**Next Step:** Set the GITHUB_TOKEN environment variable and restart VS Code
