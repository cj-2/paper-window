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

Tudo é aplicado na hora como prévia. As mudanças de perfil só são guardadas quando você clica em **Salvar**; as opções gerais são salvas ao fechar a janela. Tudo fica em `%APPDATA%\PaperWindow\settings.json`.
Na primeira execução a janela abre sozinha.

No topo ficam **Efeito ativo** e o **Perfil** que está sendo editado. Abaixo, as abas **Cor e brilho**, **Textura** e **Mouse** mexem no visual do perfil; **Dia e noite** e **Geral** valem para todos os perfis. **Importar**, **Exportar**, **Restaurar padrão** e **Fechar** ficam no rodapé.

| Onde | Opção | O que faz |
|---|---|---|
| Topo | Efeito ativo | Liga/desliga filtro e textura |
| Topo | Perfil | Escolhe o perfil ativo; **Salvar** guarda as alterações, **Novo...** cria um perfil com o visual da tela, **Renomear...** e **Excluir** |
| Cor e brilho | Efeito de cor | E-ink quente, E-ink neutro, Sépia, Jornal, Papel kraft, Papel colorido (mantém um pouco da cor) |
| Cor e brilho | Intensidade do filtro | 0% = cores originais, 100% = efeito completo |
| Cor e brilho | Filtro de luz azul | Esquenta a tela reduzindo azul (até 65%) e um pouco de verde (até 25%), como uma luz noturna; 0% = desligado |
| Cor e brilho | Brilho | Escurece a tela por software, de 100% (normal) até 20%; funciona em qualquer monitor e escurece além do mínimo do próprio monitor |
| Textura | Textura do papel | Fibras, Grão fino, Jornal (pontilhado), Aquarela |
| Textura | Opacidade da textura | 0% = sem textura, 100% = textura máxima |
| Mouse | Cursor temático | Troca a seta, a mãozinha de links e o cursor de texto por cursores desenhados nas cores do perfil. Estilos: Seta de tinta, Lápis, Caneta bico de pena, Giz de cera |
| Mouse | Rastro de tinta ao arrastar | Enquanto o botão principal está pressionado, o mouse deixa um traço de tinta que vai afinando e desbotando, como caneta no papel ou o "fantasma" de telas e-ink. A duração vai de 0,2 s a 1,5 s |
| Mouse | Espessura do rastro | Grossura do traço com o botão pressionado, de 2 px a 12 px. Ao soltar, o traço afina até 30% disso |
| Mouse | Pingo de tinta ao clicar | Cada clique deixa um pingo de tinta que se espalha numa manchinha com respingos e some em menos de 1 s. Liga/desliga separado do rastro |
| Dia e noite | Trocar de perfil automaticamente entre dia e noite | Ativa o perfil do dia e o da noite nos horários escolhidos (veja [Dia e noite](#dia-e-noite)) |
| Geral | Esconder textura, rastro e pingos em prints e compartilhamento de tela | A textura, o rastro e os pingos continuam visíveis para você, mas não aparecem em prints, gravações nem compartilhamento de tela (Windows 10 2004+) |
| Geral | Atalho liga/desliga | Atalho global para ligar/desligar o efeito |
| Geral | Iniciar com o Windows | Abre sozinho ao fazer login (`HKCU\...\Run`) |

### Perfis

Cada perfil guarda um visual: efeito de cor, intensidade, luz azul, brilho, textura, opacidade e os efeitos do mouse (cursor temático, rastro de tinta e pingo de tinta, cada um com o seu liga/desliga).
Mexer nesses controles mostra o resultado na tela na hora, mas só altera o perfil quando você clica em **Salvar**. O botão fica ativo só quando há alterações.

- Se você trocar de perfil, importar, exportar, fechar a janela ou sair do programa com alterações pendentes, ele pergunta se quer salvar (**Sim**/**Não**/**Cancelar**). **Não** descarta as alterações e volta o visual salvo.
- **Novo...** funciona como "salvar como": cria um perfil com o visual que está na tela e deixa o perfil original como estava salvo.
- **Restaurar padrão** volta os controles para os valores padrão. Clique em **Salvar** para manter.
As opções gerais (efeito ativo, dia e noite, esconder em prints, atalho, iniciar com o Windows) valem para todos os perfis.

Arquivos de configuração antigos (de antes dos perfis) viram automaticamente um perfil chamado "Padrão".

### Dia e noite

Com **Trocar de perfil automaticamente entre dia e noite** marcado, o programa ativa o **Perfil do dia** a partir do horário do dia e o **Perfil da noite** a partir do horário da noite. A noite pode passar da meia-noite (ex.: 19:00 até 07:00).

- A troca acontece ao abrir o programa, na virada de cada período (conferida a cada 30 s), ao voltar da suspensão e quando o relógio do Windows muda.
- Um perfil escolhido à mão (no menu ou na janela) vale até a próxima virada de período.
- Mudar o agendamento (ligar, trocar o perfil ou o horário) aplica o perfil do período atual na hora.
- Se houver alterações não salvas no perfil ativo, a troca espera até você salvar ou descartar.
- Excluir um perfil usado no agendamento faz o horário apontar para o primeiro perfil; renomear acompanha o novo nome.
- No menu da bandeja, **Perfil** mostra qual período e perfil estão agendados agora.

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

- O Windows desenha o cursor numa camada própria, fora do filtro de cor; por isso, sem o "Cursor temático", ele fica com as cores originais. O cursor temático recebe as cores já convertidas pelo perfil.
- O cursor temático troca a seta, a mãozinha e o cursor de texto; os outros (redimensionar, carregando etc.) continuam os do sistema. Os originais voltam ao desligar a opção, desligar o efeito ou sair do programa (e também ao reiniciar ou fazer logoff, caso o programa feche de forma inesperada).
- O brilho por software escurece as cores, mas não a luz de fundo do monitor: não economiza energia, e o preto fica um pouco menos profundo do que baixando o brilho no próprio monitor.
- O filtro de luz azul soma com a "Luz noturna" do Windows se as duas estiverem ligadas; normalmente vale usar só uma.
- O filtro de cor pode ou não aparecer em prints, dependendo da ferramenta de captura usada. A opção de esconder vale só para a textura.
- O Windows só permite um filtro de cor da Magnification por vez: se você usar Lupa/inversão de cores ao mesmo tempo, um sobrescreve o outro.
