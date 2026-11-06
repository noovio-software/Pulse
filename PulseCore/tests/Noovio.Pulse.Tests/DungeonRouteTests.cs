using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Routes;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Infrastructure.KeystoneGuru;

namespace Noovio.Pulse.Tests;

public sealed class DungeonRouteTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private static DungeonRoute Route(
        string key,
        int[]? pulls = null,
        string title = "Route",
        string? author = "Auteur",
        int forces = 100,
        int required = 100,
        int? season = 18,
        DateTimeOffset? expires = null) =>
        new(key, title, author, null, forces, required, pulls ?? [5, 5], $"https://keystone.guru/{key}", $"https://keystone.guru/{key}/embed", null, season, expires);

    [Fact]
    public void Keep_drops_expired_empty_and_incomplete_routes_and_preserves_order()
    {
        var kept = RouteSelection.Keep(
            [
                Route("expired", expires: Now.AddDays(-1)),
                Route("b"),
                Route("empty", pulls: []),
                Route("incomplete", forces: 90),
                Route("a", expires: Now.AddDays(1))
            ],
            Now,
            8);

        Assert.Equal(["b", "a"], kept.Select(r => r.PublicKey));
    }

    [Fact]
    public void Keep_retains_only_the_latest_season_and_the_requested_count()
    {
        var kept = RouteSelection.Keep(
            [Route("old", season: 17), Route("a"), Route("b"), Route("c")],
            Now,
            2);

        Assert.Equal(["a", "b"], kept.Select(r => r.PublicKey));
    }

    [Fact]
    public void Keep_accepts_an_empty_list()
    {
        Assert.Empty(RouteSelection.Keep([], Now, 8));
    }

    [Fact]
    public void Highlights_name_the_popular_route_tank_authors_and_pull_extremes()
    {
        var highlights = RouteSelection.Highlights(
        [
            Route("first", pulls: [6, 6]),
            Route("tank", pulls: [12, 2], author: "Skandar Tank"),
            Route("big", pulls: [10, 10]),
            Route("calm", pulls: [4, 4], title: "Tankard run")
        ]);

        Assert.Equal([RouteHighlight.MostPopular], highlights["first"]);
        Assert.Equal([RouteHighlight.TankAuthored], highlights["tank"]);
        Assert.Equal([RouteHighlight.BiggestPulls], highlights["big"]);

        // « Tankard » contient « tank » sans être le mot : seule la distinction des pulls reste.
        Assert.Equal([RouteHighlight.SmallestPeak], highlights["calm"]);
    }

    [Fact]
    public void Highlights_do_not_compare_identical_routes()
    {
        var highlights = RouteSelection.Highlights([Route("a"), Route("b")]);

        Assert.Equal([RouteHighlight.MostPopular], highlights["a"]);
        Assert.Empty(highlights["b"]);
    }

    [Fact]
    public void Average_pull_ignores_pulls_without_enemies()
    {
        var route = Route("a", pulls: [10, 0, 20]);

        Assert.Equal(15, route.AveragePull);
        Assert.Equal(20, route.LargestPull);
    }

    [Fact]
    public void ParseRouteKeys_keeps_page_order_this_dungeon_only_and_no_duplicates()
    {
        const string html = """
            <a href="https://keystone.guru/route/murder-row/XsdAG1V/other-dungeon">x</a>
            <a href="https://keystone.guru/route/altar-of-fangs/xwt8Pza/skandar-tank">x</a>
            <a href='https://keystone.guru/route/altar-of-fangs/0RhVlYt/elitzur-altar-1'>x</a>
            <a href="https://keystone.guru/route/altar-of-fangs/xwt8Pza/skandar-tank">x</a>
            <a href="https://keystone.guru/route/altar-of-fangs/editOnly/some-route/edit">x</a>
            """;

        Assert.Equal(["xwt8Pza", "0RhVlYt"], KeystoneGuruClient.ParseRouteKeys(html, "altar-of-fangs"));
    }

    [Fact]
    public void ParseRoute_reads_the_shape_the_api_really_returns()
    {
        // Réponse réelle, réduite : affixGroups y est un objet, là où la spécification annonce un tableau.
        const string json = """
            {
              "data": {
                "dungeonId": 182,
                "publicKey": "xwt8Pza",
                "title": "Skandar Tank - Altar of Fangs",
                "enemyForces": 825,
                "enemyForcesRequired": 817,
                "expiresAt": null,
                "pullCount": 2,
                "author": { "publicKey": "4zkdIZp", "name": "Skandar Tank", "links": { "view": "https://keystone.guru/user/skandar-tank", "avatar": null } },
                "affixGroups": { "expansion": "midnight", "season": 18, "affixes": [{ "id": 9, "name": "Tyrannical" }] },
                "links": {
                  "view": "https://keystone.guru/route/altar-of-fangs/xwt8Pza/x",
                  "embed": "https://keystone.guru/route/altar-of-fangs/xwt8Pza/x/embed",
                  "thumbnails": ["https://uploads.keystone.guru/thumbnails/xwt8Pza/1.jpg"]
                },
                "pulls": [
                  { "description": null, "spells": [], "enemies": [{ "npcId": 1, "enemyForces": 7 }, { "npcId": 2, "enemyForces": 5 }] },
                  { "description": null, "spells": [], "enemies": [] }
                ]
              }
            }
            """;

        var route = KeystoneGuruClient.ParseRoute(json);

        Assert.NotNull(route);
        Assert.Equal("Skandar Tank", route.Author);
        Assert.Equal("https://keystone.guru/user/skandar-tank", route.AuthorUrl);
        Assert.Equal([2, 0], route.PullSizes);
        Assert.Equal(18, route.GameSeason);
        Assert.Null(route.ExpiresAtUtc);
        Assert.EndsWith("/embed", route.EmbedUrl);
    }

    [Fact]
    public async Task Sync_keeps_going_after_a_failed_dungeon_and_never_stores_an_empty_list()
    {
        var store = new FakeStore();
        var service = new RouteSyncService(
            new FakeSeason(),
            new FakeRoutes(),
            store,
            new FakeJournal(),
            Options.Create(new RouteSyncOptions()),
            TimeProvider.System,
            NullLogger<RouteSyncService>.Instance);

        var report = await service.RunAsync(CancellationToken.None);

        Assert.Equal(1, report.Updated);
        Assert.Equal(2, report.Failed);

        var saved = Assert.Single(store.Saved);
        Assert.Equal("kg-good", saved.Slug); // le slug Keystone.guru, retrouvé par le mode défi
        Assert.Equal("Bon Donjon", saved.Name); // le nom du Journal, dans la langue du site
        Assert.Equal(["a"], saved.Routes.Select(r => r.PublicKey));
    }

    private sealed class FakeSeason : ISeasonDungeonSource
    {
        public Task<SeasonDungeonList> GetCurrentDungeonsAsync(CancellationToken ct) =>
            Task.FromResult(new SeasonDungeonList("season-mn-2",
            [
                new SeasonDungeonInfo("broken", "Broken", "BRK", 1, 1800, null, null),
                new SeasonDungeonInfo("empty", "Empty", "EMP", 2, 1800, null, null),
                new SeasonDungeonInfo("good", "Good Dungeon", "GD", 3, 1800, null, null)
            ]));
    }

    private sealed class FakeRoutes : IDungeonRouteSource
    {
        public Task<IReadOnlyDictionary<int, string>> GetDungeonSlugsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string> { [3] = "kg-good" });

        public Task<IReadOnlyList<string>> GetPopularRouteKeysAsync(string dungeonSlug, int count, CancellationToken ct) => dungeonSlug switch
        {
            "broken" => throw new HttpRequestException("500"),
            "empty" => Task.FromResult<IReadOnlyList<string>>([]),
            _ => Task.FromResult<IReadOnlyList<string>>(["gone", "a"])
        };

        public Task<DungeonRoute?> GetRouteAsync(string publicKey, CancellationToken ct) =>
            Task.FromResult(publicKey == "gone" ? null : Route(publicKey));
    }

    private sealed class FakeStore : IDungeonRouteStore
    {
        public List<SeasonDungeon> Saved { get; } = [];

        public Task ReplaceAsync(SeasonDungeon dungeon, CancellationToken ct)
        {
            Saved.Add(dungeon);
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> GetLastFetchedAsync(CancellationToken ct) => Task.FromResult<DateTimeOffset?>(null);
    }

    private sealed class FakeJournal : IEncounterJournalSource
    {
        public Task<EncounterJournal?> GetAsync(string season, CancellationToken ct) =>
            Task.FromResult<EncounterJournal?>(new EncounterJournal([new JournalInstance(1, "Bon Donjon", "Good Dungeon", InstanceKind.Dungeon, [])]));
    }
}
