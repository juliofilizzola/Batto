$proc = Start-Process -FilePath "$PSScriptRoot\..\publish\Batto.exe" -PassThru
Start-Sleep -Seconds 3
if ($proc.HasExited) {
    Write-Host "Process exited with code: $($proc.ExitCode)" -ForegroundColor Yellow
} else {
    Write-Host "Process is RUNNING with PID: $($proc.Id)" -ForegroundColor Green
}
