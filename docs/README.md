# Documentação técnica — Paper Window

Esta pasta explica **como o programa funciona por dentro**, para quem desenvolve nele. O `README.md` da raiz é o manual do usuário (o que cada opção faz); aqui o foco é o código: quem faz o quê, como as partes conversam e por que certas decisões foram tomadas.

## Índice

| Documento | Assunto |
|---|---|
| [arquitetura.md](arquitetura.md) | Visão geral, mapa de arquivos, ciclo de vida e fluxo de dados |
| [visual.md](visual.md) | Filtro de cor (Magnification), textura de papel e janelas sobrepostas |
| [mouse.md](mouse.md) | Cursor temático, rastro de tinta e pingos de tinta |
| [configuracoes.md](configuracoes.md) | Modelo de dados, perfis, rascunho, agenda dia/noite, importar/exportar e janela de configurações |
| [camadas-e-captura.md](camadas-e-captura.md) | Threads e timers, as camadas de tela, por que cada uma aparece (ou não) em capturas, o que é desfeito ao sair |
| [windows.md](windows.md) | Integração com o Windows: P/Invoke, atalho global, registro, captura de tela |
| [estendendo.md](estendendo.md) | Receitas para adicionar opções, efeitos e camadas; armadilhas e depuração |

## Resumo em um parágrafo

Paper Window é um app de bandeja (WinForms, .NET 8) que deixa a tela com cara de papel/e-ink. Faz isso com três camadas independentes: um **filtro de cor** de tela cheia (API de Magnification do Windows), uma **textura** desenhada numa janela transparente que não captura cliques, e **efeitos de mouse** (cursor desenhado por código, rastro e pingos de tinta). Tudo é controlado por **perfis** salvos em JSON, com troca automática entre dia e noite.

## Como manter esta documentação

Estas páginas devem acompanhar o código. Ao mudar o comportamento, a estrutura ou uma decisão de design, atualize o documento correspondente **na mesma alteração**. A regra completa está no `AGENTS.md`.
