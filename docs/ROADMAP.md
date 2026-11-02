# Estado das fases

Este documento separa funcionalidades implementadas de itens ainda necessários para atingir a especificação integral.

## Implementado

- Fundação WPF/.NET 10, projetos em camadas, DI/Host, MVVM básico, EF Core SQLite, migrations, logs Serilog, tema inicial.
- Navegação de catálogo, detalhes, biblioteca, listas, histórico, configurações, pesquisa global e leitor integrada em uma janela principal com voltar, seção ativa e estilo visual unificado.
- MangaDex: busca paginada por título com status, idioma original e traduzido, tags inclusivas/exclusivas, autor/artista por nome, classificação de conteúdo e ordenação; detalhes, capas, capítulos pt-BR e deduplicação por volume/número.
- Leitor: WebView2, página única e dupla página, navegação de páginas/capítulos, seletor de página, direção de leitura, ajuste à largura/altura/área, zoom, tela cheia, atalhos, progresso local, prefetch limitado com cache em memória, validação de imagens/origens e bloqueio básico de anúncios.
- Biblioteca local: mangás com status/favoritos e progresso; animes com status, favoritos e progresso; listas mistas manga/anime com migração preservando dados antigos.
- Jikan: busca, detalhes, recomendações, episódios paginados até o fim e acompanhamento local de vistos, respeitando intervalo entre chamadas.
- TMDB: correspondência conservadora de séries japonesas animadas, metadados e provedores regionais; temporadas e detalhes de episódios pt-BR carregados sob demanda.
- Watchmode: busca e sources para BR, com tratamento de plano sem cobertura.
- Pesquisa global manga/anime com debounce, cancelamento e sugestões de pesquisas recentes durante a sessão; tela inicial com leituras recentes, progresso dos animes acompanhados, populares Jikan e recomendações Jikan baseadas na biblioteca; configurações de chaves DPAPI, backup/restauração do banco, cache de metadados em memória, documentação, testes, publicação self-contained e instalador Inno Setup compilado e validado por instalação, inicialização e desinstalação locais.

## Ainda pendente

- Leitor: modo vertical de rolagem contínua e cache de imagem em disco (o prefetch atual é limitado às próximas páginas e o cache fica na memória).

- Recomendações MangaDex/TMDB e correspondência persistida genérica entre provedores (atualmente só a correspondência Anime Jikan–TMDB é persistida).
- Configurações de aparência/leitura/idiomas e limpeza/limite do cache ainda faltam.
- Testes adicionais para fluxos de erro e regras do banco; validação visual/interativa do leitor, que depende do WebView2 Runtime.
- Instalação/atualização em máquina limpa, preservação de dados durante atualização e validação do fluxo em máquina sem WebView2 Runtime.

As pendências são necessárias para concluir as oito fases; os arquivos de preparação de release não significam que o instalador já foi compilado ou validado.





