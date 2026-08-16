using System;
using System.IO;
using System.Reflection;
using Bclone.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The <c>VERSION</c> file is the single source of version truth — and now something reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ METHODOLOGY §5 has called <c>VERSION</c> the single source of truth since Phase 0 and
/// recorded, in the same paragraph, that <em>"nothing reads it yet"</em>.</b> A single source of
/// truth with no consumers is a text file, and it is D98's rule one level up: *a number that is
/// always zero is a lie waiting to be found*. It was listed as one of Phase 2's release blockers
/// (`DESIGN.md §4`, DoD item 4) for exactly that reason.
/// </para>
/// <para>
/// <b>This is the guard that makes the wiring real rather than incidental.</b>
/// <c>Directory.Build.props</c> reads the file into <c>$(Version)</c>; if that is ever removed,
/// refactored away, or quietly broken by an MSBuild change, every assembly silently reverts to
/// .NET's default <c>1.0.0.0</c> — and nobody would notice until a release shipped carrying the
/// wrong number. **A build failure is a much better place to find that out than a tag.**
/// </para>
/// </remarks>
public sealed class VersionTests
{
    private readonly ITestOutputHelper _output;

    public VersionTests(ITestOutputHelper output) => _output = output;

    /// <summary>⭐ The built assembly carries the number written in <c>VERSION</c>.</summary>
    [Fact]
    public void TheBuiltAssemblyCarriesTheVersionFilesNumber()
    {
        string declared = ReadTheVersionFile();
        Version? built = typeof(SimWorld).Assembly.GetName().Version;

        _output.WriteLine($"VERSION says {declared}; the assembly says {built}");

        Assert.NotNull(built);

        // The file states three parts and .NET stores four, so the comparison is on the three
        // that were stated rather than on a revision nobody wrote down.
        var wanted = Version.Parse(declared);
        Assert.Equal(wanted.Major, built!.Major);
        Assert.Equal(wanted.Minor, built.Minor);
        Assert.Equal(wanted.Build, built.Build);
    }

    /// <summary>
    /// The anti-vacuity companion (D7): it is not just any number, and it is not the default.
    /// </summary>
    /// <remarks>
    /// Without this the guard above passes trivially the day somebody writes <c>1.0.0</c> into
    /// the file — which is precisely the value an unwired build reports. **The one number this
    /// test must never accept as evidence of wiring is the one .NET hands out for free.**
    /// </remarks>
    [Fact]
    public void AndItIsNotSimplyDotNetsDefault()
    {
        string declared = ReadTheVersionFile();
        _output.WriteLine($"VERSION says {declared}");

        Assert.NotEqual("1.0.0", declared);
    }

    /// <summary>Semantic versioning, asserted rather than assumed (METHODOLOGY §5).</summary>
    [Fact]
    public void TheVersionFileIsSemanticVersioning()
    {
        string declared = ReadTheVersionFile();

        Assert.Matches(@"^\d+\.\d+\.\d+$", declared);
    }

    // ---------------------------------------------------------------

    /// <summary>The repo's <c>VERSION</c>, found by walking up from the test binary.</summary>
    /// <remarks>
    /// Walked rather than hard-coded, because the depth from <c>bin/Debug/net8.0</c> to the repo
    /// root is an artefact of the build layout and has changed once already.
    /// </remarks>
    private static string ReadTheVersionFile()
    {
        DirectoryInfo? here = new(AppContext.BaseDirectory);

        while (here is not null)
        {
            string candidate = Path.Combine(here.FullName, "VERSION");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate).Trim();
            }

            here = here.Parent;
        }

        throw new Xunit.Sdk.XunitException(
            $"No VERSION file above {AppContext.BaseDirectory} — the single source of version "
            + "truth has gone missing.");
    }
}
