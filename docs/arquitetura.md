# Arquitetura

## Stack

- .NET 8, Windows Forms (`net8.0-windows`), `WinExe`, DPI `PerMonitorV2`.
- `AllowUnsafeBlocks` ligado: a textura e a camada de tinta mexem direto nos pixels dos bitmaps.
- Sem dependências externas (NuGet). Tudo que não é .NET é P/Invoke em `Native.cs`.
- O ícone (`assets/paper.ico`) é embutido como recurso `paper.ico` e lido por `AppIcon`.

## Mapa de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `Program.cs` | `Main` (instância única por mutex), `AppIcon` e `PaperContext`, o **orquestrador** do app |
| `Settings.cs` | `Profile` (um visual), `Settings` (tudo que é salvo, mais rascunho e regras de perfis) e `Startup` (iniciar com o Windows) |
| `SettingsForm.cs` | Janela de configurações (abas, prévia ao vivo, salvar/novo/renomear/excluir, importar/exportar, gravação do atalho) |
| `NameDialog.cs` | Diálogo modal pequeno que pede o nome de um perfil |
| `ColorEffects.cs` | Efeitos de cor e a matemática das matrizes de cor (efeito, luz azul, brilho) |
| `PaperTexture.cs` | Estilos de textura e geração procedural do tile de ruído |
| `LayeredWindow.cs` | Base das janelas sobrepostas: transparente, sem clique, sempre no topo, alfa por pixel |
| `OverlayForm.cs` | Janela da textura, uma por monitor |
| `InkLayer.cs` | Janela única do mouse: junta rastro e pingos, faz o timer e a composição |
| `InkTrail.cs` | Desenho do rastro: traço, suavização (filtro 1€, Catmull-Rom), afinamento |
| `InkDrops.cs` | Desenho do pingo de tinta |
| `CursorTheme.cs` | Cursores desenhados por código e instalação no lugar dos do sistema |
| `Hotkey.cs` | `Hotkey` (texto ⇄ combinação) e `HotkeyManager` (registro global) |
| `WindowsTheme.cs` | Alterna o modo claro/escuro do Windows |
| `Native.cs` | Declarações P/Invoke e constantes Win32 |
| `tools/make-icon.ps1` | Gera o `.ico` desenhando o jornalzinho por código |

## O orquestrador: `PaperContext`

`PaperContext` (em `Program.cs`) é um `ApplicationContext`: não há janela principal, o app vive na bandeja. Ele é dono de tudo que tem estado em tempo de execução:

- o `Settings` carregado;
- o `NotifyIcon` e o menu de contexto;
- o `HotkeyManager`;
- a lista de `OverlayForm` (textura), o `InkLayer` (mouse) e, se aberta, a `SettingsForm`;
- dois timers: `_topmostTimer` (2 s, devolve as janelas ao topo) e `_scheduleTimer` (30 s, confere a agenda dia/noite).

### O método `Apply()`

É o ponto central: **traz a tela em sintonia com as configurações atuais**. É idempotente e chamado sempre que algo muda. Em ordem:

1. Calcula a matriz de cor do visual mostrado e a aplica (`SetColorMatrix`).
2. Instala (ou restaura) o cursor temático com essa mesma matriz.
3. Configura o `InkLayer` (rastro, pingos, esconder em captura).
4. Registra o atalho global.
5. Abre/fecha os overlays se a textura mudou; ajusta opacidade e exclusão de captura em cada um.
6. Liga/desliga o timer de manter-no-topo.

Regra do projeto: **quem muda uma configuração chama `Apply()`** (direto ou via `SaveAndApply()`), e não mexe na tela por conta própria.

### Ciclo de vida

```
Main ── mutex de instância única ── Application.Run(PaperContext)
  construtor: MagInitialize → CursorTheme.Restore → monta bandeja/menu/timers
              → CheckSchedule → Apply → (1ª execução: abre configurações)
  em execução: eventos (atalho, menu, timers, SystemEvents) → SaveAndApply/Apply
  ExitThreadCore: fecha a janela (pode cancelar) → desfaz tudo:
              cursores originais, overlays fechados, matriz = identidade, MagUninitialize
```

Detalhes que importam:

- **Restauração no início:** `CursorTheme.Restore()` roda ao abrir, caso uma execução anterior tenha caído com cursores temáticos instalados. Também é chamada em `UnhandledException`.
- **Sair com alterações pendentes:** `ExitThreadCore` fecha a `SettingsForm`; se o usuário cancelar a pergunta de salvar, a janela continua aberta e o app **não** sai.
- **Eventos do sistema** (`SystemEvents`): `DisplaySettingsChanged` recria os overlays (monitores mudaram), `TimeChanged` e `PowerModeChanged(Resume)` reconferem a agenda.

## Fluxo de dados

```
settings.json ⇄ Settings ── Shown (rascunho ou perfil salvo) ──► PaperContext.Apply()
                                                                  ├─► ColorEffects → Magnification (tela inteira)
                                                                  ├─► CursorTheme  (usa a mesma matriz)
                                                                  ├─► InkLayer     (rastro + pingos)
                                                                  ├─► OverlayForm × monitores (textura)
                                                                  └─► HotkeyManager
SettingsForm ──edita rascunho──► onChange() ──► Apply()
Tray / atalho / agenda ──► SaveAndApply() ──► Settings.Save() + Apply()
```

A janela de configurações recebe um callback `onChange` do `PaperContext`. Cada controle de visual edita um **rascunho** do perfil e dispara `onChange`, o que dá a **prévia ao vivo** sem tocar no perfil salvo (veja [configuracoes.md](configuracoes.md)).

## Convenções do código

- Classes `internal`; namespace único `PaperWindow`.
- Estilos e efeitos são **tabelas estáticas** (`ColorEffects.All`, `TextureStyles.All`, `CursorStyles.All`) com um `Find(id)` que cai no primeiro item se o id for desconhecido. Isso torna arquivos de versões novas/antigas tolerantes.
- Ids de estilo gravados no JSON nunca devem ser renomeados (ex.: o cursor `pena` mantém o id antigo para não perder a escolha de quem já salvou).
- Comentários no código estão em inglês; textos para o usuário, em português.
- Valores de configuração são sempre validados em `Normalize()` ao ler o arquivo (faixas, ids desconhecidos).
