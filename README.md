# Paper Window

<img src="assets/paper-preview.png" width="96" alt="Ícone do Paper Window: um jornalzinho sorridente">

Deixa a tela do Windows com cara de e-paper / papel:

- **Filtro de cor em tela cheia** (Magnification API do Windows): tons de cinza puxados para papel quente. O preto vira cinza-tinta e o branco vira off-white, com o contraste um pouco menor, como num e-ink.
- **Textura de papel**: uma janela transparente que ignora cliques, fica sempre no topo e cobre todos os monitores com grão e fibras sutis.
- Ícone na bandeja do sistema (perto do relógio):
  - **Duplo clique** ou **Configurações...**: abre a janela de configuração
  - **Efeito papel ativo**: liga/desliga
  - **Sair**: fecha e devolve as cores normais

## Configurações

Tudo é aplicado na hora, e salvo em `%APPDATA%\PaperWindow\settings.json` quando você fecha a janela.
Na primeira execução a janela abre sozinha.

| Opção | O que faz |
|---|---|
| Efeito ativo | Liga/desliga filtro e textura |
| Efeito de cor | E-ink quente, E-ink neutro, Sépia, Jornal, Papel kraft, Papel colorido (mantém um pouco da cor) |
| Intensidade do filtro | 0% = cores originais, 100% = efeito completo |
| Textura do papel | Fibras, Grão fino, Jornal (pontilhado), Aquarela |
| Opacidade da textura | 0% = sem textura, 100% = textura máxima |
| Esconder textura em prints e compartilhamento de tela | A textura continua visível para você, mas não aparece em prints, gravações nem compartilhamento de tela (Windows 10 2004+) |
| Iniciar com o Windows | Abre sozinho ao fazer login (`HKCU\...\Run`) |

### Importar e exportar

- **Exportar...** salva as configurações atuais num arquivo `.json` (por exemplo, para fazer backup ou levar para outro PC).
- **Importar...** carrega um arquivo exportado e aplica na hora. Efeitos ou texturas que não existem nesta versão voltam para o padrão, e valores fora da faixa são ajustados para 0–100%.
- "Iniciar com o Windows" não entra no arquivo, porque é uma configuração de cada computador.

## Compilar

Requer o .NET 8 SDK.

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

O executável sai em `publish\PaperWindow.exe` (precisa do .NET 8 Desktop Runtime instalado).
Se quiser um exe que roda em qualquer PC sem runtime, troque por `--self-contained true` (fica com ~150 MB).

## Ajustes

- Novos efeitos de cor: adicione uma linha em `ColorEffects.All` (`ColorEffects.cs`) com cor da tinta, cor do papel e quanto da cor original manter
- Novas texturas: adicione uma linha em `TextureStyles.All` (`PaperTexture.cs`) com o peso de cada camada de ruído e a cor do grão
- Opacidade máxima da textura: `MaxAlpha` em `PaperTexture.cs`
- Ícone (jornalzinho): desenhado por código em `tools\make-icon.ps1`; edite e rode `pwsh tools\make-icon.ps1` para gerar `assets\paper.ico` de novo

## Limitações

- O filtro de cor pode ou não aparecer em prints, dependendo da ferramenta de captura usada. A opção de esconder vale só para a textura.
- O Windows só permite um filtro de cor da Magnification por vez: se você usar Lupa/inversão de cores ao mesmo tempo, um sobrescreve o outro.
