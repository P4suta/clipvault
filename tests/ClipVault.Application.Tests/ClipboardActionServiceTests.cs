using System.Text;
using ClipVault.Application.Clipboard;
using ClipVault.Application.Messages;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;

namespace ClipVault.Application.Tests;

public class ClipboardActionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(10);

    [Fact]
    public async Task MaterializeForView_returns_repository_content()
    {
        var entry = NewEntry();
        var content = new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes("full text"));
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.MaterializeAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(content);
        var service = Build(repo, Substitute.For<IClipboardWriter>(), new WeakReferenceMessenger());

        var result = await service.MaterializeForViewAsync(entry);

        Assert.Same(content, result);
    }

    [Fact]
    public async Task MaterializeForView_does_not_write_clipboard_or_mark_used()
    {
        var entry = NewEntry();
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.MaterializeAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>())
            .Returns(new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes("x")));
        var writer = Substitute.For<IClipboardWriter>();
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(repo, writer, messenger);

        await service.MaterializeForViewAsync(entry);

        await writer.DidNotReceive().WriteAsync(Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().UpdateAsync(Arg.Any<ClipboardEntry>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, announced);
    }

    [Fact]
    public async Task MaterializeForView_returns_null_when_entry_missing()
    {
        var entry = NewEntry();
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.MaterializeAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>()).Returns((ClipContent?)null);
        var service = Build(repo, Substitute.For<IClipboardWriter>(), new WeakReferenceMessenger());

        var result = await service.MaterializeForViewAsync(entry);

        Assert.Null(result);
    }

    [Fact]
    public async Task CopyForView_writes_content_without_marking_used()
    {
        var content = new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes("copy me"));
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var writer = Substitute.For<IClipboardWriter>();
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(repo, writer, messenger);

        await service.CopyForViewAsync(content);

        await writer.Received(1).WriteAsync(content, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().UpdateAsync(Arg.Any<ClipboardEntry>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, announced);
    }

    [Fact]
    public async Task CopyToClipboard_writes_marks_used_and_announces()
    {
        var entry = NewEntry();
        var content = new ClipContent(ClipContentType.Text, Encoding.UTF8.GetBytes("payload"));
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.MaterializeAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(content);
        var writer = Substitute.For<IClipboardWriter>();
        var monitor = Substitute.For<IClipboardMonitor>();
        monitor.SuppressNextCapture().Returns(Substitute.For<IDisposable>());
        var clock = Substitute.For<IClock>();
        var when = DateTimeOffset.UnixEpoch.AddDays(99);
        clock.UtcNow.Returns(when);
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = new ClipboardActionService(repo, writer, monitor, clock, messenger);

        var result = await service.CopyToClipboardAsync(entry);

        Assert.True(result);
        await writer.Received(1).WriteAsync(content, Arg.Any<CancellationToken>());
        monitor.Received(1).SuppressNextCapture();
        await repo.Received(1).UpdateAsync(entry, Arg.Any<CancellationToken>());
        Assert.Equal(when, entry.LastUsedAt);
        Assert.Equal(1, announced);
    }

    [Fact]
    public async Task CopyToClipboard_returns_false_and_does_nothing_when_entry_is_missing()
    {
        var entry = NewEntry();
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.MaterializeAsync(Arg.Any<EntryId>(), Arg.Any<CancellationToken>()).Returns((ClipContent?)null);
        var writer = Substitute.For<IClipboardWriter>();
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(repo, writer, messenger);

        var result = await service.CopyToClipboardAsync(entry);

        Assert.False(result);
        await writer.DidNotReceive().WriteAsync(Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().UpdateAsync(Arg.Any<ClipboardEntry>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, announced);
    }

    [Fact]
    public async Task TogglePin_pins_an_unpinned_entry_and_announces()
    {
        var entry = NewEntry();
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(repo, Substitute.For<IClipboardWriter>(), messenger);

        await service.TogglePinAsync(entry);

        Assert.True(entry.IsPinned);
        await repo.Received(1).UpdateAsync(entry, Arg.Any<CancellationToken>());
        Assert.Equal(1, announced);
    }

    [Fact]
    public async Task TogglePin_unpins_a_pinned_entry()
    {
        var entry = NewEntry();
        entry.Pin();
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var service = Build(repo, Substitute.For<IClipboardWriter>(), new WeakReferenceMessenger());

        await service.TogglePinAsync(entry);

        Assert.False(entry.IsPinned);
        await repo.Received(1).UpdateAsync(entry, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_removes_the_entry_and_announces()
    {
        var entry = NewEntry();
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(repo, Substitute.For<IClipboardWriter>(), messenger);

        await service.DeleteAsync(entry);

        await repo.Received(1).RemoveAsync(entry.Id, Arg.Any<CancellationToken>());
        Assert.Equal(1, announced);
    }

    [Fact]
    public async Task ClearAll_clears_the_repository_and_announces()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(repo, Substitute.For<IClipboardWriter>(), messenger);

        await service.ClearAllAsync();

        await repo.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        Assert.Equal(1, announced);
    }

    private static ClipboardEntry NewEntry() =>
        ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash("h"),
            "hello",
            image: null,
            sizeInBytes: 5,
            SourceApplication.Unknown,
            capturedAt: Now);

    private static ClipboardActionService Build(
        IClipboardHistoryRepository repository,
        IClipboardWriter writer,
        IMessenger messenger)
    {
        var monitor = Substitute.For<IClipboardMonitor>();
        monitor.SuppressNextCapture().Returns(Substitute.For<IDisposable>());
        return new ClipboardActionService(repository, writer, monitor, Substitute.For<IClock>(), messenger);
    }
}
