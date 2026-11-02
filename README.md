# SeuManga

O SeuManga é um aplicativo desktop para Windows que reúne descoberta e leitura de mangás, acompanhamento de animes e organização de uma biblioteca pessoal em um só lugar. A interface em português usa tema escuro, navegação lateral e páginas integradas para catálogo, detalhes, leitura, biblioteca, histórico, favoritos e configurações.

O aplicativo consulta o catálogo de mangás da MangaDex e o catálogo de animes do Jikan. Seus dados pessoais — biblioteca, favoritos, progresso e histórico — ficam em um banco SQLite local. O acesso à internet é necessário para pesquisar catálogos, capas, páginas e informações complementares; as funções de organização local continuam disponíveis sem rede.

Esta versão é **0.2.0**. Consulte o [histórico de releases](https://github.com/Hirynnn/SeuManga/releases) para baixar instalador ou pacote portátil.

## O que já funciona

- Busca MangaDex paginada com filtros de status, idiomas, tags, autor/artista, classificação indicativa e ordenação, além de capas, detalhes e capítulos.
- Detalhes do mangá mostram as traduções disponíveis; escolha o idioma dos capítulos antes de abrir o leitor. O app prioriza português quando existe e, caso contrário, seleciona uma tradução disponível.
- Leitor de páginas MangaDex@Home com modos de página individual/dupla ou rolagem contínua com carregamento sob demanda, navegação por página/capítulo, seletor e direção de leitura, ajuste de imagem, zoom, tela cheia, atalhos, prefetch limitado e salvamento do progresso.
- Biblioteca local de mangás e animes com status, favoritos, progresso, histórico de leitura e listas personalizadas mistas.
- Busca global de mangás/animes com sugestões das buscas recentes na sessão; busca Jikan para animes, detalhes, recomendações e lista completa de episódios; episódios assistidos ficam salvos no dispositivo.
- Integrações opcionais TMDB e Watchmode; o TMDB complementa temporadas e episódios. Chaves salvas com DPAPI no perfil Windows.
- Onde assistir usa somente disponibilidade retornada pelas fontes e explicita quando a cobertura do Brasil não foi confirmada.
- Configurações de chaves de API e backup/restauração do banco local, sem incluir os segredos protegidos no arquivo de backup.
- Tema escuro/roxo com navegação lateral agrupada, catálogo e filtros em painéis redesenhados, cache temporário de metadados e logs rotativos.

Consulte [`docs/ROADMAP.md`](docs/ROADMAP.md) para os recursos da especificação que continuam pendentes. A interface só habilita fluxos conectados a uma implementação real.

## Requisitos

- Windows 10 ou 11 x64.
- SDK .NET 10 para desenvolvimento.
- Runtime Evergreen do Microsoft Edge WebView2 para leitura integrada.
- Acesso à internet para catálogos e provedores externos. Biblioteca, histórico e listas usam o banco local.

O catálogo e as imagens da MangaDex usam o Microsoft Edge WebView2 para conexões HTTPS compatíveis com Windows 10 e 11. A validação de certificados permanece habilitada.

## Executar

```powershell
dotnet restore SeuManga.sln
dotnet build SeuManga.sln
dotnet run --project src/SeuManga.Desktop/SeuManga.Desktop.csproj
```

O banco e os logs são criados em `%APPDATA%\SeuManga`. `SEUMANGA_DATA_DIR` pode apontar para uma pasta alternativa, por exemplo em testes. As migrations do EF Core são aplicadas na inicialização.

## Baixar para Windows

Baixe a versão mais recente na página de [Releases do SeuManga](https://github.com/Hirynnn/SeuManga/releases/latest):

- **[SeuManga-0.2.0-win-x64.exe](https://github.com/Hirynnn/SeuManga/releases/download/v0.2.0/SeuManga-0.2.0-win-x64.exe)**: aplicativo x64 em um único executável, com o runtime .NET incluído. Execute o arquivo após baixar.
- **[SeuManga-0.2.0-win-x64-portable.zip](https://github.com/Hirynnn/SeuManga/releases/download/v0.2.0/SeuManga-0.2.0-win-x64-portable.zip)**: a mesma versão em ZIP; extraia e execute `SeuManga.exe`.

Ambas as opções ainda precisam do Runtime Evergreen do Microsoft Edge WebView2 para o leitor integrado. O instalador WebView2 pode ser obtido no [site oficial da Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/).

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
