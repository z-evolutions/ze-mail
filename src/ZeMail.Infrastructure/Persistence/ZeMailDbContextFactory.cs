using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZeMail.Infrastructure.Persistence;

public class ZeMailDbContextFactory : IDesignTimeDbContextFactory<ZeMailDbContext>
{
    public ZeMailDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ZeMailDbContext>()
            .UseSqlite("Data Source=zemail-dev.db")
            .Options;

        return new ZeMailDbContext(options);
    }
}