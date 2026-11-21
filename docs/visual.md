# Visual: filtro de cor, textura e janelas sobrepostas

O visual de "papel" tem duas camadas independentes: o **filtro de cor** (muda as cores de tudo na tela) e a **textura** (grão desenhado por cima).

## Filtro de cor — `ColorEffects.cs`

### Como o Windows aplica

A API de **Magnification** (`magnification.dll`) permite definir uma matriz de cor 5×5 para a tela inteira (`MagSetFullscreenColorEffect`). O Windows multiplica cada pixel `[r g b a 1]` pela matriz. Vantagens: é rápido (feito pelo sistema) e atinge todos os monitores. Limitação: só **um** filtro de Magnification por vez no sistema (a Lupa ou a inversão de cores disputam o mesmo recurso).

`PaperContext` chama `MagInitialize` no início e `MagUninitialize` na saída. Se a inicialização falhar (`_magReady == false`), o filtro é simplesmente ignorado.

### O modelo de um efeito

Um `ColorEffect` tem `Id`, `Name`, `Ink`, `Paper` e `Saturation`:

- A **luminância** do pixel é mapeada numa rampa que vai de `Ink` (o preto vira essa cor) até `Paper` (o branco vira essa cor).
- `Saturation` mantém parte da cor original (0 = rampa de cinza pura; `colorido` usa 0,45).

`ColorEffects.All` lista os efeitos (E-ink quente, neutro, Sépia, Jornal, Papel kraft, Papel colorido).

### Pipeline da matriz

O visual mostrado passa por três etapas, nesta ordem (em `PaperContext.ColorMatrix()`):

1. `Matrix(efeito, intensidade)`: monta a matriz do efeito e faz mistura com a identidade. Intensidade 0 = cores originais, 1 = efeito completo.
2. `ReduceBlueLight(m, quantidade)`: escala para baixo as colunas de saída verde (até −25%) e azul (até −65%), como uma luz noturna.
3. `Dim(m, fator)`: escala todas as colunas (inclusive o deslocamento) pelo brilho (mínimo 20%).

Se o efeito estiver desligado (`Settings.Enabled == false`), usa-se `Identity()`.

`Transform(m, cor)` faz o caminho inverso útil: calcula como uma cor *aparece* na tela depois da matriz. É usada pelo cursor temático (ver [mouse.md](mouse.md)).

### Adicionar um efeito

Acrescente uma linha em `ColorEffects.All` com id estável, nome, cor da tinta, cor do papel e saturação. A janela de configurações lista os efeitos a partir dessa tabela; não precisa mexer em mais nada.

## Textura — `PaperTexture.cs`

Um `TextureStyle` define os pesos de quatro camadas de ruído (`Coarse`, `Mid`, `Fine`, `Grain`), um `Gamma` e a cor `R,G,B` do grão. `TextureStyles.All` lista os estilos (Fibras, Grão fino, Jornal pontilhado, Aquarela).

### Geração

`PaperTexture.Create(style, w, h)` devolve um bitmap `w × h`:

1. Constrói **um tile de 512×512** por estilo (guardado em cache no dicionário `Tiles`) com `BuildTile`:
   - três redes de ruído de valor (`Lattice` de 8, 32 e 128 células) amostradas com interpolação suave (`Sample`) e **com quebra de borda**, para o tile ser contínuo;
   - mais um ruído branco (`Grain`);
   - a soma ponderada passa por `pow(n, Gamma)`: quanto maior o gamma, mais o resultado vira pontinhos escuros esparsos;
   - o valor vira o **alfa** do pixel (até `MaxAlpha = 64`).
2. Repete (ladrilha) o tile pela área pedida, escrevendo direto na memória do bitmap (`unsafe`).

A semente do gerador é fixa (`1337`), então a textura é sempre a mesma.

### Opacidade

O controle de opacidade do usuário **não** refaz a textura: ele ajusta o alfa constante da janela (`LayeredWindow.SetOpacity`). `MaxAlpha` só limita o quão forte a textura fica em 100%.

### Adicionar uma textura

Acrescente uma linha em `TextureStyles.All`. Para mudar a força máxima, ajuste `MaxAlpha`.

## Janelas sobrepostas — `LayeredWindow.cs` e `OverlayForm.cs`

`LayeredWindow` é um `Form` sem borda e sem barra de tarefas com estilos estendidos do Windows:

| Estilo | Efeito |
|---|---|
| `WS_EX_LAYERED` | A janela mostra um bitmap com alfa por pixel |
| `WS_EX_TRANSPARENT` | Cliques passam direto para o que está embaixo |
| `WS_EX_NOACTIVATE` + `ShowWithoutActivation` | Nunca rouba o foco |
| `WS_EX_TOOLWINDOW` | Fora do Alt+Tab |
| `WS_EX_TOPMOST` | Sempre no topo |

Métodos principais:

- `Present(bitmap, posição)`: envia o bitmap com `UpdateLayeredWindow` (cria DCs de GDI, seleciona o HBITMAP e libera tudo no `finally`).
- `SetOpacity(alfa)`: muda só o alfa constante, sem reenviar o bitmap.
- `SetCaptureExcluded(bool)`: usa `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` para a janela aparecer para você mas não em prints/gravações. Só no Windows 10 build 19041+; em versões antigas o flag viraria um quadrado preto, então é ignorado.
- `KeepOnTop()`: reafirma `HWND_TOPMOST`. Chamado a cada 2 s porque a barra de tarefas e outras janelas topmost podem subir acima.

`OverlayForm` é um `LayeredWindow` que, ao criar o handle, gera a textura com o tamanho do monitor e a apresenta. **Há uma por monitor** (`Screen.AllScreens`). Quando o estilo de textura ou os monitores mudam, `PaperContext` descarta todas e recria (`CloseOverlays` / `OpenOverlays`).

### Por que a textura também passa pelo filtro de cor

A janela da textura é composta *antes* do filtro da Magnification, então o grão herda as cores do perfil sem trabalho extra. O mesmo vale para a camada de tinta do mouse.
