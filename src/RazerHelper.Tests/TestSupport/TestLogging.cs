using System.Runtime.CompilerServices;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Tests.TestSupport;

internal static class TestLogging
{
    // Runs once when the test assembly loads, before any test. Code under test
    // logs failures on purpose; without this those lines would land in the
    // real razerhelper.log in the developer's profile.
    [ModuleInitializer]
    internal static void RedirectLogToTempFolder() =>
        AppLog.RedirectTo(Path.Combine(Path.GetTempPath(), "RazerHelper.Tests", "logs"));
}
