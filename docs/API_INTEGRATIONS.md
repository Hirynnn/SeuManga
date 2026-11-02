# Integrações externas

## MangaDex

- API oficial: https://api.mangadex.org/docs/
- Cliente: `MangaDexClient`; leitura de imagens: `MangaDexImageClient`.
- Fluxos usados: `/manga`, `/manga/{id}`, `/manga/{id}/feed` e `/at-home/server/{chapterId}`.
- Busca usa os filtros oficiais de idioma original, idioma com capítulos disponíveis, status, tags, autor/artista, classificação e ordenação. Tags/autor/artista são resolvidos por nome via endpoints MangaDex e mantidos em cache curto; conteúdo erótico e explícito fica desativado por padrão.
- O endpoint MangaDex@Home entrega URLs temporárias por capítulo. O app busca essas URLs ao iniciar cada capítulo e não as armazena como permanentes.
- Imagens são baixadas por HTTPS com validação do domínio e tamanho. O WebView2 do leitor também bloqueia requisições a uma lista local de domínios de anúncios e rastreadores, usando o evento WebResourceRequested; não é uma extensão completa de listas EasyList. A leitura continua sujeita a disponibilidade, acesso e regras da MangaDex; capítulos restritos não são contornados.
- Capítulos repetidos por volume/número são agrupados, e a interface mostra o grupo quando retornado.

## Jikan

- API: https://docs.api.jikan.moe/ (v4 em `https://api.jikan.moe/v4`).
- Cliente: `JikanClient`.
- Busca, detalhes e episódios são consultados sem chave. O acesso é serializado a cada 1,15 s para ficar abaixo do limite publicado de 3 chamadas/s e 60/min. Respostas podem ser incompletas ou temporariamente limitadas.

## TMDB

- Referência: https://developer.themoviedb.org/reference/intro/getting-started
- A chave é opcional e enviada via Bearer Authorization. A correspondência exige país de origem JP, gênero TV 16 (animação) e título compatível.
- O app usa dados pt-BR quando disponíveis; resultados de busca não confirmados como anime são ignorados. Detalhes de temporadas e episódios são carregados quando o usuário solicita, com datas, resumos, imagens e duração somente quando enviados pelo TMDB.
- Disponibilidade de TV vem do endpoint de provedores do TMDB, associado ao JustWatch. A tela identifica JustWatch como fonte. TMDB exige atribuição e uso não comercial no plano de desenvolvedor; verifique os termos atuais antes de distribuir.
- A chave é protegida por DPAPI `CurrentUser` em `api-secrets.json`.

## Watchmode

- API: https://api.watchmode.com/
- Chave opcional; pesquisa e disponibilidade usam os endpoints de busca e sources.
- Disponibilidade é solicitada com região `BR`. Se o serviço negar a cobertura regional, o app informa que o plano não confirmou o Brasil; uma lista vazia não é apresentada como disponibilidade.
- O plano Developer é não comercial, exige atribuição e cobre até três países configurados. Planos e limites variam; confira a conta e preços atuais antes do uso.
- Watchmode não fornece reprodução de vídeo. Links apontam para os destinos disponibilizados pelo serviço.

## AniList

Desativado até revisão de termos e autorização aplicável.

## Licenças e cache

Reveja os termos vigentes de cada provedor antes de distribuir ou usar comercialmente. O cache MangaDex é apenas em memória e tem expiração curta. O app não grava as URLs temporárias de leitura.



