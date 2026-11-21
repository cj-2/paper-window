# Integração com o Windows

Tudo que fala com o sistema operacional fica concentrado em poucos pontos. Este documento lista cada recurso do Windows usado, onde e por quê.

## P/Invoke — `Native.cs`

Única fonte das declarações `DllImport` e constantes Win32. Ao precisar de uma API nova, declare-a aqui.

| DLL | APIs | Usada em |
|---|---|---|
| `user32` | `UpdateLayeredWindow` (2 sobrecargas), `SetWindowPos`, `SetWindowDisplayAffinity` | `LayeredWindow` |
| `user32` | `RegisterHotKey`, `UnregisterHotKey`, `GetKeyState` | `HotkeyManager`, `Hotkey.WinKeyDown` |
| `user32` | `GetAsyncKeyState` | `Native.PrimaryButtonDown` → `InkLayer` |
| `user32` | `SetSystemCursor`, `SystemParametersInfo`, `GetSystemMetrics`, `CreateIconIndirect`, `GetIconInfo`, `DestroyIcon` | `CursorTheme` |
| `user32` / `gdi32` | `GetDC`, `ReleaseDC`, `CreateCompatibleDC`, `SelectObject`, `DeleteObject`, `DeleteDC` | `LayeredWindow.Present` |
| `magnification` | `MagInitialize`, `MagUninitialize`, `MagSetFullscreenColorEffect` | `PaperContext` |
| `user32` | `SendMessageTimeout` (declarada em `WindowsTheme.cs`) | `WindowsTheme.Set` |

`MAGCOLOREFFECT` carrega a matriz 5×5 (25 floats, linha por linha).

## Instância única

`Program.Main` cria um `Mutex` `Local\PaperWindow.SingleInstance`. Se já existir, a segunda execução sai sem fazer nada.

## Atalho global — `Hotkey.cs`

- **`Hotkey`** é um `record struct` (tecla + Ctrl/Alt/Shift/Win). Converte de/para texto (`"Ctrl+Alt+Win+P"`) com `TryParse`/`ToString`. Texto vazio é "sem atalho" (sucesso com `null`).
- **Validade (`IsValid`):** precisa de Ctrl, Alt ou Win, ou ser uma tecla F1–F24 sozinha (Shift sozinho roubaria a digitação normal); a tecla não pode ser um modificador.
- **`HotkeyManager`** herda de `NativeWindow` e cria uma **janela só de mensagens** (`HWND_MESSAGE`) para receber `WM_HOTKEY`. Registra com `MOD_NOREPEAT` (segurar a tecla não repete).
  - `Set(hotkey)` troca o atalho; `Failed` fica `true` se o registro falhar (normalmente outro programa já usa a combinação). `PaperContext` mostra um balão na bandeja ao iniciar; a janela de configurações mostra o aviso em vermelho.
  - `Suspend(bool)` libera temporariamente o atalho enquanto o usuário grava um novo.

## Cursores do sistema

`SetSystemCursor` troca o cursor até o logoff; `SPI_SETCURSORS` recarrega o esquema do usuário. Detalhes e cuidados em [mouse.md](mouse.md).

## Excluir da captura de tela

`SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` mantém a janela visível para o usuário, mas fora de prints, gravações e compartilhamento. Exige Windows 10 versão 2004 (build 19041). Em builds anteriores o mesmo valor se comportaria como `WDA_MONITOR` (caixa preta), então `LayeredWindow.SetCaptureExcluded` não faz nada nelas.

O **filtro de cor da Magnification** não é uma janela: aparecer ou não em capturas depende da ferramenta usada, e não há controle sobre isso.

## Tema claro/escuro — `WindowsTheme.cs`

Lê e grava, em `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`:

- `AppsUseLightTheme` (apps) e `SystemUsesLightTheme` (sistema): 1 = claro, 0 = escuro.

`Set(dark)` não faz nada se já estiver no modo pedido. Depois de gravar, envia `WM_SETTINGCHANGE` com `"ImmersiveColorSet"` por broadcast (com `SMTO_ABORTIFHUNG` e 1 s de limite), a mesma notificação do app Configurações, para Explorer, barra de tarefas e apps abertos se repintarem na hora. `PaperContext.SetWindowDark` engole erros de permissão/E/S, de modo que falhar aqui nunca derruba o app.

## Iniciar com o Windows — `Startup` (em `Settings.cs`)

Cria/remove o valor `PaperWindow` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, apontando para `Environment.ProcessPath` (entre aspas). Por usuário, sem precisar de administrador.

## Eventos do sistema (`SystemEvents`)

| Evento | Reação |
|---|---|
| `DisplaySettingsChanged` | Recria os overlays (monitores mudaram) e reaplica |
| `TimeChanged` | Reconfere a agenda dia/noite |
| `PowerModeChanged` (Resume) | Reconfere a agenda ao voltar da suspensão |

Os handlers são removidos em `ExitThreadCore`.

## Publicação

```bash
dotnet publish -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -o publish
```

Gera `publish\PaperWindow.exe` (depende do .NET 8 Desktop Runtime). A pasta `publish/`, `bin/` e `obj/` ficam fora do git.
