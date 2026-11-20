using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data;

public sealed class CyrodiilContext(DbContextOptions<CyrodiilContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountGuide> AccountGuides => Set<AccountGuide>();

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

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    public DbSet<CharacterHorse> CharacterHorses => Set<CharacterHorse>();

    public DbSet<CharacterSpecialization> CharacterSpecializations => Set<CharacterSpecialization>();

    public DbSet<CharacterCapOverride> CharacterCapOverrides => Set<CharacterCapOverride>();

    public DbSet<NoticeBoard> NoticeBoards => Set<NoticeBoard>();

    public DbSet<Notice> Notices => Set<Notice>();

    public DbSet<GamblingTable> GamblingTables => Set<GamblingTable>();

    public DbSet<GamblingGame> GamblingGames => Set<GamblingGame>();

    public DbSet<AuctionLot> AuctionLots => Set<AuctionLot>();

    public DbSet<AuctionParcel> AuctionParcels => Set<AuctionParcel>();

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

        modelBuilder.Entity<AccountGuide>(entity =>
        {
            entity.ToTable("account_guides");
            entity.HasKey(x => new { x.AccountId, x.Topic });
            entity.Property(x => x.Topic).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("bank_accounts", t => t.HasCheckConstraint("ck_bank_accounts_balance", "balance >= 0"));
            entity.HasKey(x => x.CharacterId);
            entity.Property(x => x.CharacterId).ValueGeneratedNever();
            entity.HasOne<Character>().WithOne().HasForeignKey<BankAccount>(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterHorse>(entity =>
        {
            entity.ToTable("character_horses", t => t.HasCheckConstraint("ck_character_horses_price", "price >= 0"));
            entity.HasKey(x => x.CharacterId);
            entity.Property(x => x.CharacterId).ValueGeneratedNever();
            entity.Property(x => x.Horse).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.HasOne<Character>().WithOne().HasForeignKey<CharacterHorse>(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterSpecialization>(entity =>
        {
            entity.ToTable("character_specializations", t =>
            {
                t.HasCheckConstraint("ck_character_specializations_price", "price >= 0");
                t.HasCheckConstraint("ck_character_specializations_changes", "changes >= 0");
            });
            entity.HasKey(x => x.CharacterId);
            entity.Property(x => x.CharacterId).ValueGeneratedNever();
            entity.Property(x => x.Specialization).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.HasOne<Character>().WithOne().HasForeignKey<CharacterSpecialization>(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterCapOverride>(entity =>
        {
            entity.ToTable("character_cap_overrides", t => t.HasCheckConstraint("ck_character_cap_overrides_cap", "cap >= 0 AND cap <= 100"));
            entity.HasKey(x => new { x.CharacterId, x.Stat });
            entity.Property(x => x.Stat).HasMaxLength(ColumnLengths.EnumName);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NoticeBoard>(entity =>
        {
            entity.ToTable("notice_boards");
            entity.Property(x => x.Area).HasMaxLength(ColumnLengths.WorldId);
            entity.Property(x => x.Cell).HasMaxLength(ColumnLengths.WorldId);
            entity.Property(x => x.PlacedByRef).HasMaxLength(ColumnLengths.Reference);
        });

        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("notices", t =>
            {
                t.HasCheckConstraint("ck_notices_text", "length(text) > 0");
                t.HasCheckConstraint("ck_notices_expiry", "expires_at > posted_at");
            });
            entity.Property(x => x.Text).HasMaxLength(ColumnLengths.NoticeText);
            entity.HasIndex(x => x.BoardId);
            entity.HasIndex(x => x.CharacterId);
            entity.HasOne<NoticeBoard>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GamblingTable>(entity =>
        {
            entity.ToTable("gambling_tables");
            entity.Property(x => x.Area).HasMaxLength(ColumnLengths.WorldId);
            entity.Property(x => x.Cell).HasMaxLength(ColumnLengths.WorldId);
            entity.Property(x => x.PlacedByRef).HasMaxLength(ColumnLengths.Reference);
        });

        modelBuilder.Entity<GamblingGame>(entity =>
        {
            entity.ToTable("gambling_games", table =>
            {
                table.HasCheckConstraint("ck_gambling_games_players", "first_character_id <> second_character_id");
                table.HasCheckConstraint("ck_gambling_games_gold", "stake > 0 AND first_raises >= 0 AND second_raises >= 0 AND pot::bigint = 2::bigint * stake + first_raises + second_raises AND cut >= 0 AND cut <= pot");
                table.HasCheckConstraint("ck_gambling_games_winner", "winner_character_id IS NULL OR winner_character_id IN (first_character_id, second_character_id)");
                table.HasCheckConstraint("ck_gambling_games_status", "(status = 'Open' AND ended_at IS NULL AND winner_character_id IS NULL AND cut = 0) OR (status = 'Settled' AND ended_at IS NOT NULL AND winner_character_id IS NOT NULL) OR (status IN ('Refunded', 'Cancelled') AND ended_at IS NOT NULL AND winner_character_id IS NULL AND cut = 0)");
            });
            entity.Property(x => x.Mode).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.Property(x => x.FirstName).HasMaxLength(ColumnLengths.NameKey);
            entity.Property(x => x.SecondName).HasMaxLength(ColumnLengths.NameKey);
            entity.Property(x => x.Rounds).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.FirstCharacterId, x.Id });
            entity.HasIndex(x => new { x.SecondCharacterId, x.Id });
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.OperationId).IsUnique();
            entity.HasOne<GamblingTable>().WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.FirstCharacterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.SecondCharacterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuctionLot>(entity =>
        {
            entity.ToTable("auction_lots", table =>
            {
                table.HasCheckConstraint("ck_auction_lots_item", "count > 0");
                table.HasCheckConstraint("ck_auction_lots_prices", "(start_price IS NOT NULL OR buyout_price IS NOT NULL) AND (start_price IS NULL OR start_price > 0) AND (buyout_price IS NULL OR buyout_price > 0) AND (start_price IS NULL OR buyout_price IS NULL OR buyout_price > start_price) AND fee >= 0");
                table.HasCheckConstraint("ck_auction_lots_bid", "bids >= 0 AND (top_bid IS NULL) = (top_bidder_character_id IS NULL) AND (top_bid IS NULL) = (bids = 0) AND (top_bid IS NULL OR (start_price IS NOT NULL AND top_bid >= start_price AND (buyout_price IS NULL OR top_bid < buyout_price))) AND (top_bidder_character_id IS NULL OR top_bidder_character_id <> seller_character_id)");
                table.HasCheckConstraint("ck_auction_lots_status", "(status = 'Open' AND closed_at IS NULL AND buyer_character_id IS NULL AND price IS NULL AND cut = 0) OR (status = 'Sold' AND closed_at IS NOT NULL AND buyer_character_id IS NOT NULL AND buyer_character_id <> seller_character_id AND price > 0 AND cut >= 0 AND cut <= price) OR (status IN ('Expired', 'Withdrawn', 'Removed') AND closed_at IS NOT NULL AND buyer_character_id IS NULL AND price IS NULL AND cut = 0)");
            });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.Property(x => x.SellerName).HasMaxLength(ColumnLengths.NameKey);
            entity.Property(x => x.ItemName).HasMaxLength(ColumnLengths.ItemName);
            entity.Property(x => x.ItemType).HasMaxLength(ColumnLengths.RecordType);
            entity.HasIndex(x => new { x.Status, x.EndsAt });
            entity.HasIndex(x => new { x.SellerCharacterId, x.Status });
            entity.HasIndex(x => x.TopBidderCharacterId);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.SellerCharacterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.TopBidderCharacterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.BuyerCharacterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuctionParcel>(entity =>
        {
            entity.ToTable("auction_parcels", table => table.HasCheckConstraint("ck_auction_parcels_gold",
                "(reason IN ('Sale', 'Outbid', 'Refund') AND gold > 0) OR (reason IN ('Won', 'Bought', 'Unsold', 'Withdrawn', 'Removed') AND gold = 0)"));
            entity.Property(x => x.Reason).HasConversion<string>().HasMaxLength(ColumnLengths.EnumName);
            entity.HasIndex(x => x.CharacterId).HasFilter("collected_at IS NULL");
            entity.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AuctionLot>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
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
