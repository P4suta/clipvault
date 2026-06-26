using System.Text;
using ClipVault.Application.Abstractions;
using ClipVault.Application.Capture;
using ClipVault.Application.Clipboard;
using ClipVault.Application.History;
using ClipVault.Application.Insights;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using ClipVault.Infrastructure.Persistence;
using ClipVaultApp.Localization;
using ClipVaultApp.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;

namespace ClipVault.App.Tests;

public sealed class HistoryViewModelTests : IDisposable
{
    private readonly InMemoryClipboardHistoryRepository _repo = new();
    private readonly IClipboardWriter _writer = Substitute.For<IClipboardWriter>();
    private readonly IClipboardMonitor _monitor = Substitute.For<IClipboardMonitor>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly CaptureStateService _captureState = new();
    private readonly HistoryViewModel _vm;

    public HistoryViewModelTests()
    {
        _clock.UtcNow.Returns(DateTimeOffset.UnixEpoch.AddDays(1));
        var query = new HistoryQueryService(_repo);
        var actions = new ClipboardActionService(_repo, _writer, _monitor, _clock, _messenger);
        _vm = new HistoryViewModel(
            query, actions, _captureState, _messenger, new FakeUiDispatcher(), new LocalizationService(AppLanguage.Japanese));
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public async Task Load_populates_entries_and_clears_empty()
    {
        await SeedAsync("a", "first");
        await SeedAsync("b", "second");

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, _vm.Entries!.Count);
        Assert.False(_vm.IsEmpty);
    }

    [Fact]
    public async Task Load_marks_empty_when_the_repository_has_no_entries()
    {
        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.True(_vm.IsEmpty);
    }

    [Fact]
    public async Task Kind_filter_narrows_the_list()
    {
        await SeedAsync("t", "plain text", payload: "plain text");
        await SeedAsync("u", "https://example.com", payload: "https://example.com");
        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.SelectedKindFilter = _vm.KindFilters.First(o => o.Kind == ContentKind.Url);

        Assert.Equal("https://example.com", Assert.Single(_vm.Entries!).Preview);
    }

    [Fact]
    public async Task App_filter_narrows_the_list()
    {
        await SeedAsync("a", "from chrome", app: "chrome");
        await SeedAsync("b", "from code", app: "code");
        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.SelectedAppFilter = _vm.AppFilters.First(o => string.Equals(o.App, "code", StringComparison.Ordinal));

        Assert.Equal("from code", Assert.Single(_vm.Entries!).Preview);
    }

    [Fact]
    public async Task Is_filter_active_reflects_a_concrete_selection()
    {
        await SeedAsync("u", "https://example.com", payload: "https://example.com");
        await _vm.LoadCommand.ExecuteAsync(null);
        Assert.False(_vm.IsFilterActive);

        _vm.SelectedKindFilter = _vm.KindFilters.First(o => o.Kind == ContentKind.Url);

        Assert.True(_vm.IsFilterActive);
    }

    [Fact]
    public async Task Filter_options_list_the_present_kinds_and_apps()
    {
        await SeedAsync("u", "https://example.com", app: "chrome", payload: "https://example.com");
        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains(_vm.KindFilters, o => o.Kind == ContentKind.Url);
        Assert.Contains(_vm.AppFilters, o => string.Equals(o.App, "chrome", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Delete_command_removes_the_entry_and_reloads()
    {
        await SeedAsync("a", "doomed");
        await _vm.LoadCommand.ExecuteAsync(null);

        await _vm.DeleteCommand.ExecuteAsync(_vm.Entries!.Single());

        Assert.Empty(_vm.Entries!);
        Assert.Equal(0, await _repo.CountAsync());
    }

    [Fact]
    public async Task Toggle_pin_command_pins_the_entry()
    {
        var entry = await SeedAsync("a", "pin me");
        await _vm.LoadCommand.ExecuteAsync(null);

        await _vm.TogglePinCommand.ExecuteAsync(_vm.Entries!.Single());

        Assert.True(entry.IsPinned);
    }

    [Fact]
    public async Task Paste_raises_paste_requested_when_the_copy_succeeds()
    {
        await SeedAsync("a", "copy me", payload: "copy me");
        await _vm.LoadCommand.ExecuteAsync(null);
        var raised = false;
        _vm.PasteRequested += (_, _) => raised = true;

        await _vm.PasteCommand.ExecuteAsync(_vm.Entries!.Single());

        Assert.True(raised);
        await _writer.Received(1).WriteAsync(Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Paste_ignores_a_null_item()
    {
        var raised = false;
        _vm.PasteRequested += (_, _) => raised = true;

        await _vm.PasteCommand.ExecuteAsync(null);

        Assert.False(raised);
    }

    [Fact]
    public void Toggle_pause_syncs_with_the_capture_state()
    {
        _vm.TogglePauseCommand.Execute(null);

        Assert.True(_vm.IsPaused);
        Assert.True(_captureState.IsPaused);
    }

    [Fact]
    public async Task View_detail_for_a_tracking_url_enables_clean_url_then_close_resets()
    {
        await SeedAsync("u", "https://e.com", payload: "https://e.com/?utm_source=x&id=1");
        await _vm.LoadCommand.ExecuteAsync(null);

        await _vm.ViewDetailCommand.ExecuteAsync(_vm.Entries!.Single());

        Assert.True(_vm.IsDetailOpen);
        Assert.True(_vm.CanCleanUrl);
        Assert.False(_vm.DetailIsImage);

        _vm.CloseDetailCommand.Execute(null);

        Assert.False(_vm.IsDetailOpen);
        Assert.Null(_vm.DetailText);
    }

    [Fact]
    public async Task Copy_cleaned_url_writes_to_the_clipboard()
    {
        await SeedAsync("u", "https://e.com", payload: "https://e.com/?utm_source=x&id=1");
        await _vm.LoadCommand.ExecuteAsync(null);
        await _vm.ViewDetailCommand.ExecuteAsync(_vm.Entries!.Single());

        await _vm.CopyCleanedUrlCommand.ExecuteAsync(null);

        await _writer.Received(1).WriteAsync(Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
    }

    private async Task<ClipboardEntry> SeedAsync(string hash, string preview, string app = "chrome", string payload = "payload")
    {
        var entry = ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash(hash),
            preview,
            image: null,
            sizeInBytes: payload.Length,
            new SourceApplication(app, null, null),
            DateTimeOffset.UnixEpoch.AddDays(1));
        await _repo.AddAsync(entry, new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes(payload)));
        return entry;
    }
}
