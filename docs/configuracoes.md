# Configurações, perfis e janela de opções

## Modelo de dados — `Settings.cs`

### `Profile`: um visual

Guarda tudo que muda a aparência: `Effect`, `FilterStrength`, `BlueLightFilter`, `Brightness`, `Texture`, `TextureOpacity`, `CursorTheme`, `CursorStyle`, `CursorSize`, `InkTrail`, `InkTrailLength`, `InkTrailWidth`, `InkDrop`, mais o `Name`.

- `Clone(nome)` copia o perfil com outro nome.
- `CopyLookFrom(outro)` copia **tudo menos o nome**.
- `SameLook(outro)` compara só o visual (usado para detectar alterações e na importação).
- `Normalize()` corrige ids desconhecidos e valores fora da faixa.

> Ao criar uma opção nova de visual, ela precisa entrar em **quatro** lugares: a propriedade, `CopyLookFrom`, `SameLook` e `Normalize`. Esquecer um deles quebra o "salvar", a detecção de alterações ou a validação.

### `Settings`: tudo que é salvo

Opções **gerais** (valem para todos os perfis):

| Campo | Para quê |
|---|---|
| `Enabled` | Liga/desliga todo o efeito |
| `HideTextureFromCapture` / `HideMouseFromCapture` | Esconder textura / tinta em capturas |
| `ToggleHotkey` | Atalho global em texto (`""` = nenhum) |
| `ScheduleEnabled`, `DayProfile`, `NightProfile`, `DayStart`, `NightStart` | Agenda dia/noite |
| `ScheduleWindowsTheme` | Alterna claro/escuro do Windows pelos mesmos horários |
| `Profiles`, `ActiveProfile` | Lista de perfis e o ativo |

"Iniciar com o Windows" **não** está aqui: é uma configuração de cada PC, guardada em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pela classe `Startup`.

### Arquivo

`%APPDATA%\PaperWindow\settings.json`, JSON indentado e com escape relaxado (acentos legíveis para quem abre à mão).

- `Load()` nunca impede o app de abrir: arquivo ilegível ou inválido cai nos padrões.
- `Read(caminho)` lê tanto o arquivo do app quanto um exportado. Lança exceção se for inválido (a importação mostra o erro).
- **Migrações dentro de `Read`:**
  - `HideFromCapture` (opção única antiga) → copiado para `HideTextureFromCapture` e `HideMouseFromCapture`.
  - Arquivo sem `Profiles` (anterior aos perfis) → o visual na raiz vira o perfil "Padrão".
- `Normalize()` (privado) garante pelo menos um perfil, nomes únicos, perfil ativo existente, atalho válido, horários dentro de um dia e perfis de agenda que existam.

## Perfil mostrado × perfil salvo (o rascunho)

Três propriedades de `Settings` (marcadas `[JsonIgnore]`, não vão para o arquivo):

- `Current`: o perfil ativo, salvo.
- `Draft`: cópia editável do perfil ativo; `null` quando não há alterações pendentes.
- `Shown`: `Draft ?? Current`. **É o que a tela mostra.** `PaperContext.Apply()` sempre lê `Shown`.

Fluxo:

1. O usuário mexe num controle de visual → `EditDraft(...)` cria o rascunho (se preciso) e aplica a mudança → `onChange()` → `Apply()`: a prévia aparece na hora.
2. **Salvar** → `SaveDraft()` copia o rascunho para o perfil e descarta o rascunho.
3. Trocar de perfil, importar, exportar ou fechar com alterações pendentes → `ConfirmUnsaved()` pergunta Sim/Não/Cancelar. *Não* chama `DiscardDraft()`.
4. `HasUnsavedChanges` só é verdadeiro se o rascunho for **diferente** do salvo (`SameLook`); voltar o controle ao valor original some com o "pendente".

**Novo...** é um "salvar como": cria um perfil com `Shown` (o que está na tela) e descarta o rascunho do perfil original, que continua como estava salvo.

### Operações com perfis

- `UniqueName`: `"Noite"`, `"Noite (2)"`, `"Noite (3)"`… (sem diferenciar maiúsculas).
- `AddProfile`: adiciona uma cópia e a ativa.
- `RenameProfile`: recusa nome vazio ou repetido; atualiza `ActiveProfile`, `DayProfile` e `NightProfile` que apontavam para o nome antigo.
- `DeleteProfile`: nunca apaga o último; reaponta a agenda para o primeiro perfil se necessário.
- `ImportProfiles`: **só adiciona**. Perfil de mesmo nome e mesmo visual é ignorado; nome repetido ganha sufixo; o perfil ativo do arquivo vira o ativo. Opções gerais **não** mudam.

## Agenda dia/noite

`IsNight(hora)` decide o período. A noite pode passar da meia-noite (`19:00 → 07:00`); horários iguais significam "sempre dia". `ScheduledProfile(hora)` devolve o perfil do período, ou `null` com a agenda desligada.

`PaperContext.CheckSchedule()` roda:

- ao abrir, a cada 30 s (`_scheduleTimer`), ao mudar o relógio (`TimeChanged`), ao voltar da suspensão e depois de qualquer mudança na janela de configurações (postado, para não atualizar controles de dentro dos próprios eventos).

Regras (a chave `_scheduleKey = "período|perfil"` evita reaplicar):

- Só troca quando o período ou o perfil agendado **mudou**. Um perfil escolhido à mão vale até a próxima virada.
- Se há alterações não salvas, **espera** e tenta no próximo tique.
- Ao trocar: descarta rascunho, muda `ActiveProfile`, `SaveAndApply()`.

O tema do Windows (`CheckWindowsTheme`) é independente da troca de perfil: usa `_themeNight` para só agir quando o período muda, e chama `WindowsTheme.Set(dark)`.

## Janela de configurações — `SettingsForm.cs`

### Estrutura

- Topo: **Efeito ativo**, **Perfil** (combo) e botões Salvar / Novo / Renomear / Excluir.
- Abas: **Cor e brilho**, **Textura**, **Mouse** (editam o perfil) e **Dia e noite**, **Geral** (valem para todos).
- Rodapé: Importar, Exportar, Restaurar padrão, Fechar.
- Montada por helpers: `Grid`/`AddRow` (linhas de rótulo + controle + valor), `Page` (monta uma aba), `Combo`, `Slider`, `Hint`.
- O `TabControl` não se dimensiona sozinho: `OnLoad` o ajusta para caber na maior aba.

### Padrões importantes

- **`_loading`**: marcador de que o código está preenchendo os controles. Os eventos checam `_loading` e ignoram mudanças programáticas. Sem isso, preencher os controles dispararia `EditLook` e criaria rascunhos falsos.
- **`EditLook(...)`** = `Changed(() => _settings.EditDraft(...))`. Opções gerais usam `Changed(...)` direto (gravam no `Settings` sem rascunho).
- **`Changed`**: ignora se `_loading`, aplica a mudança, atualiza rótulos/estados e chama `onChange` (que vai a `Apply()`).
- **`RefreshControls()`**: puxa os valores atuais para os controles. É chamado pelo `PaperContext` depois de `SaveAndApply()`, para a janela refletir mudanças vindas da bandeja, do atalho ou da agenda.
- **`UpdateEnabledState()`**: desabilita controles quando o efeito está desligado e as opções de cada efeito de mouse quando ele está desligado; o botão Salvar só fica ativo com `HasUnsavedChanges`; os horários seguem ativos se a agenda *ou* o tema do Windows estiver ligado.
- **Fechar:** `OnFormClosing` pergunta sobre alterações pendentes (exceto em desligamento do Windows, onde descarta). `OnFormClosed` salva e chama `onChange` para a tela voltar ao visual salvo se o rascunho foi descartado.

### Gravação do atalho

O campo é um `TextBox` somente leitura:

1. Ao receber foco, **suspende** o atalho atual (`HotkeyManager.Suspend(true)`) para a combinação chegar ao campo em vez de acionar o efeito.
2. `OnHotkeyKeyDown` espera uma tecla "de verdade" (ignora só Ctrl/Shift/Alt/Win), valida com `Hotkey.IsValid`, grava em `ToggleHotkey` e tira o foco (o que reativa o registro e mostra se deu certo).
3. Delete/Backspace = sem atalho; Esc cancela.
4. A tecla Win é detectada por `Hotkey.WinKeyDown`, porque os eventos de teclado do WinForms não a informam.
5. `ProcessDialogKey` evita que Enter/Esc fechem a janela enquanto o campo está em foco.

### Importar e exportar

- **Exportar:** confirma alterações pendentes e grava com `Settings.Write` (todos os perfis e opções gerais).
- **Importar:** confirma alterações, lê com `Settings.Read`, chama `ImportProfiles` e mostra quantos perfis entraram.
