using AltPulse.Client.Components;
using AltPulse.Client.Models;
using AltPulse.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace AltPulse.Client.Pages;

public partial class Home
{
    [Inject] private IAltPulseApi Api { get; set; } = default!;
    [Inject] private LocalHistoryStore Store { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool ready;
    private bool searching;
    private bool searchOpen;
    private bool focusNameInput;
    private bool labelsCompact;
    private ElementReference nameInput;
    private string region = "eu";
    private string? realm;
    private string? name;
    private string? searchError;
    private string selectedHistoryKey = "";
    private string? highlightKey;

    private AltPulseSettings settings = new();
    private List<HistoryEntry> history = [];
    private readonly List<CharacterColumn> columns = [];
    private IReadOnlyList<RealmInfo> realms = [];
    private SeasonInfo? season;
    private CharacterSnapshot? equipmentFor;

    private IReadOnlyList<CharacterColumn> SortedColumns => Ui.Sort(columns, settings.SortBy).ToList();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (focusNameInput && searchOpen)
        {
            focusNameInput = false;
            await nameInput.FocusAsync(preventScroll: true);
        }

        if (!firstRender)
        {
            return;
        }

        // localStorage n'existe qu'une fois le circuit/WASM interactif : rien n'est lu pendant le prérendu.
        settings = await Store.GetSettingsAsync();
        history = await Store.GetHistoryAsync();
        region = Regions.All.Contains(settings.LastRegion) ? settings.LastRegion : "eu";
        labelsCompact = settings.LabelsCompact ?? await JS.InvokeAsync<bool>("altpulse.isSmallScreen");
        columns.AddRange(history.Select(h => new CharacterColumn(h)));
        ready = true;
        if (columns.Count == 0)
        {
            OpenSearch();
        }

        StateHasChanged();

        await Task.WhenAll(LoadRegionAsync(region), LoadAllColumnsAsync());
    }

    private async Task LoadRegionAsync(string targetRegion)
    {
        var seasonTask = Api.GetSeasonAsync(targetRegion);
        var realmsTask = Api.GetRealmsAsync(targetRegion);
        try
        {
            await Task.WhenAll(seasonTask, realmsTask);
        }
        catch (Exception)
        {
            // Les tâches en échec sont gérées individuellement ci-dessous.
        }

        if (targetRegion != region)
        {
            return;
        }

        season = seasonTask.IsCompletedSuccessfully ? seasonTask.Result : season;
        realms = realmsTask.IsCompletedSuccessfully ? realmsTask.Result : [];
        StateHasChanged();
    }

    private async Task LoadAllColumnsAsync()
    {
        using var throttle = new SemaphoreSlim(4);
        await Task.WhenAll(columns.ToList().Select(async c =>
        {
            await throttle.WaitAsync();
            try
            {
                await LoadColumnAsync(c, forceRefresh: false);
            }
            finally
            {
                throttle.Release();
            }
        }));
    }

    private async Task LoadColumnAsync(CharacterColumn column, bool forceRefresh)
    {
        column.Loading = true;
        column.Error = null;
        StateHasChanged();

        try
        {
            var result = await Api.GetCharacterAsync(column.Entry.Region, column.Entry.RealmSlug, column.Entry.Name, forceRefresh);
            column.Data = result.Character ?? column.Data;
            column.Error = result.Error;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            column.Error = "Serveur injoignable.";
        }
        finally
        {
            column.Loading = false;
            StateHasChanged();
        }
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(realm) || string.IsNullOrWhiteSpace(name))
        {
            searchError = "Indique un royaume et un nom de personnage.";
            return;
        }

        var realmSlug = realms.FirstOrDefault(r => string.Equals(r.Name, realm.Trim(), StringComparison.OrdinalIgnoreCase))?.Slug ?? realm.Trim();
        await SearchCoreAsync(realmSlug);
    }

    private async Task SearchCoreAsync(string realmSlug)
    {
        searching = true;
        searchError = null;

        try
        {
            var result = await Api.GetCharacterAsync(region, realmSlug, name!.Trim());
            if (result.Character is not { } character)
            {
                searchError = result.Error ?? "Personnage introuvable.";
                return;
            }

            history = await Store.AddOrTouchAsync(character);
            var entry = history[0];
            var column = columns.FirstOrDefault(c => c.Key == character.Key);
            if (column is null)
            {
                column = new CharacterColumn(entry);
                columns.Add(column);
            }

            column.Entry = entry;
            column.Data = character;
            column.Error = null;
            selectedHistoryKey = character.Key;
            searchOpen = false;
            await HighlightAsync(character.Key);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            searchError = "Impossible de joindre le serveur.";
        }
        finally
        {
            searching = false;
        }
    }

    private void OpenSearch()
    {
        searchOpen = true;
        searchError = null;
        focusNameInput = true;
    }

    private void CloseSearch() => searchOpen = false;

    private void OnSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            CloseSearch();
        }
    }

    private async Task OnHistorySelectedAsync(ChangeEventArgs e)
    {
        selectedHistoryKey = e.Value?.ToString() ?? "";
        if (history.FirstOrDefault(h => h.Key == selectedHistoryKey) is not { } entry)
        {
            return;
        }

        name = entry.Name;
        realm = entry.RealmName;
        if (region != entry.Region)
        {
            region = entry.Region;
            await LoadRegionAsync(region);
        }

        await SearchCoreAsync(entry.RealmSlug);
    }

    private async Task OnRegionChangedAsync(ChangeEventArgs e)
    {
        region = e.Value?.ToString() ?? "eu";
        settings = settings with { LastRegion = region };
        await Store.SaveSettingsAsync(settings);
        await LoadRegionAsync(region);
    }

    private async Task OnSortChangedAsync(string sortBy)
    {
        settings = settings with { SortBy = sortBy };
        await Store.SaveSettingsAsync(settings);
    }

    private async Task ToggleCompactAsync()
    {
        labelsCompact = !labelsCompact;
        settings = settings with { LabelsCompact = labelsCompact };
        await Store.SaveSettingsAsync(settings);
    }

    private async Task ToggleSectionAsync(string section)
    {
        if (!settings.CollapsedSections.Remove(section))
        {
            settings.CollapsedSections.Add(section);
        }

        await Store.SaveSettingsAsync(settings);
    }

    private async Task RemoveAsync(CharacterColumn column)
    {
        columns.Remove(column);
        history = await Store.RemoveAsync(column.Key);
        if (selectedHistoryKey == column.Key)
        {
            selectedHistoryKey = "";
        }
    }

    private async Task ClearHistoryAsync()
    {
        await Store.ClearAsync();
        history.Clear();
        columns.Clear();
        selectedHistoryKey = "";
    }

    private async Task HighlightAsync(string key)
    {
        highlightKey = key;
        StateHasChanged();
        await Task.Yield();
        await JS.InvokeVoidAsync("altpulse.scrollToColumn", key);
    }
}
