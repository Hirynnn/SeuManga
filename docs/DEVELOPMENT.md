# Desenvolvimento

## Requisitos

Windows x64, .NET 10 SDK, WebView2 Runtime para validar o leitor e acesso NuGet na primeira restauração.

## Comandos

```powershell
dotnet restore SeuManga.sln
dotnet build SeuManga.sln -c Release
dotnet test SeuManga.sln -c Release
dotnet run --project src/SeuManga.Desktop/SeuManga.Desktop.csproj
```

Para isolar o banco durante um smoke test: `$env:SEUMANGA_DATA_DIR = Join-Path $env:TEMP 'SeuManga-Test'` antes de executar.

## Publicar Windows x64

```powershell
dotnet publish src/SeuManga.Desktop/SeuManga.Desktop.csproj -c Release -p:PublishProfile=win-x64
```

A publicação é self-contained .NET, mas usa o Runtime Evergreen WebView2 instalado no Windows. Compile `installer/SeuManga.iss` com Inno Setup depois de publicar. O instalador verifica o runtime e encaminha o usuário ao instalador oficial se estiver ausente. Dados pessoais ficam em AppData e não são removidos por atualização normal.

## Estado de validação

`dotnet build` e `dotnet test` fazem parte da validação local. Os testes não chamam APIs reais; verifique MangaDex, Jikan, TMDB e Watchmode em uma sessão online antes de cada distribuição. Não coloque chaves nos comandos de shell ou no repositório.
