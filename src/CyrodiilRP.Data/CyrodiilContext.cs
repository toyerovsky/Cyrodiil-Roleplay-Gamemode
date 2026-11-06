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

    public DbSet<DiceRoll> DiceRolls => Set<DiceRoll>();

    public DbSet<LoreField> LoreFields => Set<LoreField>();

    public DbSet<LoreIntroduction> LoreIntroductions => Set<LoreIntroduction>();

    public DbSet<Faction> Factions => Set<Faction>();

    public DbSet<FactionRank> FactionRanks => Set<FactionRank>();

    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();

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
            entity.Property(x => x.DiceSet).HasMaxLength(ColumnLengths.DiceSet);
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

        modelBuilder.Entity<DiceRoll>(entity =>
        {
            entity.ToTable("dice_rolls", t => t.HasCheckConstraint("ck_dice_rolls_total", "total > 0"));
            entity.Property(x => x.CharacterName).HasMaxLength(ColumnLengths.NameKey);
            entity.Property(x => x.Asked).HasMaxLength(ColumnLengths.DiceAsked);
            entity.Property(x => x.Dice).HasMaxLength(ColumnLengths.DiceValues);
            entity.Property(x => x.Reason).HasMaxLength(ColumnLengths.DiceReason);
            entity.Property(x => x.Area).HasMaxLength(ColumnLengths.WorldId);
            entity.HasIndex(x => new { x.CharacterId, x.Id });
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoreField>(entity =>
        {
            entity.ToTable("lore_fields", t => t.HasCheckConstraint("ck_lore_fields_number", "number IS NULL OR number BETWEEN 5 AND 100"));
            entity.HasKey(x => new { x.CharacterId, x.FieldId });
            entity.Property(x => x.FieldId).HasMaxLength(ColumnLengths.LoreFieldId);
            entity.Property(x => x.CatalogId).HasMaxLength(ColumnLengths.LoreCatalogId);
            entity.Property(x => x.Text).HasMaxLength(ColumnLengths.LoreText);
            entity.Property(x => x.Rolls).HasMaxLength(ColumnLengths.LoreRolls);
            entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoreIntroduction>(entity =>
        {
            entity.ToTable("lore_introductions", t => t.HasCheckConstraint("ck_lore_introductions_other", "knower_character_id <> known_character_id"));
            entity.HasKey(x => new { x.KnowerCharacterId, x.KnownCharacterId });
            entity.HasIndex(x => x.KnownCharacterId);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.KnowerCharacterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.KnownCharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Faction>(entity =>
        {
            entity.ToTable("factions");
            entity.Property(x => x.Name).HasMaxLength(ColumnLengths.FactionName);
            entity.Property(x => x.NameKey).HasMaxLength(ColumnLengths.FactionName);
            entity.HasIndex(x => x.NameKey).IsUnique();
        });

        modelBuilder.Entity<FactionRank>(entity =>
        {
            entity.ToTable("faction_ranks", t =>
            {
                t.HasCheckConstraint("ck_faction_ranks_number", "number > 0");
                t.HasCheckConstraint("ck_faction_ranks_position", "position >= 0");
            });
            entity.HasKey(x => new { x.FactionId, x.Number });
            entity.Property(x => x.Name).HasMaxLength(ColumnLengths.FactionRankName);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.PrimitiveCollection(x => x.Permissions).ElementType().HasConversion<string>();
            entity.HasOne<Faction>().WithMany().HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade);
        });

        // A rank goes only once nobody holds it: FactionBook moves its members first.
        modelBuilder.Entity<FactionMember>(entity =>
        {
            entity.ToTable("faction_members");
            entity.HasKey(x => x.CharacterId);
            entity.Property(x => x.CharacterId).ValueGeneratedNever();
            entity.HasOne<Character>().WithOne().HasForeignKey<FactionMember>(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Faction>().WithMany().HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<FactionRank>().WithMany().HasForeignKey(x => new { x.FactionId, x.RankNumber }).OnDelete(DeleteBehavior.NoAction);
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
