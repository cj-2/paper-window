# Regras para agentes neste projeto

Estas regras valem para qualquer agente de IA que trabalhe neste repositório (Claude, Codex, Copilot etc.).

## Manter a documentação em `docs/`

A pasta `docs/` explica o projeto para o desenvolvedor: como o sistema funciona e o que cada parte faz (comece por `docs/README.md`, que tem o índice). O `README.md` da raiz continua sendo o manual do usuário.

- **Ao implementar ou alterar algo, atualize a documentação na mesma alteração.** Isso vale para novos arquivos/classes, mudanças de comportamento, novas opções de configuração, mudanças no formato do `settings.json`, novas APIs do Windows e decisões de design.
- Atualize o documento do assunto em vez de criar um novo; crie um documento novo (e adicione ao índice em `docs/README.md`) só para um assunto que ainda não tem lugar.
- Explique o **como** e o **porquê** (fluxo, decisões, armadilhas), não repita o código linha a linha. O público é um desenvolvedor que não acompanhou a implementação.
- Se uma alteração tornar um trecho da documentação falso, corrija-o; documentação desatualizada é pior que nenhuma.
- Escreva em português, como o restante do projeto. Mudanças visíveis ao usuário também vão para o `README.md` da raiz.
- Alterações só na documentação não exigem build nem publicação.

## Só implementar quando for pedido

- **Implemente apenas quando o usuário pedir explicitamente** ("implementa", "faz", "cria", "corrige", "aplica" etc.).
- **Perguntas não são pedidos de implementação.** Diante de uma pergunta ("dá para...?", "como funciona...?", "por que...?", "o que acha de...?"), responda e, se fizer sentido, proponha o que poderia ser feito. **Não altere arquivos, não rode builds que mudem o estado e não execute a implementação** até o usuário confirmar.
- Na dúvida se a mensagem é um pedido ou uma pergunta, trate como pergunta e pergunte antes de agir.
