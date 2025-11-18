# Using xAI Grok API with Continue.dev

## Quick Setup

### Option 1: Automated Setup (Recommended)

Run the configuration script:

```powershell
.\scripts\configure-continue-xai.ps1
```

This will:

1. Prompt for your xAI API key securely
2. Validate the key with a test request
3. Create the Continue.dev configuration
4. Set up Grok-4-0709, Grok-2, and Grok-beta models

### Option 2: Manual Setup

1. **Get your xAI API key** from [https://console.x.ai/](https://console.x.ai/)

2. **Open Continue.dev config**:
   - Press `Ctrl+Shift+P` in VS Code
   - Search: "Continue: Open Config"
   - Or edit directly: `%USERPROFILE%\.continue\config.json`

3. **Replace the config** with:

```json
{
  "models": [
    {
      "title": "Grok-4-0709 (Latest)",
      "provider": "openai",
      "model": "grok-4-0709",
      "apiBase": "https://api.x.ai/v1",
      "apiKey": "xai-YOUR_KEY_HERE"
    },
    {
      "title": "Grok-2 (Stable)",
      "provider": "openai",
      "model": "grok-2-latest",
      "apiBase": "https://api.x.ai/v1",
      "apiKey": "xai-YOUR_KEY_HERE"
    },
    {
      "title": "Grok-beta (Fast)",
      "provider": "openai",
      "model": "grok-beta",
      "apiBase": "https://api.x.ai/v1",
      "apiKey": "xai-YOUR_KEY_HERE"
    }
  ],
  "tabAutocompleteModel": {
    "title": "Grok-beta",
    "provider": "openai",
    "model": "grok-beta",
    "apiBase": "https://api.x.ai/v1",
    "apiKey": "xai-YOUR_KEY_HERE"
  }
}
```

4. **Replace** `xai-YOUR_KEY_HERE` with your actual API key

5. **Restart VS Code**

---

## Usage

### Generate E2E Tests with Grok

**Press `Ctrl+L`** to open Continue.dev chat, then use these prompts:

#### Example 1: Generate Municipal Account View Test

```text
Generate a C# xUnit E2E test using FlaUI for WileyWidget WPF app:
- Test class: MunicipalAccountViewE2ETests
- Inherit from WpfTestBase
- Test method: ValidateConservationTrustFund_Displays31Accounts
- Use SyncfusionHelpers.GetDataGridRowCount()
- Assert rowCount == 31
- Add [StaFact] attribute
```

#### Example 2: Generate Syncfusion Grid Interaction

```text
Generate helper method for FlaUI to:
- Find Syncfusion SfDataGrid by AutomationId
- Apply filter to Type column
- Get filtered row count
- Return List<string> of Type column values
```

#### Example 3: Generate Test Data Validator

```text
Create C# method to validate Municipal Account data:
- Input: List<MunicipalAccount>
- Verify: 31 total items
- Verify: 5 items with Type="Bank"
- Verify: All AccountNumbers are non-null
- Use FluentAssertions
```

---

## Model Comparison

| Model              | Speed     | Best For                           | Context     | Recommended Use                   |
| ------------------ | --------- | ---------------------------------- | ----------- | --------------------------------- |
| **Grok-4-0709** ⭐ | Fast      | Complex E2E tests, enterprise code | 200K tokens | Primary model for test generation |
| **Grok-2**         | Medium    | Stable fallback, full tests        | 128K tokens | General purpose                   |
| **Grok-beta**      | Very Fast | Quick edits, autocomplete          | 32K tokens  | Autocomplete, small changes       |

**Recommendation**: Use **Grok-4-0709** for E2E test generation (best quality, latest model).

---

## Advantages Over Local Ollama

✅ **Faster response times** (cloud-hosted, optimized inference)
✅ **Better C# understanding** (trained on GitHub + enterprise code)
✅ **No local resource usage** (no GPU/CPU overhead)
✅ **Latest model updates** (Grok-4-0709 July 2024 release)
✅ **Massive context window** (200K tokens for Grok-4)
✅ **Superior code quality** (better reasoning and structure)

---

## Cost Considerations

xAI API pricing (as of Nov 2025):

- **Grok-4-0709**: Premium tier (contact xAI for pricing)
- **Grok-2**: $2 per 1M tokens (input), $10 per 1M tokens (output)
- **Grok-beta**: $5 per 1M tokens (input), $15 per 1M tokens (output)

**Typical E2E test generation**:

- Grok-4-0709: ~2,500 tokens = $0.025-0.06 per test (best quality)
- Grok-beta: ~2,000 tokens = $0.02-0.05 per test (fast iterations)

**Budget-friendly tip**: Use Grok-beta for autocomplete/quick edits, Grok-4-0709 for full test generation.

---

## Security Best Practices

🔐 **Keep your API key secure:**

1. **Never commit** `config.json` to git:

   ```bash
   echo "%USERPROFILE%\.continue\config.json" >> .gitignore
   ```

2. **Use environment variables** (optional):

   ```json
   {
     "apiKey": "${XAI_API_KEY}"
   }
   ```

   Set in PowerShell:

   ```powershell
   $env:XAI_API_KEY = "xai-your-key"
   ```

3. **Rotate keys regularly** at [console.x.ai](https://console.x.ai/)

---

## Troubleshooting

### Error: "Invalid API key"

- Verify key format: `xai-XXXXXXXXXXXX`
- Check key is active at [console.x.ai](https://console.x.ai/)
- Ensure no extra spaces in config.json

### Error: "Rate limit exceeded"

- Wait 60 seconds between requests
- Upgrade to higher tier at console.x.ai

### Continue.dev not responding

- Restart VS Code
- Check Continue.dev logs: `Ctrl+Shift+P` → "Continue: Open Logs"
- Verify internet connection

---

## Next Steps

1. ✅ Run setup script:

```powershell
.\scripts\configure-continue-xai.ps1
```

2. ✅ Enter your xAI API key when prompted
3. ✅ Restart VS Code
4. ✅ Press `Ctrl+L` and select **"Grok-4-0709 (Latest)"**
5. ✅ Try this prompt:

   ```text
   Generate a C# xUnit E2E test using FlaUI for MunicipalAccountView
   that validates 31 Conservation Trust Fund accounts in SfDataGrid
   ```

---

## Why Grok-4-0709?

🚀 **Latest xAI Model** (July 2024 release)
🧠 **200K Token Context** (can understand your entire codebase)
⚡ **Fast Inference** (cloud-optimized)
🎯 **Best for Enterprise C#** (excellent WPF/Syncfusion understanding)
✨ **Superior Code Quality** (better reasoning than Grok-2)

---

**Documentation**: [docs/AI_E2E_TESTING_SETUP.md](./AI_E2E_TESTING_SETUP.md)
**xAI Console**: [https://console.x.ai/](https://console.x.ai/)
**Continue.dev Docs**: [https://docs.continue.dev/](https://docs.continue.dev/)
