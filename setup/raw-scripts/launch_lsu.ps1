# Launch Lenovo System Update for driver refresh on this specific machine.
$paths = @(
  'C:\Program Files (x86)\Lenovo\System Update\Tvsu.exe',
  'C:\Program Files\Lenovo\System Update\Tvsu.exe'
)
$found = $null
foreach ($p in $paths) {
  if (Test-Path $p) { $found = $p; break }
}
if ($found) {
  Write-Host "Launching $found"
  Start-Process $found
} else {
  Write-Host "Tvsu.exe not in expected paths; searching..."
  Get-ChildItem -Path 'C:\Program Files','C:\Program Files (x86)' -Recurse -Filter 'Tvsu*.exe' -ErrorAction SilentlyContinue |
    Select-Object -First 5 FullName
}
