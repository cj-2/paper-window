# Mouse: cursor temático, rastro e pingos de tinta

Os efeitos de mouse são três recursos separados no perfil (`CursorTheme`, `InkTrail`, `InkDrop`), cada um com seu liga/desliga.

## Cursor temático — `CursorTheme.cs`

### O problema

O Windows desenha o cursor num plano próprio, **fora** do filtro de cor da Magnification. Sem tratamento, o cursor ficaria com as cores originais. A solução é substituir os cursores do sistema por imagens desenhadas por código e já passadas pela mesma matriz de cor do perfil.

### Como funciona

- `CursorStyles.All` lista os estilos: `seta`, `lapis`, `pena`, `giz`.
- `CursorTheme.Apply(estilo, matriz, tamanho)` instala (ou, com `estilo == null`, restaura) os cursores:
  1. Ajusta o tamanho pela métrica de cursor do sistema (`SM_CXCURSOR`), então escala do Windows ou ponteiro grande aumentam o cursor na mesma proporção.
  2. Gera uma chave `estilo|tamanho|matriz` e **sai cedo se for igual à instalada** (evita reinstalar a cada tique de um slider).
  3. Cria uma `Palette` que converte as cores da arte (`InkDark`, `InkLight`, `Paper`) com `ColorEffects.Transform`.
  4. Desenha o ponteiro (`DrawPointer`) e o cursor de texto (`DrawIBeam`), converte cada bitmap em cursor (`MakeCursor`, via `CreateIconIndirect`) e chama `SetSystemCursor`.
- Substitui três cursores: seta (`OCR_NORMAL`), mão de link (`OCR_HAND`, mesmo desenho da seta) e cursor de texto (`OCR_IBEAM`).
- Os desenhos usam uma grade de 32 unidades escalada para o tamanho pedido; o ponto ativo (hotspot) fica na ponta.

### Restaurar

`CursorTheme.Restore()` chama `SystemParametersInfo(SPI_SETCURSORS)`, que recarrega o esquema de cursores do usuário. É chamada:

- ao desligar a opção ou o efeito;
- ao sair do programa;
- **na inicialização** (caso a execução anterior tenha caído);
- em exceções não tratadas.

> `SetSystemCursor` é válido até o logoff ou até um `SPI_SETCURSORS`. Por isso a restauração defensiva é importante.

### Adicionar um estilo de cursor

1. Acrescente um `CursorStyle` em `CursorStyles.All` (id estável).
2. Crie o método de desenho `DrawXxx(Graphics, Palette)` e inclua o `case` em `DrawPointer`.

## Camada de tinta — `InkLayer.cs`

Rastro e pingos são desenhados **juntos numa única janela** (`LayeredWindow`). Isso permite que se misturem como uma tinta só: onde se encontram, vale a tinta mais forte (em vez de somar e formar uma emenda escura), e o traço "sai" do pingo do próprio clique.

### Sem hook global

Não há hook de mouse. Um `Timer` de 15 ms (`Tick`) consulta:

- `Native.PrimaryButtonDown()`: botão principal pressionado (`GetAsyncKeyState`, respeitando a troca de botões do Windows);
- `Cursor.Position`: posição do ponteiro.

Detecta a borda de descida (clique) e de subida (soltar). O timer só roda quando rastro ou pingo está ligado; ao ligar, um botão já pressionado não conta como clique (`_wasDown`).

### O que acontece em cada tique

1. **Clique:** cria um `Drop` (se pingos ligados) e um `Stroke` (se rastro ligado) cujo `Origin` é o pingo do mesmo clique.
2. **Botão segurado:** o `Stroke` atual recebe uma amostra por tique (mesmo parado, porque o filtro de suavização precisa de amostras regulares).
3. **Soltou:** `Stroke.Release` fecha o traço onde o cursor realmente parou.
4. **Limpeza:** `Expire` remove pontos velhos; traços e pingos acabados saem das listas.
5. **Desenho:** se não sobrou nada, esconde a janela; senão `Render` gera um bitmap cobrindo **só a área da tinta** e o `Present` mostra.

### `Render` em duas passadas

1. **Passada 1:** rastro e pingos são desenhados em canvases separados, **opacos**, usando o nível de cinza como "vida/força" da tinta. Sobreposições opacas não somam, então emendas e cruzamentos não escurecem.
2. **Passada 2** (`unsafe`, por pixel): converte cobertura × vida em alfa da tinta. A vida do rastro entra ao quadrado (as partes velhas somem mais rápido). Onde rastro e pingo se encontram, vale o máximo dos dois. A cor é fixa (`Ink`), com alfa máximo 210.

## Rastro — `InkTrail.cs`

Um `Stroke` por clique, que guarda os pontos `(posição, tempo)`.

- **Suavização:** as amostras passam pelo **filtro 1€** (`OneEuroFilter`): filtro passa-baixa cuja frequência de corte sobe com a velocidade. Devagar (onde aparece o tremor) suaviza forte; em movimentos rápidos acompanha com pouca defasagem. Um ponto só é guardado depois de andar `MinStep` (3 px).
- **Ponta (`Tip`):** enquanto o botão está pressionado, a ponta liga o traço suavizado ao cursor real, para a tinta nunca ficar atrás do ponteiro.
- **Curva:** `Smooth` aplica **Catmull-Rom** entre os pontos, subdividindo a cada poucos pixels, para flicks rápidos saírem arredondados e não em zigue-zague.
- **Espessura:** `DrawLife` desenha cada segmento com a largura `maxWidth * pressão * (0,4 + 0,6 * vida)`. A `Pressure` é 1 enquanto o botão está pressionado e, ao soltar, afina com ease-out até 30% em 220 ms.
- **Pescoço (`Neck`):** o começo do traço herda a largura e a força do pingo e vai se igualando ao traço normal ao longo de um comprimento que depende do raio do pingo e da velocidade inicial (medida nos primeiros 80 ms). É o que faz parecer tinta puxada da mancha.

## Pingos — `InkDrops.cs`

Um `Drop(Pos, Time, Seed)` é imutável. Linha do tempo:

| Tempo | O que acontece |
|---|---|
| 0–140 ms | Mancha se espalha (ease-out cúbico) até o raio `Radius` (6 px a 96 dpi) |
| até 260 ms | Totalmente carregada de tinta |
| 260–900 ms | Vai desbotando; some em 900 ms |

`DrawLife` desenha uma mancha irregular (12 lóbulos, `FillClosedCurve`) mais 3–5 respingos. A forma vem de um `Random(Seed)`, então o **mesmo pingo sempre tem o mesmo desenho** a cada quadro (não fica tremendo).

## DPI

Tudo que é medido em pixels (largura, raio, passo mínimo) é multiplicado por `scale = DeviceDpi / 96`.

## Captura de tela

A janela da tinta pode ser excluída de prints/gravações pela opção `HideMouseFromCapture` (desligada por padrão: o rastro ajuda a mostrar onde você aponta). O cursor temático **sempre** aparece em capturas, porque substitui o cursor do próprio Windows.
