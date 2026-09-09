#:project ../Warhammer40k.Api/Warhammer40k.Api.csproj
#:property PublishAot=false

using System.Text.Json;
using Warhammer40k.Api;
using Warhammer40k.Core;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;

var output = Path.GetFullPath(args.Length == 1 ? args[0] : ".vs/phone-validation/fixture.json");
var catalogue = CatalogueProvider.LoadEmbedded();
RosterUnit Unit(string id)
{
    var unit = RosterUnit.FromDatasheet(catalogue.FindById(id) ?? throw new InvalidOperationException($"Missing fixture datasheet: {id}"));
    unit.Id = "phone-" + id;
    return unit;
}

var immortals = Unit("immortals");
immortals.ModelCount = 10;
var plasmancer = Unit("plasmancer");
plasmancer.AttachedToRosterUnitId = immortals.Id;
plasmancer.AssignedEnhancementId = "atomic-disintegrators";
plasmancer.IsWarlord = true;
var warriors = Unit("necron-warriors");
var overlord = Unit("overlord");
overlord.AttachedToRosterUnitId = warriors.Id;
var roster = new Roster
{
    Id = "phone-layout-fixture",
    Name = "Synthetic phone layout fixture — not a match army",
    PointsLimit = 2000,
    DetachmentId = "cryptek-conclave",
    DetachmentIds = ["cryptek-conclave"],
    Units = [immortals, plasmancer, warriors, overlord, Unit("canoptek-reanimator")],
};
var library = new ScheduleLibrary();

// Deliberately broad synthetic timings stress layout coverage; these are not a player's schedules.
void Schedule(string key)
{
    foreach (var phase in BattlePhases.Ordered)
        foreach (var turn in new[] { BattleTurn.Player, BattleTurn.Opponent })
            library.GetOrCreate(key).SetWindow(phase, turn, true);
}
foreach (var unit in roster.Units)
    foreach (var ability in catalogue.FindById(unit.DatasheetId)!.Abilities)
        Schedule(AbilityScheduleKeys.ForUnitAbility(unit.DatasheetId, ability.Name));
foreach (var rule in ArmyRuleCatalogue.ForFaction(roster.Faction))
    Schedule(AbilityScheduleKeys.ForArmyRule(rule.Name));
foreach (var stratagem in CoreStratagemCatalogue.All)
    Schedule(AbilityScheduleKeys.ForCoreStratagem(stratagem.Id));
var detachment = DetachmentCatalogue.FindById("cryptek-conclave")!;
foreach (var stratagem in detachment.Stratagems)
    Schedule(AbilityScheduleKeys.ForDetachmentStratagem(detachment.Id, stratagem.Id));
library.GetOrCreate(AbilityScheduleKeys.ForEnhancement("atomic-disintegrators")).ApplyToUnit = true;
library.GetOrCreate(AbilityScheduleKeys.ForUnitAbility("overlord", "My Will Be Done")).ApplyToUnit = true;
roster.AbilitySchedules = library.EffectiveFor(roster);

var fixture = new
{
    Kind = "synthetic-layout-only",
    Catalogue = catalogue,
    Roster = roster,
    Library = library,
    Settings = new UserSettings { PlayCardSwipe = true, PlayHudSticky = true },
    User = new UserInfo(true, "phone-layout-fixture", "Synthetic layout fixture", "github", ["authenticated"]),
    Validation = new RosterValidator().Validate(roster, catalogue),
    Microscarab = detachment.Stratagems.Single(stratagem => stratagem.Name == "Microscarab Swarm"),
};
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(fixture, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
Console.WriteLine($"Synthetic layout fixture written to {output}; do not import it into an account.");
