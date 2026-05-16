# Check for Ryzen AI installer landing in Downloads (full + partial).
$dl = "$env:USERPROFILE\Downloads"
Write-Host "=== DOWNLOADS DIR: $dl ==="
Get-ChildItem $dl -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'ryzen|RyzenAI|\.crdownload$|\.partial$|\.tmp$' } |
  Sort-Object LastWriteTime -Descending |
  Select-Object Name, Length, LastWriteTime |
  Format-Table -AutoSize

Write-Host "=== MOST RECENT 10 DOWNLOADS (any file) ==="
Get-ChildItem $dl -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 10 Name, Length, LastWriteTime |
  Format-Table -AutoSize
