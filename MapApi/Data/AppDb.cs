using Microsoft.EntityFrameworkCore;
using MapApi.Models;
namespace MapApi.Data
{
    public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
    {
        public DbSet<Poi> Pois => Set<Poi>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Poi>().HasKey(x => x.Id);
            b.Entity<Poi>().HasIndex(x => new { x.IsActive, x.Priority });
        }
    }
}