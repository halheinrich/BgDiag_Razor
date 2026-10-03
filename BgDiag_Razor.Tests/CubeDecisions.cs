using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

namespace BgDiag_Razor.Tests;

/// <summary>
/// The cube decisions the cube-row tests answer: one where gammons are
/// possible and one where they are not, built by the producer's
/// <see cref="TestRecords"/>. Shared by the live row's tests and its inert
/// copy's, which compare the two rows at the same decisions.
/// </summary>
internal static class CubeDecisions
{
    /// <summary>A cube decision where gammons are possible: the fourth answer reads Too good.</summary>
    public static readonly CubeDecision WithGammons = DecisionWhereGammons(possible: true);

    /// <summary>A cube decision where gammons are not possible: the fourth answer reads No double / Pass.</summary>
    public static readonly CubeDecision WithoutGammons = DecisionWhereGammons(possible: false);

    /// <summary>The decision where gammons are, or are not, possible.</summary>
    public static CubeDecision DecisionAt(bool gammonsPossible) =>
        gammonsPossible ? WithGammons : WithoutGammons;

    /// <summary>
    /// A cube decision where gammons are or are not possible. The two differ
    /// only in the gammon fact: both are a money session with the cube
    /// centred, and only the Jacoby rule, which closes gammons at a centred
    /// cube, differs. The fact is the producer's
    /// (<see cref="CubeDecision.GammonsPossible"/>); the fixture refuses to
    /// hand a test a decision that disagrees with what it asked for.
    /// </summary>
    private static CubeDecision DecisionWhereGammons(bool possible)
    {
        var decision = TestRecords.Cube(
            position: TestRecords.Position(session: TestRecords.MoneySession(isJacoby: !possible)));
        if (decision.GammonsPossible != possible)
            throw new InvalidOperationException(
                $"The fixture asked for gammons possible = {possible}; the record says {decision.GammonsPossible}.");
        return decision;
    }
}
