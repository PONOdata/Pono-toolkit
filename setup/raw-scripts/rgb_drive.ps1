# Drive the keyboard RGB via Windows.Devices.Lights.LampArray (the WinRT Dynamic Lighting API).
# Expects Dynamic Lighting to be enabled and the keyboard to appear as a LampArray device.
# Flashes: red (2s) -> green (2s) -> blue (2s) -> amber steady.

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Runtime.WindowsRuntime -ErrorAction SilentlyContinue

# Helper: await a WinRT IAsyncOperation<T> from PowerShell 5.1 synchronously.
function Wait-AsyncOperation {
    param([Parameter(Mandatory)] $AsyncOp, [Parameter(Mandatory)] [type] $ResultType)
    $asTaskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq 'AsTask' -and
            $_.GetParameters().Count -eq 1 -and
            $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
        } | Select-Object -First 1
    $generic = $asTaskMethod.MakeGenericMethod($ResultType)
    $task = $generic.Invoke($null, @($AsyncOp))
    $task.Wait(-1) | Out-Null
    return $task.Result
}

function New-LampColor {
    param([byte]$R, [byte]$G, [byte]$B)
    [Windows.UI.Color, Windows, ContentType=WindowsRuntime]::FromArgb(255, $R, $G, $B)
}

try {
    $null = [Windows.Devices.Lights.LampArray, Windows, ContentType=WindowsRuntime]
    $null = [Windows.Devices.Enumeration.DeviceInformation, Windows, ContentType=WindowsRuntime]
    $null = [Windows.UI.Color, Windows, ContentType=WindowsRuntime]
    Write-Host "WinRT types loaded."
} catch {
    Write-Host "FAILED to load WinRT types: $_"
    exit 2
}

Write-Host "=== ENUMERATING LAMPARRAY DEVICES ==="
$selector = [Windows.Devices.Lights.LampArray, Windows, ContentType=WindowsRuntime]::GetDeviceSelector()
Write-Host "  selector: $selector"

$findOp = [Windows.Devices.Enumeration.DeviceInformation, Windows, ContentType=WindowsRuntime]::FindAllAsync($selector)
$devices = Wait-AsyncOperation -AsyncOp $findOp -ResultType ([Windows.Devices.Enumeration.DeviceInformationCollection, Windows, ContentType=WindowsRuntime])

if (-not $devices -or $devices.Count -eq 0) {
    Write-Host "  NO LAMPARRAY DEVICES FOUND."
    Write-Host "  This means Windows/LampArray does not see the keyboard as a controllable RGB device."
    exit 3
}

Write-Host "  Found $($devices.Count) device(s):"
foreach ($d in $devices) {
    Write-Host "    - $($d.Name)  id=$($d.Id)"
}

Write-Host ""
Write-Host "=== OPENING FIRST DEVICE ==="
$first = $devices[0]
Write-Host "  opening: $($first.Name)"

$openOp = [Windows.Devices.Lights.LampArray, Windows, ContentType=WindowsRuntime]::FromIdAsync($first.Id)
$lampArray = Wait-AsyncOperation -AsyncOp $openOp -ResultType ([Windows.Devices.Lights.LampArray, Windows, ContentType=WindowsRuntime])

Write-Host ("  LampCount: {0}" -f $lampArray.LampCount)
Write-Host ("  LampArrayKind: {0}" -f $lampArray.LampArrayKind)
Write-Host ("  IsAvailable: {0}" -f $lampArray.IsAvailable)
Write-Host ("  MinUpdateInterval (ms): {0}" -f $lampArray.MinUpdateInterval.TotalMilliseconds)

if (-not $lampArray.IsAvailable) {
    Write-Host "  Device reports IsAvailable=false. Another app likely has ownership."
}

Write-Host ""
Write-Host "=== REQUESTING MESSAGE CHANNEL (FG priority) ==="
try {
    $lampArray.RequestMessageChannel() | Out-Null
    Write-Host "  ok"
} catch {
    Write-Host "  RequestMessageChannel threw: $_"
}

function Show-LampColor {
    param([string]$label, [byte]$r, [byte]$g, [byte]$b, [int]$holdMs)
    Write-Host "  -> $label (r=$r g=$g b=$b)"
    $c = New-LampColor -R $r -G $g -B $b
    $lampArray.SetColor($c)
    Start-Sleep -Milliseconds $holdMs
}

Write-Host ""
Write-Host "=== DRIVING COLORS ==="
Show-LampColor -label "RED"    -r 255 -g 0   -b 0   -holdMs 2000
Show-LampColor -label "GREEN"  -r 0   -g 255 -b 0   -holdMs 2000
Show-LampColor -label "BLUE"   -r 0   -g 0   -b 255 -holdMs 2000
Show-LampColor -label "AMBER"  -r 255 -g 150 -b 0   -holdMs 500

Write-Host ""
Write-Host "=== DONE. Keyboard should now be steady AMBER. ==="
Write-Host "If it cycled red/green/blue and is now amber, Windows RGB control is working."
Write-Host "If nothing happened, the keyboard is not enumerated as a LampArray device."
