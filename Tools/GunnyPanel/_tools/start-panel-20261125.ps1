$ErrorActionPreference = 'Stop'

$app = 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\app'
$log = 'C:\Gunny\Gunny12.5\Tools\GunnyPanel\_tools\start-panel-20261125.log'

function Write-Log([string]$message) {
    $line = "{0} {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $message
    $line | Tee-Object -FilePath $log -Append
}

try {
    $existing = Get-NetTCPConnection -LocalPort 90 -State Listen -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Log "Panel already listening on port 90, PID=$($existing.OwningProcess)"
        exit 0
    }

    Write-Log 'Starting GunnyPanel...'
    Start-Process -FilePath (Join-Path $app 'GunnyPanel.exe') -WorkingDirectory $app
    Start-Sleep -Seconds 5

    $listen = Get-NetTCPConnection -LocalPort 90 -State Listen -ErrorAction SilentlyContinue
    if (-not $listen) {
        throw 'Port 90 is not listening after start.'
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
    exit 1
}
