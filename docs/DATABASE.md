# Banco local

O SQLite fica em `%APPDATA%\SeuManga\seumanga.db` por padrão. A pasta pode ser substituída por `SEUMANGA_DATA_DIR`. EF Core executa migrations na inicialização; arquivos anteriores a migrations devem ser copiados antes de atualização manual.

## Entidades atuais

- `Library`: títulos MangaDex salvos, status, favorito e último capítulo/página.
- `History`: ponto de leitura mais recente por manga e capítulo.
- `CustomLists` e `CustomListItems`: listas pessoais mistas de mangás e animes, com unicidade por lista, tipo de mídia e ID. Migration converte itens antigos para tipo mangá e preserva seus IDs.
- `AnimeLibrary`: séries Jikan adicionadas à biblioteca, com status/favorito.
- `WatchedEpisodes`: episódios marcados como assistidos.
- `__EFMigrationsHistory`: versão do esquema aplicado.

Chaves únicas impedem títulos duplicados por ID externo e itens duplicados numa lista. O histórico exibido é limitado aos 500 registros mais recentes.

## Backup e restauração

Em **Configurações > Armazenamento local**, crie um backup consistente do banco SQLite mesmo com o aplicativo aberto ou restaure um arquivo de backup do SeuManga. A restauração verifica a integridade e o esquema, aplica migrations pendentes e cria uma cópia temporária do banco atual para recuperação em caso de falha.

Os backups incluem biblioteca, listas, histórico, progresso e identificadores externos. Eles não incluem `api-secrets.json`: as chaves protegidas por DPAPI são vinculadas ao usuário do Windows que as salvou e devem ser configuradas separadamente em outro perfil.
