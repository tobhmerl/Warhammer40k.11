using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Tests;

/// <summary>
/// Enhancements are detachment-specific, so switching detachment must revoke assignments the new selection no
/// longer offers (otherwise R6 reports "not part of your detachment(s)"). Pins
/// <see cref="EnhancementReconciler"/>.
/// </summary>
public class EnhancementReconcilerTests
{
    private static readonly Detachment Starshatter = DetachmentCatalogue.FindById("starshatter-arsenal")!;
    private static readonly Detachment Awakened = DetachmentCatalogue.FindById("awakened-dynasty")!;

    [Fact]
    public void Switching_detachment_revokes_an_enhancement_the_new_selection_does_not_offer()
    {
        var roster = new Roster
        {
            DetachmentId = "awakened-dynasty",
            Units =
            [
                new RosterUnit { Id = "u1", DatasheetId = "overlord", AssignedEnhancementId = "dread-majesty" },
            ],
        };

        var revoked = EnhancementReconciler.Revoke(roster, [Awakened]);

        Assert.True(revoked);
        Assert.Null(roster.Units[0].AssignedEnhancementId);
    }

    [Fact]
    public void An_enhancement_still_offered_by_the_selection_is_kept()
    {
        var roster = new Roster
        {
            DetachmentId = "awakened-dynasty",
            Units =
            [
                new RosterUnit { Id = "u1", DatasheetId = "overlord", AssignedEnhancementId = "phasal-subjugator" },
            ],
        };

        var revoked = EnhancementReconciler.Revoke(roster, [Awakened]);

        Assert.False(revoked);
        Assert.Equal("phasal-subjugator", roster.Units[0].AssignedEnhancementId);
    }

    [Fact]
    public void Revoking_an_enhancement_drops_its_orphaned_schedule()
    {
        var roster = new Roster
        {
            DetachmentId = "awakened-dynasty",
            Units =
            [
                new RosterUnit { Id = "u1", DatasheetId = "overlord", AssignedEnhancementId = "dread-majesty" },
            ],
        };
        var key = AbilityScheduleKeys.ForEnhancement("dread-majesty");
        roster.GetOrCreateSchedule(key).ApplyToUnit = true;

        EnhancementReconciler.Revoke(roster, [Awakened]);

        Assert.Null(roster.FindSchedule(key));
    }

    [Fact]
    public void A_kept_enhancement_retains_its_schedule()
    {
        var roster = new Roster
        {
            DetachmentId = "starshatter-arsenal",
            Units =
            [
                new RosterUnit { Id = "u1", DatasheetId = "overlord", AssignedEnhancementId = "dread-majesty" },
            ],
        };
        var key = AbilityScheduleKeys.ForEnhancement("dread-majesty");
        roster.GetOrCreateSchedule(key).ApplyToUnit = true;

        var revoked = EnhancementReconciler.Revoke(roster, [Starshatter]);

        Assert.False(revoked);
        Assert.NotNull(roster.FindSchedule(key));
    }

    [Fact]
    public void Clearing_all_detachments_revokes_every_enhancement()
    {
        var roster = new Roster
        {
            Units =
            [
                new RosterUnit { Id = "u1", DatasheetId = "overlord", AssignedEnhancementId = "dread-majesty" },
                new RosterUnit { Id = "u2", DatasheetId = "warriors", AssignedEnhancementId = "phasal-subjugator" },
            ],
        };

        var revoked = EnhancementReconciler.Revoke(roster, []);

        Assert.True(revoked);
        Assert.All(roster.Units, u => Assert.Null(u.AssignedEnhancementId));
    }
}
