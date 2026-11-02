# SeuManga

Aplicativo desktop Windows para descobrir, ler e organizar mangás e acompanhar animes. O projeto usa C#, .NET 10, WPF, MVVM, SQLite e integrações diretas com MangaDex e Jikan. Todas as áreas do aplicativo, incluindo detalhes e leitor, são navegadas dentro de uma única janela principal.

## O que já funciona

- Busca MangaDex paginada com filtros de status, idiomas, tags, autor/artista, classificação indicativa e ordenação, além de capas, detalhes e capítulos.
- Leitor WebView2 para páginas MangaDex@Home, com modos de página individual/dupla, navegação por página/capítulo, seletor e direção de leitura, ajuste de imagem, zoom, tela cheia, atalhos, filtro básico de anúncios, prefetch limitado e salvamento do progresso.
- Biblioteca local de mangás e animes com status, favoritos, progresso, histórico de leitura e listas personalizadas mistas.
- Busca global de mangás/animes com sugestões das buscas recentes na sessão; busca Jikan para animes, detalhes, recomendações e lista completa de episódios; episódios assistidos ficam salvos no dispositivo.
- Integrações opcionais TMDB e Watchmode; o TMDB complementa temporadas e episódios. Chaves salvas com DPAPI no perfil Windows.
- Onde assistir usa somente disponibilidade retornada pelas fontes e explicita quando a cobertura do Brasil não foi confirmada.
- Configurações de chaves de API e backup/restauração do banco local, sem incluir os segredos protegidos no arquivo de backup.
- Tema escuro/roxo, cache temporário de metadados e logs rotativos.

Consulte [`docs/ROADMAP.md`](docs/ROADMAP.md) para os recursos da especificação que continuam pendentes. A interface só habilita fluxos conectados a uma implementação real.

## Requisitos

- Windows 10 ou 11 x64.
- SDK .NET 10 para desenvolvimento.
- Runtime Evergreen do Microsoft Edge WebView2 para leitura integrada.
- Acesso à internet para catálogos e provedores externos. Biblioteca, histórico e listas usam o banco local.

## Executar

```powershell
dotnet restore SeuManga.sln
dotnet build SeuManga.sln
dotnet run --project src/SeuManga.Desktop/SeuManga.Desktop.csproj
```

O banco e os logs são criados em `%APPDATA%\SeuManga`. `SEUMANGA_DATA_DIR` pode apontar para uma pasta alternativa, por exemplo em testes. As migrations do EF Core são aplicadas na inicialização.

## Baixar para Windows

Baixe a versão mais recente na página de [Releases do SeuManga](https://github.com/Hirynnn/SeuManga/releases/latest):

- **SeuManga-Setup-0.1.0-win-x64.exe**: instalador com atalho no Menu Iniciar; requer o Microsoft Edge WebView2 Runtime.
- **SeuManga-0.1.0-win-x64-portable.zip**: pacote portátil completo; extraia todos os arquivos e execute `SeuManga.exe`. Também requer o WebView2 Runtime.

## Testes

```powershell
dotnet test SeuManga.sln
```

Os testes usam respostas simuladas para as APIs e SQLite temporário para verificar persistência sem depender de serviços externos.

## APIs opcionais

Configure TMDB e Watchmode em **Configurações**. O app guarda os segredos criptografados com DPAPI para o usuário atual do Windows; não use os arquivos de configuração para compartilhar essas chaves. Veja [`docs/API_INTEGRATIONS.md`](docs/API_INTEGRATIONS.md) para cobertura, atribuição e limitações.

## Estrutura

- `src/SeuManga.Desktop`: WPF, Views, ViewModels, navegação e inicialização.
- `src/SeuManga.Domain`: entidades e enums.
- `src/SeuManga.Application`: contratos e serviços de aplicação.
- `src/SeuManga.Infrastructure`: MangaDex, Jikan, TMDB, Watchmode, DPAPI, EF Core e SQLite.
- `tests/SeuManga.Application.Tests`: testes de serviços, clientes e persistência.
- `installer/SeuManga.iss`: script Inno Setup para Windows x64.

Mais detalhes: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`docs/DATABASE.md`](docs/DATABASE.md) e [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).
