using ClipVault.Application.Capture;
using ClipVault.Application.Capture.Rules;
using ClipVault.Application.Clipboard;
using ClipVault.Application.Messages;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;

namespace ClipVault.Application.Tests;

public class ClipboardIngestionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(10);

    [Fact]
    public async Task Empty_snapshot_is_ignored()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var service = Build(repo, new WeakReferenceMessenger());

        var empty = new ClipboardSnapshot(
            ClipContentType.Text,
            [],
            string.Empty,
            null,
            SourceApplication.Unknown,
            ClipboardPrivacySignals.None);
        var outcome = await service.IngestAsync(empty);

        Assert.Equal(IngestionStatus.Ignored, outcome.Status);
        await repo.DidNotReceive().AddAsync(Arg.Any<ClipboardEntry>(), Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejected_by_gate_is_not_stored()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        var service = Build(repo, new WeakReferenceMessenger(), new PrivacySignalRule());

        var forbidden = Snapshots.Text("secret", signals: new ClipboardPrivacySignals(ExcludeFromHistory: true, null));
        var outcome = await service.IngestAsync(forbidden);

        Assert.Equal(IngestionStatus.Rejected, outcome.Status);
        await repo.DidNotReceive().AddAsync(Arg.Any<ClipboardEntry>(), Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_content_is_added_and_announced()
    {
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.FindByHashAsync(Arg.Any<ContentHash>(), Arg.Any<CancellationToken>()).Returns((ClipboardEntry?)null);

        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);

        var service = Build(repo, messenger);
        var outcome = await service.IngestAsync(Snapshots.Text("hello"));

        Assert.Equal(IngestionStatus.Added, outcome.Status);
        await repo.Received(1).AddAsync(Arg.Any<ClipboardEntry>(), Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, announced);
    }

    [Fact]
    public async Task Duplicate_content_promotes_existing_without_inserting()
    {
        var existing = ClipboardEntry.Create(
            ClipContentType.Text,
            new ContentHash("h"),
            "hello",
            null,
            5,
            SourceApplication.Unknown,
            capturedAt: Now.AddDays(-1));
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.FindByHashAsync(Arg.Any<ContentHash>(), Arg.Any<CancellationToken>()).Returns(existing);

        var service = Build(repo, new WeakReferenceMessenger());
        var outcome = await service.IngestAsync(Snapshots.Text("hello"));

        Assert.Equal(IngestionStatus.Promoted, outcome.Status);
        Assert.Equal(Now, existing.LastUsedAt);
        await repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().AddAsync(Arg.Any<ClipboardEntry>(), Arg.Any<ClipContent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_snapshot_does_not_announce()
    {
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(Substitute.For<IClipboardHistoryRepository>(), messenger);

        var empty = new ClipboardSnapshot(
            ClipContentType.Text, [], string.Empty, null, SourceApplication.Unknown, ClipboardPrivacySignals.None);
        await service.IngestAsync(empty);

        Assert.Equal(0, announced);
    }

    [Fact]
    public async Task Rejected_snapshot_does_not_announce()
    {
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(Substitute.For<IClipboardHistoryRepository>(), messenger, new PrivacySignalRule());

        var forbidden = Snapshots.Text("secret", signals: new ClipboardPrivacySignals(ExcludeFromHistory: true, null));
        await service.IngestAsync(forbidden);

        Assert.Equal(0, announced);
    }

    [Fact]
    public async Task Duplicate_content_announces_the_change()
    {
        var existing = ClipboardEntry.Create(
            ClipContentType.Text, new ContentHash("h"), "hello", null, 5, SourceApplication.Unknown, capturedAt: Now.AddDays(-1));
        var repo = Substitute.For<IClipboardHistoryRepository>();
        repo.FindByHashAsync(Arg.Any<ContentHash>(), Arg.Any<CancellationToken>()).Returns(existing);
        var messenger = new WeakReferenceMessenger();
        var announced = 0;
        messenger.Register<HistoryChangedMessage>(this, (_, _) => announced++);
        var service = Build(repo, messenger);

        await service.IngestAsync(Snapshots.Text("hello"));

        Assert.Equal(1, announced);
    }

    private static ClipboardIngestionService Build(
        IClipboardHistoryRepository repository,
        IMessenger messenger,
        params ICaptureRule[] rules) =>
        new(new CaptureGate(rules), new FakeEncryptionService(), repository, new FixedClock(Now), messenger);
}
