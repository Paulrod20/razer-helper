namespace RazerHelper.Tests.Diagnostics;

/// <summary>
/// A guard against garbled text. Reading a UTF-8 file in the wrong encoding and
/// saving it back turns the degree sign and the bullet into two or three odd
/// characters each, and a test that contains the same garbled text passes
/// anyway. So this checks the files themselves. Special characters in the code
/// are written as \u escapes, which no encoding mix-up can damage.
/// </summary>
public class SourceTextTests
{
    // The leftovers of that mistake: what remains of the degree sign and the
    // middle dot (A with a circumflex), of accented letters (A with a tilde), and
    // of bullets, dashes and curly quotes (a with a circumflex, then a euro sign).
    private static readonly string[] GarbledMarkers =
    [
        "\u00C2",
        "\u00C3",
        "\u00E2\u20AC"
    ];

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> TextFiles()
    {
        var root = FindRepositoryRoot();
        var separator = Path.DirectorySeparatorChar;

        return Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}") && !path.Contains($"{separator}bin{separator}"))
            .Where(path => Path.GetFileName(path) != nameof(SourceTextTests) + ".cs")
            .Append(Path.Combine(root, "README.md"));
    }

    [Fact]
    public void TheSourceIsFound_SoTheGuardBelowIsNotVacuous() =>
        Assert.True(TextFiles().Count() > 50);

    [Fact]
    public void NoSourceFileOrTheReadmeContainsGarbledText()
    {
        var garbled = new List<string>();

        foreach (var path in TextFiles())
        {
            var lines = File.ReadAllLines(path);

            for (var index = 0; index < lines.Length; index++)
            {
                if (GarbledMarkers.Any(marker => lines[index].Contains(marker)))
                    garbled.Add($"{Path.GetFileName(path)}:{index + 1}");
            }
        }

        Assert.True(garbled.Count == 0, "Garbled characters found (a file was saved in the wrong encoding): " + string.Join(", ", garbled));
    }

    [Fact]
    public void TheDegreeSignInTemperatures_IsARealDegreeSign()
    {
        var text = RazerHelper.Core.Services.TemperatureText.Format(null, 41.2);

        Assert.Equal("GPU: 41°C", text);
        Assert.Equal(1, text.Count(character => character == '°'));
        Assert.DoesNotContain('Â', text);
    }

    [Fact]
    public void TheBulletsInTheGpuQuestion_AreRealBullets()
    {
        var apps = new[] { new RazerHelper.Core.Models.DgpuApp(1, "blender", 100L * 1024 * 1024, RazerHelper.Core.Models.DgpuAppVerdict.Close) };

        var text = RazerHelper.Core.Services.DgpuText.BuildConfirmation(apps);

        Assert.Contains("• blender", text);
        Assert.DoesNotContain("â", text);
    }
}
