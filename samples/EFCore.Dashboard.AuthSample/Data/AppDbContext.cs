using EFCore.Dashboard.AuthSample.Models;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Dashboard.AuthSample.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
}
