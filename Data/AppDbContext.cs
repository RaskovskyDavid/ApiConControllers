using ApiConControllers.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace ApiConControllers.Data;
// IdentityDbContext brings in all the Identity tables (users, roles, claims, etc.).
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Always call base: it configures the Identity tables.
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            // We look tokens up by hash on every refresh, so index it (and keep it unique).
            entity.HasIndex(t => t.TokenHash).IsUnique();

            // Deleting a user deletes their refresh tokens.
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
