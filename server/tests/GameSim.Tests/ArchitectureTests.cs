using System.Text.RegularExpressions;

namespace GameSim.Tests;

/// <summary>
/// Enforces the architecture rules in AGENTS.md mechanically. A rule that only
/// lives in a document gets broken by the third contributor who never read it —
/// and the contributors here are frequently unattended agents.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenInSim =
    [
        "new Random(",
        "Random.Shared",
        "Guid.NewGuid",
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTime.Today",
        "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow",
        "Environment.TickCount",
        "Stopwatch",
        "Parallel.",
        ".AsParallel(",
        "Task.Run",

        // Thread timing. A sim that sleeps or delays is reading a wall clock by
        // another name: the tick is the only clock, and it is the caller's to
        // advance.
        "Task.Delay",
        "Thread.Sleep",
    ];

    [Fact]
    public void GameSim_project_has_no_dependencies_at_all()
    {
        string csproj = File.ReadAllText(Path.Combine(ServerRoot, "src", "GameSim", "GameSim.csproj"));

        Assert.DoesNotContain("<PackageReference", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void GameSim_assembly_does_not_reference_the_web_or_data_stack()
    {
        string[] forbidden =
        [
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions.Hosting",
            "System.Net.Http",
        ];

        string[] violations =
        [
            .. typeof(Rng).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .Where(name => forbidden.Any(f => name.StartsWith(f, StringComparison.Ordinal)))
        ];

        Assert.Empty(violations);
    }

    [Fact]
    public void GameSim_source_contains_no_non_deterministic_calls()
    {
        var violations = new List<string>();

        foreach (string file in Directory.EnumerateFiles(
                     Path.Combine(ServerRoot, "src", "GameSim"), "*.cs", SearchOption.AllDirectories))
        {
            string code = StripComments(File.ReadAllText(file));

            foreach (string banned in ForbiddenInSim)
            {
                if (code.Contains(banned, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetFileName(file)}: {banned}");
                }
            }
        }

        Assert.Empty(violations);
    }

    /// <summary>
    /// Comments legitimately name the banned APIs in order to warn about them,
    /// so they must not count as usage.
    /// </summary>
    private static string StripComments(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        code = Regex.Replace(code, @"//[^\n]*", string.Empty);
        return code;
    }

    private static string ServerRoot => field ??= FindServerRoot();

    private static string FindServerRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AutoChess.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate AutoChess.slnx above {AppContext.BaseDirectory}.");
    }
}
