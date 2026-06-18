using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// Implements the <b>auto-apply</b> half of rule R10: when the Pantheon of Woe detachment is selected, every
/// Necrons Monster takes its matching Necrodermal Binding and pays the surcharge. This is the only mutation
/// the engine performs; the UI calls it on roster edits, then validates. It is pure (no I/O) and idempotent —
/// an already-applied (edited) surcharge is preserved.
/// </summary>
public static class PantheonBindingApplier
{
    /// <summary>Applies or clears Pantheon bindings on a roster's units for the given detachment.</summary>
    public static void Apply(Roster roster, CatalogueData catalogue, Detachment? detachment)
    {
        var pantheon = detachment?.AppliesPantheonBindings ?? false;

        foreach (var unit in roster.Units)
        {
            var sheet = catalogue.FindById(unit.DatasheetId);
            var binding = pantheon && sheet is { IsMonster: true }
                ? catalogue.FindBindingForUnit(sheet.Name)
                : null;

            if (binding is not null)
            {
                // Apply the default surcharge only on first apply / when the binding changes, so an
                // editable surcharge a user has tweaked is not clobbered on a re-run.
                if (!string.Equals(unit.AppliedBindingId, binding.Name, StringComparison.Ordinal))
                {
                    unit.AppliedBindingId = binding.Name;
                    unit.BindingSurcharge = binding.Points;
                }
            }
            else
            {
                unit.AppliedBindingId = null;
                unit.BindingSurcharge = 0;
            }
        }
    }
}
