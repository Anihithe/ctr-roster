using CtrRoster.Application.Availabilities.Commands;
using CtrRoster.Application.Sessions.Commands;
using CtrRoster.Application.Tables.Commands;
using CtrRoster.Domain.Entities;
using CtrRoster.Domain.Enums;
using CtrRoster.Domain.Exceptions;
using CtrRoster.Domain.Tests.TestHelpers;
using CtrRoster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CtrRoster.Domain.Tests;

public class FunctionalScenarioTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly TestMessageRenderer _renderer;

    public FunctionalScenarioTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
        AppDbContext.ConfigureSqlitePragmas(_connection, isInMemory: true);

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();

        _renderer = new TestMessageRenderer();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task GrandSlam_CompleteGameNightUserJourney_ShouldEnforceAllInvariantsEndToEnd()
    {
        using var db = new AppDbContext(_options);

        // 1. Initialisation : Configuration de la guilde avec limite de 2 tables max dans la salle
        const ulong guildId = 42001;
        const ulong channelId = 84001;

        var guildConfig = new GuildConfig
        {
            GuildId = guildId,
            DefaultMaxTables = 2,
            AutoRenewSessions = true,
            RenewIntervalDays = 7
        };
        db.GuildConfigs.Add(guildConfig);
        await db.SaveChangesAsync();

        // 2. Création d'une session de jeu pour le vendredi prochain à 20h00
        var sessionHandler = new CreateSessionHandler(db);
        var sessionDate = new DateTime(2026, 10, 16, 20, 0, 0, DateTimeKind.Utc);
        var session = await sessionHandler.HandleAsync(sessionDate, channelId, guildId);

        Assert.NotNull(session);
        Assert.Equal(SessionStatus.Open, session.Status);
        Assert.Equal(2, session.MaxTables); // Hérité du serveur

        // 3. Déclaration des disponibilités par 4 joueurs
        var availHandler = new DeclareAvailabilityHandler(db, _renderer);
        await availHandler.HandleAsync(session.Id, userId: 101, username: "Alice", preferredGames: ["Catan", "7 Wonders"]);
        await availHandler.HandleAsync(session.Id, userId: 102, username: "Bob", preferredGames: ["Warhammer 40k"]);
        await availHandler.HandleAsync(session.Id, userId: 103, username: "Charlie", preferredGames: ["Warhammer 40k", "Dune"]);
        await availHandler.HandleAsync(session.Id, userId: 104, username: "David", preferredGames: ["Catan"]);

        var initialAvails = await db.PlayerAvailabilities.Where(a => a.GameSessionId == session.Id).ToListAsync();
        Assert.Equal(4, initialAvails.Count);
        Assert.All(initialAvails, a => Assert.False(a.IsAbsent));

        // 4. Bob crée la Table 1 (Warhammer 40k) et assigne Charlie
        var tableHandler = new CreateTableHandler(db, _renderer);
        var table1Result = await tableHandler.HandleAsync(
            session.Id,
            creatorUserId: 102,
            creatorUsername: "Bob",
            gameName: "Warhammer 40k",
            gameId: null,
            creatorRole: ParticipantRole.Player,
            additionalParticipants: [new ParticipantDto(103, "Charlie", ParticipantRole.Player)]);

        Assert.NotNull(table1Result.Table);
        var table1Id = table1Result.Table.Id;

        var tablesAfter1 = await db.GameTables.Where(t => t.GameSessionId == session.Id).ToListAsync();
        Assert.Single(tablesAfter1); // 1 table sur 2

        // 5. Alice crée la Table 2 (Catan) et assigne David -> Atteint la capacité maximale (2/2)
        var table2Result = await tableHandler.HandleAsync(
            session.Id,
            creatorUserId: 101,
            creatorUsername: "Alice",
            gameName: "Catan",
            gameId: null,
            creatorRole: ParticipantRole.Player,
            additionalParticipants: [new ParticipantDto(104, "David", ParticipantRole.Player)]);

        Assert.NotNull(table2Result.Table);
        var table2Id = table2Result.Table.Id;

        var tablesAfter2 = await db.GameTables.Where(t => t.GameSessionId == session.Id).ToListAsync();
        Assert.Equal(2, tablesAfter2.Count); // 2 tables sur 2 (Salle pleine)

        // 6. Eve (105) tente de créer une Table 3 -> Doit échouer car la capacité est atteinte
        var exCapacity = await Assert.ThrowsAsync<DomainException>(() => tableHandler.HandleAsync(
            session.Id,
            creatorUserId: 105,
            creatorUsername: "Eve",
            gameName: "Terraforming Mars",
            gameId: null,
            creatorRole: ParticipantRole.Player,
            additionalParticipants: []));

        Assert.Contains("capacité maximale", exCapacity.Message);

        // 7. Frank (106) rejoint la Table 1 en tant qu'Observateur (Spectateur)
        var joinHandler = new JoinTableHandler(db, _renderer);
        await joinHandler.HandleAsync(table1Id, userId: 106, username: "Frank", role: ParticipantRole.Spectator);

        var table1 = await db.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == table1Id);
        Assert.Equal(3, table1.Participants.Count);
        Assert.Contains(table1.Participants, p => p.DiscordUserId == 106 && p.Role == ParticipantRole.Spectator);

        // 8. Eve (105) rejoint la Table 2 comme Joueuse
        await joinHandler.HandleAsync(table2Id, userId: 105, username: "Eve", role: ParticipantRole.Player);
        var table2 = await db.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == table2Id);
        Assert.Equal(3, table2.Participants.Count); // Alice, David, Eve

        // 9. Règle d'exclusivité : David (sur Table 2) rejoint la Table 1
        // Doit être automatiquement délogé de la Table 2 et basculé sur la Table 1
        await joinHandler.HandleAsync(table1Id, userId: 104, username: "David", role: ParticipantRole.Player);

        var verifyTable2 = await db.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == table2Id);
        var verifyTable1 = await db.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == table1Id);
        Assert.DoesNotContain(verifyTable2.Participants, p => p.DiscordUserId == 104);
        Assert.Contains(verifyTable1.Participants, p => p.DiscordUserId == 104);

        // 10. Désistement progressif sur la Table 1 :
        var leaveHandler = new LeaveTableHandler(db, _renderer);

        // Bob (créateur) quitte -> Table 1 reste ouverte car il y a encore Charlie, David et Frank
        await leaveHandler.HandleAsync(table1Id, userId: 102);
        var checkTable1AfterBob = await db.GameTables.Include(t => t.Participants).FirstOrDefaultAsync(t => t.Id == table1Id);
        Assert.NotNull(checkTable1AfterBob);

        // Charlie se déclare Absent -> Charlie est retiré de la table et marqué absent
        var absentHandler = new SetAbsentHandler(db, _renderer);
        await absentHandler.HandleAsync(session.Id, userId: 103, username: "Charlie");
        var checkTable1AfterCharlie = await db.GameTables.Include(t => t.Participants).FirstOrDefaultAsync(t => t.Id == table1Id);
        Assert.NotNull(checkTable1AfterCharlie); // David et Frank sont toujours là

        // David quitte la Table 1 -> Il reste encore Frank (Spectateur), donc la table est encore préservée !
        await leaveHandler.HandleAsync(table1Id, userId: 104);
        var checkTable1AfterDavid = await db.GameTables.Include(t => t.Participants).FirstOrDefaultAsync(t => t.Id == table1Id);
        Assert.NotNull(checkTable1AfterDavid);
        Assert.Single(checkTable1AfterDavid.Participants); // Frank est encore là

        // Frank (dernier participant, spectateur) quitte à son tour -> Table totalement vide (0 participant) => Dissolution !
        await leaveHandler.HandleAsync(table1Id, userId: 106);

        var checkTable1Dissolved = await db.GameTables.FirstOrDefaultAsync(t => t.Id == table1Id);
        Assert.Null(checkTable1Dissolved); // Table dissoute !

        // 11. Vérifier que la capacité de la salle s'est libérée (1/2 tables)
        var remainingTables = await db.GameTables.Where(t => t.GameSessionId == session.Id).ToListAsync();
        Assert.Single(remainingTables);

        // 12. George (107) peut désormais créer la table qui était impossible à l'étape 6 !
        var table3Result = await tableHandler.HandleAsync(
            session.Id,
            creatorUserId: 107,
            creatorUsername: "George",
            gameName: "Terraforming Mars",
            gameId: null,
            creatorRole: ParticipantRole.Player,
            additionalParticipants: []);

        Assert.NotNull(table3Result.Table);
        var totalTablesFinal = await db.GameTables.Where(t => t.GameSessionId == session.Id).ToListAsync();
        Assert.Equal(2, totalTablesFinal.Count); // Retour à 2/2 tables

        // 13. Clôture manuelle de la session par un administrateur
        var closeHandler = new CloseSessionHandler(db, _renderer);
        var closedSession = await closeHandler.HandleAsync(session.Id);

        Assert.Equal(SessionStatus.Closed, closedSession.Status);

        // 14. Toute action d'inscription postérieure sur cette session doit être rejetée
        await Assert.ThrowsAsync<DomainException>(() => tableHandler.HandleAsync(
            session.Id, 108, "LateUser", "Small World", null, ParticipantRole.Player, []));
    }

    [Fact]
    public async Task MultiSession_ParallelGameNights_ShouldIsolateTablesAndExclusivity()
    {
        using var db = new AppDbContext(_options);

        var sessionHandler = new CreateSessionHandler(db);
        var fridayDate = new DateTime(2026, 10, 16, 20, 0, 0, DateTimeKind.Utc);
        var saturdayDate = new DateTime(2026, 10, 17, 20, 0, 0, DateTimeKind.Utc);

        var sessionFriday = await sessionHandler.HandleAsync(fridayDate, channelId: 1000, guildId: 500);
        var sessionSaturday = await sessionHandler.HandleAsync(saturdayDate, channelId: 1000, guildId: 500);

        var tableHandler = new CreateTableHandler(db, _renderer);

        // Alice (user 201) crée une table le Vendredi avec Bob (202)
        var tableFriday = await tableHandler.HandleAsync(
            sessionFriday.Id,
            creatorUserId: 201,
            creatorUsername: "Alice",
            gameName: "Root",
            gameId: null,
            creatorRole: ParticipantRole.Player,
            additionalParticipants: [new ParticipantDto(202, "Bob", ParticipantRole.Player)]);

        // Alice crée également une table le Samedi avec Charlie (203)
        var tableSaturday = await tableHandler.HandleAsync(
            sessionSaturday.Id,
            creatorUserId: 201,
            creatorUsername: "Alice",
            gameName: "Nemesis",
            gameId: null,
            creatorRole: ParticipantRole.Player,
            additionalParticipants: [new ParticipantDto(203, "Charlie", ParticipantRole.Player)]);

        // Assert : Alice est simultanément sur sa table du Vendredi ET sa table du Samedi sans interférence
        var loadedTableFriday = await db.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == tableFriday.Table.Id);
        var loadedTableSaturday = await db.GameTables.Include(t => t.Participants).FirstAsync(t => t.Id == tableSaturday.Table.Id);

        Assert.Contains(loadedTableFriday.Participants, p => p.DiscordUserId == 201);
        Assert.Contains(loadedTableSaturday.Participants, p => p.DiscordUserId == 201);

        // Clôturer la session du Vendredi ne doit pas impacter celle du Samedi
        var closeHandler = new CloseSessionHandler(db, _renderer);
        await closeHandler.HandleAsync(sessionFriday.Id);

        var checkFriday = await db.GameSessions.FirstAsync(s => s.Id == sessionFriday.Id);
        var checkSaturday = await db.GameSessions.FirstAsync(s => s.Id == sessionSaturday.Id);

        Assert.Equal(SessionStatus.Closed, checkFriday.Status);
        Assert.Equal(SessionStatus.Open, checkSaturday.Status);
    }

    [Fact]
    public async Task DynamicCapacityAdjustment_UnderLiveSession_ShouldEnforceCapacityLimitsDynamically()
    {
        using var db = new AppDbContext(_options);

        var sessionHandler = new CreateSessionHandler(db);
        var date = new DateTime(2026, 10, 23, 20, 0, 0, DateTimeKind.Utc);
        var session = await sessionHandler.HandleAsync(date, channelId: 1000, guildId: 500, maxTables: 1);

        var tableHandler = new CreateTableHandler(db, _renderer);

        // 1. Première table créée -> Atteint la capacité 1/1
        var t1 = await tableHandler.HandleAsync(session.Id, 301, "Player1", "Game 1", null, ParticipantRole.Player, []);
        Assert.NotNull(t1.Table);

        // 2. Deuxième table rejetée
        await Assert.ThrowsAsync<DomainException>(() =>
            tableHandler.HandleAsync(session.Id, 302, "Player2", "Game 2", null, ParticipantRole.Player, []));

        // 3. Admin augmente la capacité à 2 tables
        var capacityHandler = new SetSessionCapacityHandler(db, _renderer);
        await capacityHandler.HandleAsync(session.Id, 2);

        // 4. Deuxième table maintenant acceptée !
        var t2 = await tableHandler.HandleAsync(session.Id, 302, "Player2", "Game 2", null, ParticipantRole.Player, []);
        Assert.NotNull(t2.Table);

        // 5. Admin réduit la capacité à 1 alors qu'il y a déjà 2 tables formées
        await capacityHandler.HandleAsync(session.Id, 1);

        // Les 2 tables existantes sont conservées
        var existingTables = await db.GameTables.Where(t => t.GameSessionId == session.Id).ToListAsync();
        Assert.Equal(2, existingTables.Count);

        // Une 3ème table est immédiatement rejetée car 2 >= 1
        await Assert.ThrowsAsync<DomainException>(() =>
            tableHandler.HandleAsync(session.Id, 303, "Player3", "Game 3", null, ParticipantRole.Player, []));

        // Dissolution de Table 1 -> Reste 1 table, capacité toujours 1/1 -> Nouvelle table toujours rejetée
        var leaveHandler = new LeaveTableHandler(db, _renderer);
        await leaveHandler.HandleAsync(t1.Table.Id, 301);

        var tablesRemaining = await db.GameTables.Where(t => t.GameSessionId == session.Id).ToListAsync();
        Assert.Single(tablesRemaining);

        await Assert.ThrowsAsync<DomainException>(() =>
            tableHandler.HandleAsync(session.Id, 304, "Player4", "Game 4", null, ParticipantRole.Player, []));

        // Dissolution de Table 2 -> Reste 0 table, capacité 0/1 -> Nouvelle table acceptée !
        await leaveHandler.HandleAsync(t2.Table.Id, 302);
        var t3 = await tableHandler.HandleAsync(session.Id, 305, "Player5", "Game 5", null, ParticipantRole.Player, []);
        Assert.NotNull(t3.Table);
    }
}
