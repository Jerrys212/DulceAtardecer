using Microsoft.EntityFrameworkCore;

namespace DulceAtardecer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
