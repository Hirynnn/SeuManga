using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace SeuManga.Infrastructure.Persistence;
public sealed class SeuMangaDbContextFactory : IDesignTimeDbContextFactory<SeuMangaDbContext>
{
    public SeuMangaDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SeuMangaDbContext>();
        builder.UseSqlite("Data Source=seumanga-design.db");
        return new SeuMangaDbContext(builder.Options);
    }
}
