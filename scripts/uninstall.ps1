<#
.SYNOPSIS
    Script de desinstalação limpa do Batto (Bluetooth Battery Tray)
#>
param(
    [switch]$KeepSettings
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host ">>> Desinstalando Batto..." -ForegroundColor Cyan

# 1. Encerrar processo
Get-Process -Name "Batto" | Stop-Process -Force

# 2. Remover chave de inicialização automática do Windows
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "BattoBluetoothBatteryTray" -ErrorAction SilentlyContinue

# 3. Remover atalho do Menu Iniciar
$startMenuShortcut = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Batto.lnk"
if (Test-Path $startMenuShortcut) {
    Remove-Item -Path $startMenuShortcut -Force
}

# 4. Remover registro de desinstalação do Windows
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Batto" -Recurse -Force -ErrorAction SilentlyContinue

# 5. Remover configurações de usuário se solicitado
if (-not $KeepSettings) {
    $settingsDir = "$env:APPDATA\Batto"
    if (Test-Path $settingsDir) {
        Remove-Item -Path $settingsDir -Recurse -Force
        Write-Host "[✓] Configurações de usuário removidas." -ForegroundColor Green
    }
}

# 6. Remover arquivos instalados
$installDir = "$env:LOCALAPPDATA\Programs\Batto"
if (Test-Path $installDir) {
    # Agendar remoção da própria pasta se chamada pelo uninstall.ps1 de dentro dela
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c timeout /t 2 /nobreak & rmdir /s /q `"$installDir`"" -WindowStyle Hidden
}

Write-Host ">>> Batto desinstalado com sucesso!" -ForegroundColor Green
