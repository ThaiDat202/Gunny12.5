$ErrorActionPreference = 'Stop'

$app = 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\app'
$src = 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\src\GunnyPanel\bin\Release\net10.0\GunnyPanel.dll'
$bak = Join-Path $app 'GunnyPanel.dll.bak-20261124'
$log = 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\_tools\deploy-titles-fix-20261124.log'

function Write-Log([string]$message) {
    $line = "{0} {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $message
    $line | Tee-Object -FilePath $log -Append
}

$stopped = $false
try {
    Write-Log 'Stopping GunnyPanel...'
    taskkill /F /IM GunnyPanel.exe /T 2>&1 | ForEach-Object { Write-Log $_ }
    Start-Sleep -Seconds 2
    $stopped = $true

    Write-Log 'Backing up current GunnyPanel.dll...'
    if (Test-Path $bak) { Copy-Item $bak "$bak.prev" -Force -ErrorAction SilentlyContinue }
    Copy-Item (Join-Path $app 'GunnyPanel.dll') $bak -Force

    Write-Log 'Copying new GunnyPanel.dll...'
    Copy-Item $src (Join-Path $app 'GunnyPanel.dll') -Force

    Write-Log 'Starting GunnyPanel...'
    Start-Process -FilePath (Join-Path $app 'GunnyPanel.exe') -WorkingDirectory $app
    Start-Sleep -Seconds 5

    $listen = Get-NetTCPConnection -LocalPort 90 -State Listen -ErrorAction SilentlyContinue
    if (-not $listen) {
        throw 'Port 90 is not listening after restart.'
    }
    Write-Log "Port 90 listening, PID=$($listen.OwningProcess)"

    try {
        $response = Invoke-WebRequest 'http://127.0.0.1:90/' -UseBasicParsing -TimeoutSec 10
        Write-Log "HTTP $($response.StatusCode)"
    }
    catch {
        Write-Log "HTTP check failed: $($_.Exception.Message)"
    }

    Write-Log 'DONE'
    exit 0
}
catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    if ($stopped) {
        try {
            if (Test-Path $bak) {
                Write-Log 'Restoring backup DLL and restarting old panel...'
                Copy-Item $bak (Join-Path $app 'GunnyPanel.dll') -Force
            }
            Start-Process -FilePath (Join-Path $app 'GunnyPanel.exe') -WorkingDirectory $app
            Start-Sleep -Seconds 3
            Write-Log 'Old panel restart attempted.'
        }
        catch {
            Write-Log "Restart after failure also failed: $($_.Exception.Message)"
        }
    }
    exit 1
}
