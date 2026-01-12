# GitHub MCP Quick Start (5 Minutes)

## ✅ DONE: Configuration

GitHub MCP server is now configured in `.vscode/settings.json`

## 🔑 NEXT: Get Your Token (3 minutes)

### Quick Steps:

1. Go to: https://github.com/settings/tokens
2. Click: "Generate new token (classic)"
3. Name: `WileyWidget-Dev`
4. Expiration: 90 days
5. **Check boxes:**
   - ✅ repo (entire section)
   - ✅ workflow
   - ✅ admin:repo_hook
   - ✅ gist

6. Click: "Generate token"
7. **COPY** the token (you won't see it again!)

## 🔐 NEXT: Set Environment Variable (2 minutes)

### Windows PowerShell (Recommended):

```powershell
# Run as Administrator
[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "paste_your_token_here", "User")
```

### Or Create `.env` File:

1. In VS Code, create file: `.env`
2. Add one line:

```
GITHUB_TOKEN=paste_your_token_here
```

3. Add to `.gitignore`:

```
.env
```

## ✨ FINAL: Restart & Test

1. **Close VS Code completely**
2. **Reopen VS Code**
3. **Open Copilot Chat** (Ctrl+Shift+I)
4. **Type this command:**

```
@github list-repositories
```

If you see your repositories, you're all set! ✅

## 🎯 Common Commands

Once working, use these in Copilot Chat:

```
@github list-pull-requests Bigessfour/Wiley-Widget
@github get-pull-request Bigessfour/Wiley-Widget 1
@github list-issues Bigessfour/Wiley-Widget
```

## ❌ Not Working?

**Most common issue:** Environment variable not set or VS Code not restarted

**Fix:**

1. Verify token: `$env:GITHUB_TOKEN` (in PowerShell)
2. Should show your token (blurred in output)
3. If blank, set it again with the command above
4. Close VS Code completely
5. Reopen

## 📚 Full Details

See: `GITHUB_MCP_SETUP.md` for complete documentation

---

**Status:** ⏳ Awaiting Token Setup

**Time to Complete:** 5 minutes total
