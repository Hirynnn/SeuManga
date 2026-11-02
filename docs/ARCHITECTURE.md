# Arquitetura

A solução segue separação em quatro projetos. As dependências apontam para dentro: Desktop usa Application e Infrastructure; Infrastructure implementa contratos de Application e usa entidades Domain; Domain não depende de UI ou provedores.

## Inicialização e composição

`App` cria o Generic Host, registra serviços e clientes HTTP, configura Serilog, inicializa o SQLite via migrations e então abre a janela principal. O aplicativo não chama APIs para iniciar. `SEUMANGA_DATA_DIR` permite substituir a pasta de dados padrão durante execução controlada.

## Fluxos

- `MangaCatalogService` valida e encaminha pesquisa, detalhes, capítulos e páginas ao contrato `IMangaCatalogProvider`; `MangaDexClient` implementa as consultas.
- `MangaDexImageClient` baixa uma página de cada vez, exige HTTPS e hosts MangaDex permitidos, limita tamanho e valida `Content-Type`. O WebView2 recebe somente o conteúdo local em `data:`; scripts e navegação externa ficam desabilitados.
- `AnimeCatalogService` agrega Jikan, enriquecimento TMDB e disponibilidade Watchmode. Falhas dos provedores complementares não removem os dados principais Jikan.
- `LibraryRepository` persiste dados pessoais no SQLite. Serviços de biblioteca e acompanhamento não dependem de rede.
- `DpapiSettingsStore` protege os segredos com DPAPI `CurrentUser`.

## Regras para provedores

- A busca Jikan é serializada e espaçada em 1,15 s para respeitar o limite documentado.
- A pesquisa MangaDex usa cache em memória de curta duração; URLs do MangaDex@Home não são persistidas.
- Séries TMDB só enriquecem o anime quando o resultado tem origem japonesa, gênero de animação e correspondência de título.
- Disponibilidade de streaming só é exibida a partir de respostas regionais confirmadas; o link e a atribuição da fonte acompanham os resultados.
- AniList permanece desativado.
