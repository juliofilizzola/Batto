<#
.SYNOPSIS
    Script de instalação automática do Batto (Bluetooth Battery Tray)
#>
param(
    [switch]$StartAfterInstall,
    [switch]$EnableAutoStart
)

$ErrorActionPreference = "Stop"

$installDir = "$env:LOCALAPPDATA\Programs\Batto"
$sourceExe = "$PSScriptRoot\..\publish\Batto.exe"

if (-not (Test-Path $sourceExe)) {
    Write-Error "O arquivo Batto.exe não foi encontrado em $sourceExe. Execute dotnet publish primeiro."
    exit 1
}

Write-Host ">>> Instalando Batto - Bluetooth Battery Tray..." -ForegroundColor Cyan

# 1. Encerrar instâncias anteriores se houver
Get-Process -Name "Batto" -ErrorAction SilentlyContinue | Stop-Process -Force

# 2. Criar diretório de destino
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# 3. Copiar binário
Copy-Item -Path $sourceExe -Destination "$installDir\Batto.exe" -Force
Write-Host "[✓] Executável instalado em: $installDir\Batto.exe" -ForegroundColor Green

# 4. Criar atalho no Menu Iniciar
$startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$wscript = New-Object -ComObject WScript.Shell
$shortcut = $wscript.CreateShortcut("$startMenuDir\Batto.lnk")
$shortcut.TargetPath = "$installDir\Batto.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "Batto - Monitor de Bateria Bluetooth na Barra de Tarefas"
$shortcut.Save()
Write-Host "[✓] Atalho criado no Menu Iniciar." -ForegroundColor Green

# 5. Registrar no Painel de Aplicativos Instalados do Windows (Desinstalação Limpa)
$uninstallRegKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Batto"
if (-not (Test-Path $uninstallRegKey)) {
    New-Item -Path $uninstallRegKey -Force | Out-Null
}
Set-ItemProperty -Path $uninstallRegKey -Name "DisplayName" -Value "Batto - Bluetooth Battery Tray"
Set-ItemProperty -Path $uninstallRegKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $uninstallRegKey -Name "Publisher" -Value "Batto Open Source"
Set-ItemProperty -Path $uninstallRegKey -Name "DisplayIcon" -Value "$installDir\Batto.exe,0"
Set-ItemProperty -Path $uninstallRegKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $uninstallRegKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""

# Copiar script de desinstalação para a pasta do programa
Copy-Item -Path "$PSScriptRoot\uninstall.ps1" -Destination "$installDir\uninstall.ps1" -Force
Write-Host "[✓] Registrado em 'Aplicativos Instalados' do Windows." -ForegroundColor Green

# 6. Configurar inicialização automática se solicitado
if ($EnableAutoStart) {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "BattoBluetoothBatteryTray" -Value "`"$installDir\Batto.exe`" --minimized"
    Write-Host "[✓] Inicialização automática com o Windows ativada." -ForegroundColor Green
}

# 7. Iniciar aplicativo se solicitado
if ($StartAfterInstall) {
    Write-Host ">>> Iniciando Batto..." -ForegroundColor Cyan
    Start-Process -FilePath "$installDir\Batto.exe"
}

Write-Host ">>> Instalação concluída com sucesso!" -ForegroundColor Green
