using Microsoft.Win32;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

// Uses throwaway keys under HKCU\Software\RazerHelperTests, never the real Run key.
public sealed class RunKeyStartupRegistrationTests : IDisposable
{
    private const string ExePath = @"C:\Program Files\Razer Helper\RazerHelper.exe";

    private readonly string _root = $@"Software\RazerHelperTests\{Guid.NewGuid():N}";
    private string RunPath => $@"{_root}\Run";
    private string ApprovedPath => $@"{_root}\Approved";

    private RunKeyStartupRegistration Create() =>
        new(ExePath, "RazerHelperTest", RunPath, ApprovedPath);

    public void Dispose() =>
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\RazerHelperTests", throwOnMissingSubKey: false);

    [Fact]
    public void IsEnabled_IsFalseWhenNothingIsRegistered() =>
        Assert.False(Create().IsEnabled);

    [Fact]
    public void SetEnabled_WritesTheQuotedPath()
    {
        Create().SetEnabled(true);

        using var key = Registry.CurrentUser.OpenSubKey(RunPath);
        Assert.Equal($"\"{ExePath}\"", key!.GetValue("RazerHelperTest"));
        Assert.True(Create().IsEnabled);
    }

    [Fact]
    public void SetEnabled_False_RemovesTheEntry()
    {
        var registration = Create();
        registration.SetEnabled(true);

        registration.SetEnabled(false);

        Assert.False(registration.IsEnabled);
    }

    [Fact]
    public void SetEnabled_False_WhenNeverEnabled_DoesNotThrow() =>
        Create().SetEnabled(false);

    [Fact]
    public void IsEnabled_IsFalseWhenDisabledInTaskManager()
    {
        var registration = Create();
        registration.SetEnabled(true);
        WriteApproved(0x03);

        Assert.False(registration.IsEnabled);
    }

    [Fact]
    public void IsEnabled_IsTrueWhenTaskManagerMarksItEnabled()
    {
        var registration = Create();
        registration.SetEnabled(true);
        WriteApproved(0x02);

        Assert.True(registration.IsEnabled);
    }

    [Fact]
    public void SetEnabled_True_ClearsATaskManagerDisable()
    {
        var registration = Create();
        registration.SetEnabled(true);
        WriteApproved(0x03);

        registration.SetEnabled(true);

        Assert.True(registration.IsEnabled);
    }

    private void WriteApproved(byte firstByte)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ApprovedPath);
        key.SetValue("RazerHelperTest", new byte[] { firstByte, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
    }
}
