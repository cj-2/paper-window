# Guia para estender e depurar

Receitas práticas para as mudanças mais comuns, e armadilhas que já aparecem no código.

## Adicionar uma opção de visual ao perfil

1. **`Profile`** (`Settings.cs`): propriedade com valor padrão.
2. Mesmo arquivo: incluir a propriedade em `CopyLookFrom`, `SameLook` e `Normalize` (faixa/validação). Esquecer um deles quebra, respectivamente, o "Salvar", a detecção de alterações pendentes ou a validação de arquivos importados.
3. **`SettingsForm`**: criar o controle, colocá-lo na aba certa (`Page(...)`), ligar o evento a `EditLook(...)`, preencher o valor em `RefreshControls()` e, se precisar, ajustar `UpdateLabels()` / `UpdateEnabledState()`.
4. **`PaperContext.Apply()`**: ler o valor de `Shown` e aplicá-lo.
5. Documentação: `README.md` da raiz (tabela de opções, seção de perfis) e o documento do assunto em `docs/`.

Opções **gerais** (valem para todos os perfis) vão em `Settings`, não em `Profile`. Usam `Changed(...)` em vez de `EditLook(...)`, e não participam do rascunho.

## Adicionar um efeito de cor, textura ou estilo de cursor

São tabelas estáticas; veja o passo a passo em [visual.md](visual.md) e [mouse.md](mouse.md). Regra comum: **o `Id` é gravado no JSON e nunca deve mudar**. Só o `Name` (texto exibido) pode ser alterado.

## Adicionar uma API do Windows

Declare em `Native.cs` (exceto casos locais como `WindowsTheme`). Se a API muda o estado do sistema, defina **como desfazer** e ligue isso a `ExitThreadCore` e, se o estado sobrevive a uma queda do programa, à rotina de restauração da abertura (como é feito com `CursorTheme.Restore`).

## Adicionar uma camada que desenha na tela

Herde de `LayeredWindow` e use `Present(bitmap, posição)`. Lembre-se de:

- chamar `KeepOnTop()` pelo `_topmostTimer` de `PaperContext`;
- decidir se a camada deve sumir das capturas (`SetCaptureExcluded`) e dar a ela uma opção própria, se fizer sentido;
- liberar a janela em `Dispose` / `ExitThreadCore`.

## Armadilhas conhecidas

- **Eventos disparando ao preencher controles.** Sempre envolva atribuições programáticas com `_loading = true/false` (como `RefreshControls`). Caso contrário, `EditLook` cria um rascunho falso.
- **`Apply()` é o único caminho para a tela.** Não mexa em cursor, overlays ou matriz fora dele.
- **Atualizar a janela de dentro dos próprios eventos.** A troca de perfil pela agenda após uma mudança na janela é *postada* (`SynchronizationContext.Post`) para não chamar `RefreshControls` no meio dos eventos dos controles.
- **Rascunho e agenda.** Com alterações não salvas, a agenda **espera** em vez de trocar de perfil, e tenta de novo no tique seguinte.
- **Nomes de perfil** são comparados sem diferenciar maiúsculas; use `UniqueName`/`RenameProfile` em vez de mexer em `Name` direto.
- **`SetSystemCursor` assume a posse do handle** passado. Não destrua o cursor depois (veja `MakeCursor`).
- **DPI.** Medidas em pixel (largura da tinta, raio do pingo, passo mínimo) são multiplicadas por `DeviceDpi / 96`. Valores novos em pixel devem seguir o mesmo padrão.
- **Botões do mouse trocados.** `GetAsyncKeyState` lê o botão **físico**; `Native.PrimaryButtonDown()` já considera `SystemInformation.MouseButtonsSwapped`. Use-a em vez de ler `VK_LBUTTON` direto.
- **`unsafe`.** `PaperTexture.Create` e `InkLayer.Render` escrevem direto na memória do bitmap com ponteiros. Respeite `Stride` e os limites do bitmap.
- **Alterações no formato do JSON.** Arquivos antigos precisam continuar abrindo. Trate a migração dentro de `Settings.Read` (como já é feito para `HideFromCapture` e para arquivos sem `Profiles`) e documente em [configuracoes.md](configuracoes.md).

## Depuração

- O arquivo de configuração é legível: `%APPDATA%\PaperWindow\settings.json`. Para voltar ao estado inicial, feche o programa e apague-o (na próxima abertura a janela de configurações aparece sozinha).
- **Cursores presos** (programa fechado de forma inesperada): rodar o programa de novo já os restaura; ou abra as propriedades do mouse do Windows e aplique o esquema.
- **Filtro de cor preso:** feche o programa pela bandeja (**Sair**). Se outra ferramenta de Magnification (Lupa, inversão de cores) estiver ativa, ela disputa o mesmo recurso.
- **Atalho não funciona:** a janela de configurações mostra o aviso em vermelho quando a combinação já é usada por outro programa (`HotkeyManager.Failed`).
- **Um exe está em execução e o build falha** por arquivo bloqueado: saia do programa pela bandeja antes de compilar ou publicar.

## Build e publicação

Veja o comando em [windows.md](windows.md). Em resumo: `dotnet publish -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -o publish`; o resultado vai para `publish\PaperWindow.exe`.
