using Microsoft.EntityFrameworkCore;

namespace PetAdoption.Api;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<AdoptionRequest> AdoptionRequests => Set<AdoptionRequest>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Pet>().Property(p => p.Status).HasConversion<string>();
        b.Entity<AdoptionRequest>()
            .HasOne(r => r.Pet)
            .WithMany(p => p.Requests)
            .HasForeignKey(r => r.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public static class DbPaths
{
    public static string File(IConfiguration config) => Path.GetFullPath(config["DbPath"] ?? "petadoption.db");
}
