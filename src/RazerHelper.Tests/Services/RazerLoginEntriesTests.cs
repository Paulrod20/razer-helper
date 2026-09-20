using Microsoft.Win32;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class StartupApprovalTests
{
    [Theory]
    [InlineData(0x03)]
    [InlineData(0x07)]
    public void TheDocumentedDisabledValues_AreDisabled(byte first) =>
        Assert.True(StartupApproval.IsDisabled([first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));

    [Theory]
    [InlineData(0x02)]
    [InlineData(0x06)]
    [InlineData(0x01)] // What Razer's own entry carries: not a disabled value, and it does start.
    [InlineData(0x00)]
    public void EverythingElse_IsEnabled(byte first) =>
        Assert.False(StartupApproval.IsDisabled([first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));

    [Fact]
    public void NoValueAtAll_IsEnabled() =>
        Assert.False(StartupApproval.IsDisabled(null));

    [Fact]
    public void AnEmptyValue_IsEnabled() =>
        Assert.False(StartupApproval.IsDisabled([]));

    [Fact]
    public void TheDisabledValue_IsTwelveBytes_LikeTaskManagersOwn()
    {
        var flag = StartupApproval.CreateDisabled(new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(12, flag.Length);
        Assert.True(StartupApproval.IsDisabled(flag));
        Assert.Equal(new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc(), BitConverter.ToInt64(flag, 4));
    }

    [Fact]
    public void ATextRoundTrip_KeepsEveryByte()
    {
        byte[] original = [0x01, 0x00, 0x00, 0x00, 0x30, 0xEF, 0x13, 0x69, 0xD3, 0x43, 0xDD, 0x01];

        Assert.Equal(original, StartupApproval.FromText(StartupApproval.ToText(original)));
    }

    [Fact]
    public void NoValue_IsAnEmptyText_AndBack()
    {
        Assert.Equal(string.Empty, StartupApproval.ToText(null));
        Assert.Null(StartupApproval.FromText(string.Empty));
        Assert.Null(StartupApproval.FromText(null));
    }

    [Fact]
    public void TextThatIsNotValid_IsTreatedAsNoValue() =>
        Assert.Null(StartupApproval.FromText("not hex"));
}

public class RazerSoftwarePathsTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Razer\RazerAppEngine\RazerAppEngine.exe")]
    [InlineData(@"""C:\Program Files\Razer\RazerAppEngine\RazerAppEngine.exe"" --autoStart=1")]
    [InlineData(@"C:\Users\Pauly\AppData\Local\Razer\RazerAppEngine\User Data\Apps\Common\LampArray\razerwdl.exe")]
    [InlineData(@"c:\program files (x86)\razer\synapse3\x.exe")]
    [InlineData("C:/Program Files/Razer/App/x.exe")]
    public void ProgramsInsideARazerFolder_Match(string path) =>
        Assert.True(RazerSoftwarePaths.IsInRazerFolder(path));

    [Theory]
    [InlineData(@"C:\Users\Pauly\Developer\Apps\razer-helper\src\RazerHelper\bin\Debug\RazerHelper.exe")] // This app.
    [InlineData(@"C:\Program Files\NotRazer\app.exe")]
    [InlineData(@"C:\Program Files\Razerlike\app.exe")]
    [InlineData(@"C:\Program Files\Steam\steam.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void EverythingElse_DoesNot(string? path) =>
        Assert.False(RazerSoftwarePaths.IsInRazerFolder(path));
}

// Uses throwaway keys under HKCU\Software\RazerHelperTests, never the real Run key.
public sealed class RunKeyLoginEntriesTests : IDisposable
{
    // Its own uniquely named root, deleted on its own: test classes run in parallel.
    private readonly string _root = $@"Software\RazerHelperTests_{Guid.NewGuid():N}";
    private string RunPath => $@"{_root}\Run";
    private string ApprovedPath => $@"{_root}\Approved";

    public void Dispose() =>
        Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);

    private RunKeyLoginEntries Create() => new("RazerHelper", RunPath, ApprovedPath);

    private void AddRunEntry(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunPath);
        key.SetValue(name, command);
    }

    private void SetApproval(string name, params byte[] bytes)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ApprovedPath);
        key.SetValue(name, bytes, RegistryValueKind.Binary);
    }

    private byte[]? GetApproval(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedPath);
        return key?.GetValue(name) as byte[];
    }

    private void AddTypicalRunKey()
    {
        AddRunEntry("OneDrive", @"""C:\Users\Pauly\AppData\Local\Microsoft\OneDrive\OneDrive.exe"" /background");
        AddRunEntry("RazerAppEngine", @"""C:\Program Files\Razer\RazerAppEngine\RazerAppEngine.exe"" --autoStart=1");
        AddRunEntry("RazerHelper", @"""C:\Users\Pauly\Developer\Apps\razer-helper\bin\RazerHelper.exe""");
        AddRunEntry("Steam", @"""C:\Program Files (x86)\Steam\steam.exe"" -silent");
    }

    [Fact]
    public void FindsOnlyRazersOwnEntries_NotOtherProgramsAndNotThisApp()
    {
        AddTypicalRunKey();

        var found = Create().FindRazerEntries();

        Assert.Equal(["RazerAppEngine"], found.Select(entry => entry.Name));
    }

    [Fact]
    public void ThisAppsOwnEntry_IsNeverFound_EvenIfItsPathWereInARazerFolder()
    {
        AddRunEntry("RazerHelper", @"C:\Program Files\Razer\RazerHelper.exe");

        Assert.Empty(Create().FindRazerEntries());
    }

    [Fact]
    public void AnEntryWithNoFlag_IsEnabled()
    {
        AddTypicalRunKey();

        Assert.True(Create().FindRazerEntries().Single().IsEnabled);
    }

    [Fact]
    public void AnEntryWithRazersOwnFlag_IsStillEnabled()
    {
        AddTypicalRunKey();
        SetApproval("RazerAppEngine", 0x01, 0, 0, 0, 0x30, 0xEF, 0x13, 0x69, 0xD3, 0x43, 0xDD, 0x01);

        Assert.True(Create().FindRazerEntries().Single().IsEnabled);
    }

    [Fact]
    public void Disable_SwitchesItOff_LikeTaskManager_WithoutTouchingTheEntryItself()
    {
        AddTypicalRunKey();
        var entries = Create();

        entries.Disable("RazerAppEngine");

        Assert.False(entries.FindRazerEntries().Single().IsEnabled);
        Assert.True(StartupApproval.IsDisabled(GetApproval("RazerAppEngine")));

        using var runKey = Registry.CurrentUser.OpenSubKey(RunPath);
        Assert.NotNull(runKey!.GetValue("RazerAppEngine")); // Razer's own entry is still there.
    }

    [Fact]
    public void Disable_LeavesEveryOtherEntryAlone()
    {
        AddTypicalRunKey();

        Create().Disable("RazerAppEngine");

        Assert.Null(GetApproval("OneDrive"));
        Assert.Null(GetApproval("Steam"));
        Assert.Null(GetApproval("RazerHelper"));
    }

    [Fact]
    public void ReadApproval_ReturnsWhatWasThere_OrEmpty()
    {
        AddTypicalRunKey();
        var entries = Create();
        Assert.Equal(string.Empty, entries.ReadApproval("RazerAppEngine"));

        SetApproval("RazerAppEngine", 0x02, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8);

        Assert.Equal("020000000102030405060708", entries.ReadApproval("RazerAppEngine"));
    }

    [Fact]
    public void Restore_PutsBackExactlyTheOriginalBytes()
    {
        AddTypicalRunKey();
        byte[] original = [0x01, 0, 0, 0, 0x30, 0xEF, 0x13, 0x69, 0xD3, 0x43, 0xDD, 0x01];
        SetApproval("RazerAppEngine", original);
        var entries = Create();
        var before = entries.ReadApproval("RazerAppEngine");

        entries.Disable("RazerAppEngine");
        entries.Restore("RazerAppEngine", before);

        Assert.Equal(original, GetApproval("RazerAppEngine"));
    }

    [Fact]
    public void Restore_WhenThereWasNoFlag_RemovesOurs()
    {
        AddTypicalRunKey();
        var entries = Create();

        entries.Disable("RazerAppEngine");
        entries.Restore("RazerAppEngine", string.Empty);

        Assert.Null(GetApproval("RazerAppEngine"));
        Assert.True(entries.FindRazerEntries().Single().IsEnabled);
    }

    [Fact]
    public void WithNoRunKeyAtAll_NothingIsFound_AndNothingBreaks() =>
        Assert.Empty(Create().FindRazerEntries());
}

public class RazerLoginEntryManagerTests
{
    private static readonly IReadOnlyDictionary<string, string> NoRecord = new Dictionary<string, string>();

    [Fact]
    public void Stopping_SwitchesOffAnEnabledEntry_AndRecordsWhatItWas()
    {
        var entries = new FakeLoginEntries().Add("RazerAppEngine", approval: "020000000000000000000000");
        var manager = new RazerLoginEntryManager(entries);

        var change = manager.DisableAll(NoRecord, _ => { });

        Assert.True(change.IsSuccess);
        Assert.Equal("020000000000000000000000", change.Record["RazerAppEngine"]);
        Assert.False(manager.Find().Single().IsEnabled);
    }

    [Fact]
    public void TheRecordIsSavedBeforeAnythingIsChanged()
    {
        var entries = new FakeLoginEntries().Add("RazerAppEngine", approval: "020000000000000000000000");
        var manager = new RazerLoginEntryManager(entries);
        var saves = 0;
        var actionsWhenSaved = new List<string>();
        IReadOnlyDictionary<string, string>? saved = null;

        manager.DisableAll(NoRecord, record =>
        {
            saves++;
            saved = record;
            actionsWhenSaved.AddRange(entries.Actions);
        });

        Assert.Equal(1, saves); // It was saved, and once.
        Assert.Equal("020000000000000000000000", saved!["RazerAppEngine"]);
        Assert.Empty(actionsWhenSaved); // Nothing had been disabled yet when the record was written.
        Assert.Single(entries.Actions);
    }

    [Fact]
    public void AnEntryThatIsAlreadyOff_IsLeftAlone_AndNotRecorded()
    {
        var off = StartupApproval.ToText(StartupApproval.CreateDisabled(DateTime.UtcNow));
        var entries = new FakeLoginEntries().Add("RazerAppEngine", approval: off);

        var change = new RazerLoginEntryManager(entries).DisableAll(NoRecord, _ => { });

        Assert.Empty(entries.Actions);
        Assert.Empty(change.Record);
    }

    [Fact]
    public void AnEarlierRecord_IsNeverOverwrittenByOurOwnDisabledFlag()
    {
        // A second Stop after a partly failed first one must keep the original.
        var entries = new FakeLoginEntries().Add("RazerAppEngine", approval: "AAAA");
        var earlier = new Dictionary<string, string> { ["RazerAppEngine"] = "020000000000000000000000" };

        var change = new RazerLoginEntryManager(entries).DisableAll(earlier, _ => { });

        Assert.Equal("020000000000000000000000", change.Record["RazerAppEngine"]);
    }

    [Fact]
    public void OneEntryFailing_DoesNotStopTheOthers_AndIsReported()
    {
        var entries = new FakeLoginEntries().Add("RazerAppEngine").Add("RazerOther");
        entries.Failing.Add("RazerAppEngine");

        var change = new RazerLoginEntryManager(entries).DisableAll(NoRecord, _ => { });

        Assert.False(change.IsSuccess);
        Assert.Contains(change.Failures, failure => failure.StartsWith("RazerAppEngine"));
        Assert.Equal(["disable:RazerOther"], entries.Actions);
    }

    [Fact]
    public void AnEntryThatCouldNotBeRead_IsNeverSwitchedOff_SoNothingIsLeftOffWithoutARecord()
    {
        var entries = new FakeLoginEntries().Add("RazerAppEngine");
        entries.Failing.Add("RazerAppEngine");

        new RazerLoginEntryManager(entries).DisableAll(NoRecord, _ => { });

        Assert.DoesNotContain("disable:RazerAppEngine", entries.Actions);
    }

    [Fact]
    public void Restoring_PutsBackEachRecordedFlag()
    {
        var entries = new FakeLoginEntries().Add("RazerAppEngine");
        var manager = new RazerLoginEntryManager(entries);
        var change = manager.DisableAll(NoRecord, _ => { });

        var failures = manager.RestoreAll(change.Record);

        Assert.Empty(failures);
        Assert.True(manager.Find().Single().IsEnabled);
        Assert.Equal(string.Empty, entries.ApprovalOf("RazerAppEngine"));
    }

    [Fact]
    public void Restoring_AnEntryThatNoLongerExists_IsSkipped()
    {
        var entries = new FakeLoginEntries();
        var record = new Dictionary<string, string> { ["RazerAppEngine"] = string.Empty };

        var failures = new RazerLoginEntryManager(entries).RestoreAll(record);

        Assert.Empty(failures);
        Assert.Empty(entries.Actions);
    }

    [Fact]
    public void Restoring_OneFailure_DoesNotStopTheOthers()
    {
        var entries = new FakeLoginEntries().Add("RazerAppEngine").Add("RazerOther");
        entries.Failing.Add("RazerAppEngine");
        var record = new Dictionary<string, string> { ["RazerAppEngine"] = string.Empty, ["RazerOther"] = string.Empty };

        var failures = new RazerLoginEntryManager(entries).RestoreAll(record);

        Assert.Single(failures);
        Assert.Equal(["restore:RazerOther"], entries.Actions);
    }
}
