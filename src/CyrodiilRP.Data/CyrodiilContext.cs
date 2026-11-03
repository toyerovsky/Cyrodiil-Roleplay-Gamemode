using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data;

public sealed class CyrodiilContext(DbContextOptions<CyrodiilContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<StaffRole> StaffRoles => Set<StaffRole>();

    public DbSet<CharacterCondition> CharacterConditions => Set<CharacterCondition>();

    public DbSet<GoldDelivery> GoldDeliveries => Set<GoldDelivery>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<TraderSpot> TraderSpots => Set<TraderSpot>();

    public DbSet<SpawnPoint> SpawnPoints => Set<SpawnPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts", t => t.HasCheckConstraint("ck_accounts_ban", "(banned_until IS NULL) = (ban_reason IS NULL)"));
            entity.Property(x => x.Locale).HasMaxLength(ColumnLengths.Locale);
            entity.Property(x => x.LastNickname).HasMaxLength(ColumnLengths.Nickname);
            entity.Property(x => x.BanReason).HasMaxLength(ColumnLengths.Reason);
            entity.HasIndex(x => x.ReadyMId).IsUnique();
        });

        modelBuilder.Entity<Character>(entity =>
        {
            entity.ToTable("characters", t => t.HasCheckConstraint("ck_characters_played", "played_seconds >= 0"));
            entity.Property(x => x.FirstName).HasMaxLength(ColumnLengths.NamePart);
            entity.Property(x => x.LastName).HasMaxLength(ColumnLengths.NamePart);
            entity.Property(x => x.NameKey).HasMaxLength(ColumnLengths.NameKey);
            entity.HasIndex(x => x.NameKey).IsUnique();
            entity.HasOne(x => x.Account).WithMany(a => a.Characters).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StaffRole>(entity =>
        {
            entity.ToTable("staff_roles");
            entity.HasKey(x => x.Name);
            entity.Property(x => x.Name).HasMaxLength(ColumnLengths.RoleName);
            entity.Property(x => x.Label).HasMaxLength(ColumnLengths.RoleLabel);
            entity.HasData(StaffRoleSeeds.All);
        });

        modelBuilder.Entity<CharacterCondition>(entity =>
        {
            entity.ToTable("character_conditions");
            entity.HasKey(x => new { x.CharacterId, x.Key });
            entity.Property(x => x.Key).HasMaxLength(ColumnLengths.ConditionKey);
            entity.Property(x => x.ByRef).HasMaxLength(ColumnLengths.Reference);
            entity.Property(x => x.Reason).HasMaxLength(ColumnLengths.Reason);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoldDelivery>(entity =>
        {
            entity.ToTable("gold_deliveries", t => t.HasCheckConstraint("ck_gold_deliveries_amount", "amount > 0"));
            entity.Property(x => x.Reason).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.HasIndex(x => x.CharacterId).HasFilter("delivered_at IS NULL");
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.Property(x => x.Action).HasMaxLength(ColumnLengths.AuditAction);
            entity.Property(x => x.ActorRef).HasMaxLength(ColumnLengths.Reference);
            entity.Property(x => x.TargetRef).HasMaxLength(ColumnLengths.Reference);
            entity.Property(x => x.Details).HasMaxLength(ColumnLengths.AuditDetails);
            entity.HasIndex(x => x.At);
            entity.HasIndex(x => new { x.TargetRef, x.At });
            entity.HasIndex(x => new { x.ActorRef, x.At });
        });

        modelBuilder.Entity<TraderSpot>(entity =>
        {
            entity.ToTable("trader_spots");
            entity.Property(x => x.Shop).HasMaxLength(ColumnLengths.ShopName);
            entity.Property(x => x.Area).HasMaxLength(ColumnLengths.WorldId);
            entity.Property(x => x.Cell).HasMaxLength(ColumnLengths.WorldId);
            entity.Property(x => x.PlacedByRef).HasMaxLength(ColumnLengths.Reference);
        });

        modelBuilder.Entity<SpawnPoint>(entity =>
        {
            entity.ToTable("spawn_point", t => t.HasCheckConstraint("ck_spawn_point_one", $"id = {SpawnPoint.OnlyId}"));
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Area).HasMaxLength(ColumnLengths.WorldId);
            entity.Property(x => x.SetByRef).HasMaxLength(ColumnLengths.Reference);
        });

        SnakeCase.Apply(modelBuilder);

        // "ReadyM" is one word, which the snake case rule cannot know.
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(x => x.ReadyMId).HasColumnName("readym_id");
            entity.HasIndex(x => x.ReadyMId).HasDatabaseName("ix_accounts_readym_id");
        });
    }
}
