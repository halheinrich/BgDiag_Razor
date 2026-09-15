using System.Reflection;
using BgDiag_Razor.Components;

namespace BgDiag_Razor.Tests;

// The premise of this library's half of the trim gate
// (halheinrich/backgammon#197, after XgFilter_Razor's pin for
// halheinrich/backgammon#193). Whether the code is trim-safe is the analyzer's
// verdict, and the build enforces that verdict under TreatWarningsAsErrors;
// no test restates it. What this pins is the declaration the verdict hangs
// on, so that removing IsTrimmable from the csproj fails a test instead of
// silently letting a trim-unsafe construct travel on to BgQuiz's publish.
public class BgDiagRazorTrimPostureTests
{
    // The SDK stamps IsTrimmable into the built assembly as metadata, the one
    // trace of the csproj setting a test can read. EnableTrimAnalyzer leaves
    // no such trace and is not pinned here; the build itself is its check,
    // since any trim-unsafe call it would flag fails compilation.
    [Fact]
    public void TheAssembly_DeclaresItselfTrimmable()
    {
        Assert.Contains(
            typeof(BackgammonDiagram).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            a => a.Key == "IsTrimmable" && a.Value == "True");
    }
}
