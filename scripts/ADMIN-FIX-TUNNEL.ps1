# ============================================
# COPY AND PASTE THIS INTO ADMINISTRATOR POWERSHELL
# ============================================

# Kill hanging process
Get-Process cloudflared -ErrorAction SilentlyContinue | Stop-Process -Force

# Delete broken service
sc.exe delete Cloudflared

# Reinstall with config
& "C:\Program Files (x86)\cloudflared\cloudflared.exe" service install --config "C:\ProgramData\cloudflared\config.yml"

# Set to automatic and start
sc.exe config Cloudflared start= auto
sc.exe start Cloudflared

# Wait and verify
Start-Sleep -Seconds 10
& "C:\Program Files (x86)\cloudflared\cloudflared.exe" tunnel info ddd24f98-673d-43cb-b8a8-21a2329fffec

# Test public endpoint
Invoke-WebRequest -Uri "https://app.townofwiley.gov/health" -UseBasicParsing | Select-Object StatusCode, Content
