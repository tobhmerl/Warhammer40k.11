using System.Text.Json;
using Warhammer40k.Api;
using Warhammer40k.Core;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// AB8 coverage: settings entity mapping (incl. theme normalization) and a JSON round-trip of the
/// <see cref="BackupBundle"/> over the Web defaults used by the Functions host and the Blazor client.
/// </summary>
public class SettingsAndBackupTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SettingsEntity_round_trips_values()
    {
        var settings = new UserSettings { DefaultPointsLimit = 1500, Theme = "arcane", PlayHudSticky = true, PlayCardSwipe = true };

        var entity = SettingsEntity.From("github|7", settings);
        var back = entity.ToSettings();

        Assert.Equal("github|7", entity.PartitionKey);
        Assert.Equal("settings", entity.RowKey);
        Assert.Equal(1500, back.DefaultPointsLimit);
        Assert.Equal("arcane", back.Theme);
        Assert.True(back.PlayHudSticky);
        Assert.True(back.PlayCardSwipe);
    }

    [Fact]
    public void SettingsEntity_defaults_play_layout_to_floating_scroll()
    {
        Assert.False(UserSettings.Default.PlayHudSticky);
        Assert.False(UserSettings.Default.PlayCardSwipe);

        var back = SettingsEntity.From("u", UserSettings.Default).ToSettings();
        Assert.False(back.PlayHudSticky);
        Assert.False(back.PlayCardSwipe);
    }

    [Fact]
    public void SettingsEntity_normalizes_unknown_theme_to_default()
    {
        var entity = SettingsEntity.From("u", new UserSettings { Theme = "not-a-theme" });

        Assert.Equal(AppThemes.Default, entity.ToSettings().Theme);
    }

    [Fact]
    public void AppThemes_normalize_keeps_known_and_falls_back_for_unknown()
    {
        Assert.Equal("ember", AppThemes.Normalize("ember"));
        Assert.Equal(AppThemes.Default, AppThemes.Normalize("bogus"));
        Assert.Equal(AppThemes.Default, AppThemes.Normalize(null));
    }

    [Fact]
    public void BackupBundle_round_trips_through_web_json()
    {
        var bundle = new BackupBundle
        {
            CreatedUtc = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Settings = new UserSettings { DefaultPointsLimit = 1250, Theme = "blood" },
            Catalogue = new CatalogueData
            {
                Faction = "Necrons",
                Datasheets = [new Datasheet { Id = "overlord", Name = "Overlord", PointsOptions = [new PointsOption { Models = 1, Points = 85 }] }],
            },
            Rosters =
            [
                new Roster
                {
                    Id = "r1",
                    Name = "Vanguard",
                    Faction = Roster.NecronsFaction,
                    PointsLimit = 2000,
                    DetachmentId = "hand-of-the-dynasty",
                    Units = [new RosterUnit { Id = "u1", DatasheetId = "overlord", ModelCount = 1, IsWarlord = true }],
                },
            ],
        };

        var json = JsonSerializer.Serialize(bundle, Web);
        var back = JsonSerializer.Deserialize<BackupBundle>(json, Web);

        Assert.NotNull(back);
        Assert.Equal("tombworld-backup-v1", back!.Format);
        Assert.Equal(1250, back.Settings.DefaultPointsLimit);
        Assert.Equal("blood", back.Settings.Theme);
        Assert.NotNull(back.Catalogue);
        Assert.Equal("Overlord", back.Catalogue!.Datasheets.Single().Name);
        var roster = Assert.Single(back.Rosters);
        Assert.Equal("Vanguard", roster.Name);
        Assert.True(roster.Units.Single().IsWarlord);
    }

    [Fact]
    public void BackupBundle_allows_null_catalogue_for_default_users()
    {
        var bundle = new BackupBundle { Catalogue = null, Rosters = [] };

        var json = JsonSerializer.Serialize(bundle, Web);
        var back = JsonSerializer.Deserialize<BackupBundle>(json, Web);

        Assert.NotNull(back);
        Assert.Null(back!.Catalogue);
        Assert.Empty(back.Rosters);
    }
}
