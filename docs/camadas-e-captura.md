# Camadas, threads e captura de tela

Este documento junta o que está espalhado nos outros: **onde cada coisa roda**, **como as camadas se empilham** e **por que a captura de tela se comporta de um jeito para cada camada**.

## Um processo, uma thread

Não há processos separados para mouse, efeitos ou textura.

- `Program.Main` é `[STAThread]` e roda `Application.Run(new PaperContext())`: o loop de mensagens do WinForms, na thread de UI.
- O mutex `Local\PaperWindow.SingleInstance` só impede uma segunda cópia do programa.
- Todos os timers são `System.Windows.Forms.Timer`, que disparam **na thread de UI**:

| Timer | Intervalo | Dono | Para quê |
|---|---|---|---|
| Tinta | 15 ms | `InkLayer` | Ler botão e posição do mouse, redesenhar rastro e pingos. Só roda com rastro ou pingo ligado |
| Manter no topo | 2 s | `PaperContext` | Reafirmar `HWND_TOPMOST` nas janelas. Só roda com o efeito ligado |
| Agenda | 30 s | `PaperContext` | Conferir a virada dia/noite |

- O atalho global chega por uma janela só de mensagens (`HWND_MESSAGE`), no mesmo loop.
- Não há threads de trabalho nem hook global de mouse. O mouse é lido por **polling** (`GetAsyncKeyState` e `Cursor.Position`).

Consequência prática: como tudo roda na thread de UI, um trabalho pesado dentro de um `Tick` ou de um evento trava a tela, a tinta e a janela de configurações ao mesmo tempo. Por isso o `Render` da tinta desenha só a área coberta pela tinta, e a textura usa um tile em cache em vez de gerar o bitmap inteiro com ruído.

## As camadas

```
        ┌─ apps normais (o que você usa) ────────────────────────┐
        │                                                        │
 acima  │  InkLayer    1 janela  (rastro + pingos)               │  janelas do próprio programa:
        │  OverlayForm 1 por monitor (textura)                   │  topmost, click-through, sem foco
        ├────────────────────────────────────────────────────────┤
        │  compositor do Windows (DWM) junta tudo                │
        ├────────────────────────────────────────────────────────┤
        │  Magnification: matriz de cor na tela inteira          │  o "e-ink"; aplicado pelo Windows
        └────────────────────────────────────────────────────────┘
 à parte: o cursor, que o Windows desenha em plano próprio
```

| Camada | Quem produz | Quem aplica | Custo contínuo |
|---|---|---|---|
| Filtro de cor | Programa envia 25 floats (`MagSetFullscreenColorEffect`) | **Windows**, em tudo | Nenhum depois de enviado. Só reenvia quando algo muda |
| Textura | `PaperTexture.Create` gera o bitmap uma vez por monitor | **Windows** compõe a janela | Nenhum. Mudar a opacidade só troca o alfa da janela |
| Tinta | `InkLayer.Render` a cada tique, só enquanto há tinta | Windows compõe a janela | Timer de 15 ms; janela escondida quando não há tinta |
| Cursor | `CursorTheme` desenha o bitmap ao aplicar | **Windows** usa como cursor do sistema | Nenhum depois de instalado |

### Por que cada janela é "invisível" para o uso

`LayeredWindow` combina estilos que a tornam só uma imagem na tela: `WS_EX_LAYERED` (bitmap com alfa por pixel), `WS_EX_TRANSPARENT` (cliques passam), `WS_EX_NOACTIVATE` (não rouba foco), `WS_EX_TOOLWINDOW` (fora do Alt+Tab) e `WS_EX_TOPMOST`. Sem `ShowInTaskbar`. Detalhes em [visual.md](visual.md).

### Por que a textura e a tinta ganham a cor do perfil

As duas janelas são desenhadas pelo compositor e entram no resultado que passa pela matriz de cor. Não é preciso calcular nada: o grão e a tinta já saem na tonalidade do perfil.

O **cursor** não passa pelo filtro, porque o Windows o desenha por fora. Por isso o `CursorTheme` aplica a matriz às cores da arte antes de instalar o cursor (`ColorEffects.Transform`). Quando você muda o perfil, o cursor é redesenhado e reinstalado (se a chave estilo + tamanho + matriz mudou).

## Captura de tela: por que cada camada se comporta de um jeito

O controle vem da flag `WDA_EXCLUDEFROMCAPTURE`, ligada com `SetWindowDisplayAffinity` em `LayeredWindow.SetCaptureExcluded`:

- A janela continua **visível no monitor**, mas o Windows a **deixa fora da composição usada para captura**: prints, gravações e compartilhamento de tela.
- A flag é **por janela**. Por isso há duas opções independentes:
  - `HideTextureFromCapture` (padrão **ligada**): afeta os `OverlayForm` (textura).
  - `HideMouseFromCapture` (padrão **desligada**): afeta a janela do `InkLayer`. Fica desligada de propósito, para o rastro e os pingos aparecerem em gravações e mostrarem onde você aponta e clica.
- Requer Windows 10 build 19041 ou superior. Em builds antigos o mesmo valor agiria como `WDA_MONITOR` (a janela viraria uma caixa preta na captura). Por isso `SetCaptureExcluded` retorna sem fazer nada nessas versões.

Resultado por camada:

| Camada | Aparece na captura? | Motivo |
|---|---|---|
| Textura | Não, por padrão (configurável) | É uma janela com a flag |
| Tinta (rastro e pingos) | Sim, por padrão (configurável) | É uma janela; a flag só entra se a opção estiver ligada |
| Cursor temático | **Sempre** | Substitui o cursor do próprio Windows; não é uma janela do programa |
| Filtro de cor | **Depende da ferramenta** | Não é uma janela, então a flag não se aplica. Não há controle do programa sobre isso |

**Observação testada:** uma captura por GDI (`Graphics.CopyFromScreen`, do .NET) **incluiu** o filtro de cor: a média de cor de uma área da tela veio em tom quente com o app rodando e em cores normais depois de fechá-lo. Outras ferramentas (por exemplo as que usam a API de captura de tela do Windows 10+ ou o compartilhamento de tela de apps de reunião) não foram testadas e podem se comportar de outro jeito.

Quando o pedido é "capturar sem nenhum efeito", o que o programa consegue garantir é a textura (e a tinta, se a opção estiver ligada). O filtro de cor e o cursor temático dependem da ferramenta de captura ou não podem ser escondidos. Para uma captura totalmente "limpa", desligue o efeito (atalho `Ctrl+Alt+Win+P`), que também restaura os cursores.

## O que o programa desfaz ao sair

Tudo que muda o estado do sistema é revertido em `PaperContext.ExitThreadCore`:

| Alteração | Como é desfeita |
|---|---|
| Filtro de cor | `SetColorMatrix(Identity)` e `MagUninitialize` |
| Cursores do sistema | `CursorTheme.Restore()` (`SPI_SETCURSORS`) |
| Janelas de textura e tinta | `CloseOverlays()` e `_ink.Dispose()` |
| Atalho global | `HotkeyManager.Dispose()` |
| Ícone da bandeja | `Visible = false` e `Dispose()` |

Se o programa **cair** sem passar por aí:

- o **filtro de cor** é do Magnification e some quando o processo morre, porque a API está ligada ao processo que a inicializou. **Testado:** com o programa rodando, a média de cor de uma área da tela era RGB 91,81,70 (tom quente); depois de `Stop-Process -Force` (morte sem passar por `ExitThreadCore`), passou a 85,89,97 (cores normais), sem precisar fazer nada;
- os **cursores** continuam instalados até o logoff ou até um `SPI_SETCURSORS`. Por isso `CursorTheme.Restore()` roda na abertura e em `UnhandledException`;
- o **tema claro/escuro** e o **perfil ativo** são mudanças persistentes de propósito e não são desfeitos.

## Quando as camadas são recriadas

| Evento | O que é refeito |
|---|---|
| Mudar o estilo de textura | Fecha e reabre todos os `OverlayForm` (`_overlayTexture` guarda o estilo atual) |
| Mudar a opacidade da textura | Só `SetOpacity`: não recria nada |
| Mudar monitores ou resolução (`DisplaySettingsChanged`) | Fecha os overlays e reaplica (um por monitor atual) |
| Mudar cor, intensidade, luz azul ou brilho | Recalcula e reenvia a matriz; reinstala o cursor se a matriz mudou |
| Ligar/desligar tinta | Liga/desliga o timer e esconde a janela |
| Desligar o efeito (`Enabled = false`) | Matriz = identidade, cursor restaurado, overlays fechados, tinta desligada |
