using Microsoft.AspNetCore.Components;
using Noovio.Pulse.Web.Localization;

namespace Noovio.Pulse.Web.Components.Shared;

public partial class LanguageSwitch
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private static IEnumerable<string> Others =>
        PulseLocalization.Cultures.Where(c => c != PulseLocalization.CurrentLanguage);
}
