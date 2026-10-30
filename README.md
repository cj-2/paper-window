# Paper Window

<img src="assets/paper-preview.png" width="96" alt="Ícone do Paper Window: um jornalzinho sorridente">

Deixa a tela do Windows com cara de e-paper / papel:

- **Filtro de cor em tela cheia** (Magnification API do Windows): tons de cinza puxados para papel quente. O preto vira cinza-tinta e o branco vira off-white, com o contraste um pouco menor, como num e-ink.
- **Textura de papel**: uma janela transparente que ignora cliques, fica sempre no topo e cobre todos os monitores com grão e fibras sutis.
- **Atalho de teclado** para ligar/desligar de qualquer lugar: `Ctrl+Alt+Win+P` (dá para trocar)
- Ícone na bandeja do sistema (perto do relógio):
  - **Duplo clique** ou **Configurações...**: abre a janela de configuração
  - **Efeito papel ativo**: liga/desliga
  - **Perfil**: troca rápida entre perfis salvos
  - **Sair**: fecha e devolve as cores normais

## Configurações

Tudo é aplicado na hora, e salvo em `%APPDATA%\PaperWindow\settings.json` quando você fecha a janela.
Na primeira execução a janela abre sozinha.

| Opção | O que faz |
|---|---|
| Efeito ativo | Liga/desliga filtro e textura |
| Perfil | Escolhe o perfil ativo; **Novo...** cria uma cópia do atual, **Renomear...** e **Excluir** |
| Efeito de cor | E-ink quente, E-ink neutro, Sépia, Jornal, Papel kraft, Papel colorido (mantém um pouco da cor) |
| Intensidade do filtro | 0% = cores originais, 100% = efeito completo |
| Filtro de luz azul | Esquenta a tela reduzindo azul (até 65%) e um pouco de verde (até 25%), como uma luz noturna; 0% = desligado |
| Textura do papel | Fibras, Grão fino, Jornal (pontilhado), Aquarela |
| Opacidade da textura | 0% = sem textura, 100% = textura máxima |
| Esconder textura em prints e compartilhamento de tela | A textura continua visível para você, mas não aparece em prints, gravações nem compartilhamento de tela (Windows 10 2004+) |
| Atalho liga/desliga | Atalho global para ligar/desligar o efeito |
| Iniciar com o Windows | Abre sozinho ao fazer login (`HKCU\...\Run`) |

### Perfis

Cada perfil guarda um visual: efeito de cor, intensidade, luz azul, textura e opacidade.
Mexer nesses controles altera o perfil ativo na hora, sem botão de salvar. "Restaurar padrão" volta só o perfil atual para os valores padrão.
As opções gerais (efeito ativo, esconder em prints, atalho, iniciar com o Windows) valem para todos os perfis.

Arquivos de configuração antigos (de antes dos perfis) viram automaticamente um perfil chamado "Padrão".

### Atalho de teclado

O padrão é `Ctrl+Alt+Win+P`. Para trocar (por exemplo, se conflitar com outro atalho seu):

1. Clique no campo **Atalho liga/desliga**
2. Pressione a nova combinação. Ela precisa ter Ctrl, Alt ou Win, ou ser uma tecla F1–F24 sozinha
3. **Delete** ou **Backspace** deixa sem atalho, e **Esc** cancela

Se outro programa já usa a combinação, aparece um aviso em vermelho na janela (e um balão na bandeja ao iniciar). Aí é só escolher outra.

### Importar e exportar

- **Exportar...** salva todos os perfis e as opções gerais num arquivo `.json`, para backup, para levar para outro PC ou para compartilhar.
- **Importar...** **adiciona** os perfis do arquivo aos seus, sem apagar nada: perfis idênticos são ignorados, e nomes repetidos ganham um sufixo, como "Noite (2)". O perfil que estava ativo no arquivo passa a ser o ativo. As suas opções gerais não mudam.
- Efeitos ou texturas que não existem nesta versão voltam para o padrão, e valores fora da faixa são ajustados para 0–100%.
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

- O filtro de luz azul soma com a "Luz noturna" do Windows se as duas estiverem ligadas; normalmente vale usar só uma.
- O filtro de cor pode ou não aparecer em prints, dependendo da ferramenta de captura usada. A opção de esconder vale só para a textura.
- O Windows só permite um filtro de cor da Magnification por vez: se você usar Lupa/inversão de cores ao mesmo tempo, um sobrescreve o outro.
