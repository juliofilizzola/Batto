# Batto para Linux: guia de port

Este documento descreve como portar o **Batto** (monitor de bateria Bluetooth na bandeja) do Windows para Linux. Foi escrito para ser lido por quem vai implementar o port, humano ou agente, sem acesso ao histórico da conversa.

---

## 1. Contexto

O Batto atual é um app **WPF / .NET 10** para Windows 10 e 11. Ele mostra, na área de notificação, um anel dividido em segmentos (um por dispositivo) com o nível de bateria de cada periférico Bluetooth. Ao passar o mouse, um cartão lista os dispositivos com nome, status e percentual.

Repositório: `github.com/juliofilizzola/Batto`
Stack atual: .NET 10, WPF, `Shell_NotifyIcon`, GDI+, `Windows.Devices.*` (WinRT), registro do Windows.

**Objetivo do port:** ter a mesma experiência no Linux: ícone na bandeja com o anel de bateria, janela de gerenciamento de dispositivos, notificações e início automático.

---

## 2. O que pode ser reaproveitado

Estas partes não dependem de Windows e podem ser movidas quase sem mudança (ou com ajustes mínimos de namespace):

| Arquivo / pasta | Conteúdo | Observação |
|---|---|---|
| `src/Batto/Models/BatteryLevelRange.cs` | Faixas de bateria (alta, média, baixa, crítica) | Lógica pura |
| `src/Batto/Models/DeviceType.cs` | Tipos de dispositivo (fone, mouse, teclado...) | Lógica pura |
| `src/Batto/Models/BluetoothDeviceModel.cs` | Modelo de dispositivo | Remover dependências de WinRT, se houver |
| `src/Batto/Models/AppSettings.cs` | Configurações (intervalo, prioridade, `MaxTrayIcons`) | Lógica pura |
| `src/Batto/Storage/SettingsManager.cs` | Persistência JSON | Trocar `%APPDATA%\Batto` por `$XDG_CONFIG_HOME/batto` |
| `src/Batto/Battery/BatteryMonitor.cs` | Orquestração: ciclo de atualização, prioridade, tipo por dispositivo | Remover chamadas diretas ao WinRT; depender de uma interface de leitura |
| `src/Batto/ViewModels/MainViewModel.cs` | ViewModel da janela | Reaproveitável com a UI nova |
| `tests/Batto.Tests/` | Testes unitários | Os de lógica de faixas, ordenação e configurações valem direto |

**Status: feito.** A lógica compartilhada está em `src/Batto.Core/` (`net10.0`, sem WPF/WinRT), com as interfaces `IBluetoothDeviceSource`, `IBatteryReader`, `IAutostart` e `IUiDispatcher`. O app Windows implementa essas interfaces (`WindowsBatteryReader`, `BluetoothManager`, `WindowsAutostart`, `WpfUiDispatcher`) e faz a composição em `App.xaml.cs`. Um port Linux implementa as mesmas interfaces, sem tocar na Core.

---

## 3. O que é específico do Windows (precisa de substituto)

| Hoje (Windows) | Arquivo atual | Substituto no Linux |
|---|---|---|
| `Windows.Devices.Enumeration` (`DeviceWatcher`) | `Bluetooth/BluetoothManager.cs` | **BlueZ via D-Bus** (ObjectManager) |
| Propriedade PnP de bateria | `Bluetooth/PnpBatteryReader.cs` | `org.bluez.Battery1.Percentage` |
| GATT via WinRT (0x180F / 0x2A19) | `Bluetooth/GattBatteryReader.cs` | GATT via BlueZ D-Bus (`GattCharacteristic1`) |
| `Shell_NotifyIcon` + HWND oculto | `Tray/TrayManager.cs`, `Native/NativeMethods.cs` | **StatusNotifierItem (SNI)** via D-Bus |
| GDI+ (`Bitmap`, `DrawArc`, `GetHicon`) | `Tray/IconGenerator.cs` | SkiaSharp (ou SVG → PNG) para gerar o `IconPixmap` do SNI |
| Cartão de hover (`DeviceFlyout`) | `Tray/DeviceFlyout.xaml` | Janela aberta pelo clique + tooltip do SNI com resumo |
| Promoção de ícones Win11 | `Tray/TrayPromotion.cs` | **Não se aplica**: remover no Linux |
| Balloon / notificações | `Notifications/NotificationManager.cs` | `org.freedesktop.Notifications` via D-Bus |
| Início automático (chave `Run`) | `Storage/SettingsManager.cs` | Arquivo `.desktop` em `~/.config/autostart/` |
| WPF (`MainWindow.xaml`, estilos) | `MainWindow.xaml` | Avalonia (XAML parecido) ou GTK |
| Instalador Inno Setup | `distribution/Batto.iss` | AppImage, `.deb` ou Flatpak |
| Mutex de instância única | `App.xaml.cs` | Lock de arquivo em `$XDG_RUNTIME_DIR` ou `DBus` name único |

---

## 4. Leitura de bateria via BlueZ

BlueZ é a pilha Bluetooth do Linux. Ela expõe tudo pelo **D-Bus system bus**.

### 4.1 Enumerar dispositivos

- Serviço: `org.bluez`
- Chamar `org.freedesktop.DBus.ObjectManager.GetManagedObjects` em `/`
- Cada dispositivo aparece como um objeto com caminho do tipo `/org/bluez/hci0/dev_AA_BB_CC_DD_EE_FF`
- Interface do dispositivo: `org.bluez.Device1`
  - Propriedades úteis: `Address`, `Name`, `Alias`, `Icon`, `Paired`, `Connected`, `UUIDs`

### 4.2 Nível de bateria

- Interface: `org.bluez.Battery1` (presente no mesmo objeto do dispositivo quando o BlueZ detecta bateria)
- Propriedade: `Percentage` (tipo `y`, 0 a 100)
- Propriedade: `Source` (texto que indica a origem, quando disponível; útil para exibir "Fonte")
- Atualização: monitorar `org.freedesktop.DBus.Properties.PropertiesChanged` para essa interface

Observações:
- Teclados e mouses BLE tendem a expor `Battery1` pelo serviço de bateria GATT.
- Fones clássicos (HFP) podem expor bateria via indicadores do perfil Hands-Free. Depende do BlueZ e do driver. **Testar com os seus dispositivos reais.**

### 4.3 Fallback GATT

Se `Battery1` não existir para um dispositivo BLE:
- Percorrer `GattService1` com UUID `0000180f-0000-1000-8000-00805f9b34fb` (Battery Service)
- Dentro dele, `GattCharacteristic1` com UUID `00002a19-0000-1000-8000-00805f9b34fb` (Battery Level)
- Ler com `ReadValue` (`a{sv}` vazio) ou usar `StartNotify` para notificações

### 4.4 Ordem de prioridade (sugestão, espelhando o Windows)

1. `org.bluez.Battery1.Percentage` no próprio dispositivo
2. Bateria em objetos filhos do mesmo endereço (equivalente ao "service node" do Windows)
3. GATT Battery Service (0x180F / 0x2A19)

**Atenção:** no Windows, o AK820 mostrava um valor GATT desatualizado (94% fixo) e o valor correto vinha do nó BTHLE. No Linux, `Battery1` deve ser a fonte preferida. Validar com o AK820 antes de fechar essa ordem.

### 4.5 Bibliotecas

- **Tmds.DBus** (ou `Tmds.DBus.Protocol`): cliente D-Bus em .NET, sem dependência de libdbus nativa.
- Alternativa: chamar `dbus-send` ou `busctl` para o spike inicial (mais simples, mais lento).

---

## 5. Bandeja: StatusNotifierItem (SNI)

Linux não tem `Shell_NotifyIcon`. O padrão moderno é o **StatusNotifierItem**, exposto via D-Bus.

### 5.1 Como funciona

1. O app exporta um objeto `org.kde.StatusNotifierItem` em `/StatusNotifierItem`.
2. Registra-se no watcher `org.kde.StatusNotifierWatcher` chamando `RegisterStatusNotifierItem` com o nome do serviço D-Bus do app.
3. O painel (KDE Plasma, Waybar, etc.) lê as propriedades do item.

### 5.2 Propriedades e métodos a implementar

- `Id`, `Category` (`ApplicationStatus`), `Title`
- `Status` (`Active` ou `Passive`)
- `IconPixmap` (array de `(iiay)`: largura, altura, dados ARGB em big-endian) ou `IconName`
- `ToolTip` (`(sa(iiay)ss)`), para mostrar o resumo de bateria
- Método `Activate(x, y)` (clique esquerdo): abrir a janela principal
- Método `ContextMenu(x, y)` ou `com.canonical.dbusmenu` para o menu de contexto
- Sinais `NewIcon`, `NewToolTip`, `NewStatus` para atualizar a bandeja

### 5.3 Ícone

- Gerar o ícone em SkiaSharp (ou desenhar SVG e rasterizar) nos tamanhos 16, 22, 24, 32 e 48 px
- Replicar o anel segmentado: uma faixa por dispositivo, com a cor da faixa de bateria
- Atualizar `IconPixmap` quando algum valor mudar

### 5.4 Limitações conhecidas

- **Hover:** SNI não tem evento de passar o mouse. O equivalente é o **ToolTip**, que mostra o texto ao passar o mouse, e o clique abre a janela.
- **GNOME:** não exibe SNI por padrão. Precisa da extensão **AppIndicator and KStatusNotifierItem Support**. Documentar isso para o usuário.
- **KDE Plasma:** suporte nativo.
- **Sway / Hyprland / Waybar:** suportam SNI via `tray` module.

---

## 6. Interface

Opções:

1. **Avalonia UI** (recomendado): .NET, XAML parecido com WPF, roda em Linux, Windows e macOS. Permite reaproveitar o `MainViewModel` e boa parte dos estilos.
2. **GTK4 via GirCore** ou **GtkSharp**: nativo no GNOME, mas o XAML não é reaproveitado.

Requisitos de UI:
- Janela principal: contadores (dispositivos, conectados, na barra), lista de dispositivos com tipo, toggle "Na Barra", prioridade e percentual
- Aba de configurações: intervalo de atualização, notificações, início automático, `MaxTrayIcons`
- Tema escuro, igual à versão Windows

---

## 7. Notificações

- D-Bus: `org.freedesktop.Notifications`, método `Notify`
- Respeitar as mesmas regras do Windows: alertas em 20% e 10%, conexão e desconexão, com debouncing
- Ícone da notificação: usar o nome de ícone do tema (`battery-low`, `bluetooth-active`) ou o mesmo PNG do SNI

---

## 8. Início automático e instalação

- **Autostart:** criar `~/.config/autostart/batto.desktop` com `Exec=` apontando para o binário e `X-GNOME-Autostart-enabled=true`
- **Desinstalação:** remover o `.desktop` e o diretório de configuração (com opção de manter)
- **Empacotamento sugerido:**
  - **AppImage**: mais simples, funciona em qualquer distro
  - **`.deb`**: para Debian/Ubuntu (via `dpkg-deb` ou `fpm`)
  - **Flatpak**: opcional, mais trabalho de sandbox (o Bluetooth exige permissão de D-Bus do sistema)

**Permissões:** no Flatpak, o app precisa de acesso ao `org.bluez` e ao `org.kde.StatusNotifierWatcher` via `--talk-name`.

---

## 9. Plano de implementação em fases

### Fase 0: spike de leitura (1 sessão)
- Listar dispositivos via `busctl` ou `Tmds.DBus`
- Ler `Battery1.Percentage` dos seus dispositivos (X11mouse2, QQ2, AK820)
- Registrar o que aparece em `Battery1`, `Source` e GATT
- **Critério de saída:** tabela com dispositivo, interfaces disponíveis e valor lido

### Fase 1: núcleo compartilhado (1 a 2 sessões)
- Criar `Batto.Core` com modelos, faixas, configurações e `BatteryMonitor`
- Definir interfaces `IBatteryReader`, `ITrayHost`, `INotifier`, `IAutostart`
- Migrar testes unitários para o Core
- **Critério de saída:** testes passando no Windows com a lógica no Core

### Fase 2: leitor BlueZ (1 a 2 sessões)
- Implementar `BlueZBatteryReader : IBatteryReader`
- Enumerar, ler `Battery1`, fallback GATT, monitorar `PropertiesChanged`
- **Critério de saída:** valores corretos para os seus dispositivos, atualizados em tempo real

### Fase 3: bandeja SNI (1 a 2 sessões)
- Implementar `SniTrayHost : ITrayHost`
- Ícone em SkiaSharp, tooltip, clique para abrir a janela, menu de contexto
- **Critério de saída:** ícone visível no KDE e no GNOME com extensão

### Fase 4: UI e notificações (2 a 3 sessões)
- Janela Avalonia com o `MainViewModel` reaproveitado
- Notificações via `org.freedesktop.Notifications`
- Configurações e autostart
- **Critério de saída:** paridade funcional com a versão Windows, exceto promoção de ícones

### Fase 5: empacotamento (1 sessão)
- AppImage e `.deb` no pipeline de CI (GitHub Actions, runner `ubuntu-latest`)
- Release com tag `v*`, como já é feito para o Windows

---

## 10. Checklist de teste

Ambiente:
- [ ] Linux com adaptador Bluetooth funcional (não VM sem passthrough)
- [ ] BlueZ 5.56 ou superior (`bluetoothctl --version`)
- [ ] Testar em KDE Plasma e GNOME (com extensão)

Dispositivos (usar os mesmos do Windows):
- [ ] X11mouse2 (BLE, mouse)
- [ ] AK820 5.1-1 (BLE, teclado): valor deve bater com o teclado, não ficar fixo
- [ ] QQ2 (fone clássico): verificar se `Battery1` existe e se o valor muda ao conectar/desconectar

Comportamento:
- [ ] Ícone aparece na bandeja ao iniciar
- [ ] Clique abre a janela principal
- [ ] Tooltip mostra o resumo atualizado
- [ ] Desconectar um dispositivo atualiza a bandeja sem reiniciar o app
- [ ] Notificação em 20% e 10% sem spam
- [ ] Autostart funciona após reiniciar a sessão
- [ ] Configurações persistem em `~/.config/batto`

---

## 11. Perguntas em aberto

1. Qual distribuição e ambiente serão o alvo principal (Ubuntu/GNOME, Fedora/KDE, Arch)?
2. Vale suporte a Wayland e X11 para a janela, ou só Wayland?
3. Aceitar a dependência da extensão do GNOME, ou tentar outra solução para o ícone?
4. Versão mínima do BlueZ suportada?
5. Vai existir um repositório separado para o Linux ou o mesmo repositório com dois alvos?

---

## 12. Referências

- BlueZ D-Bus API: `org.bluez.Battery1`, `org.bluez.Device1`, `org.bluez.GattCharacteristic1` (documentação em `doc/` do código-fonte do BlueZ)
- StatusNotifierItem spec: freedesktop.org (`org.kde.StatusNotifierItem`)
- Desktop Notifications spec: freedesktop.org (`org.freedesktop.Notifications`)
- Autostart spec: XDG Desktop Entry, `~/.config/autostart`
- Tmds.DBus: `github.com/tmds/Tmds.DBus`
- Avalonia UI: `avaloniaui.net`
