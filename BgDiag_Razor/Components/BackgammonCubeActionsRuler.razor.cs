using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;
using Pill = BgDiag_Razor.Components.BackgammonCubeActions.Pill;

namespace BgDiag_Razor.Components;

/// <summary>
/// An inert copy of <see cref="BackgammonCubeActions"/>' pill row, for a host
/// to measure before any cube decision is on screen. Its width is the widest
/// the live row can take in the label form <see cref="ShortLabels"/> names,
/// under the fonts rendering wherever the host places it.
///
/// <para>
/// <b>Why it exists.</b> The umbrella's <c>SPEC-quiz-view.md</c> §4, "One
/// budget from the outset", has a host size its action row from one width
/// budget that holds the cube pills at their widest before the first cube
/// problem arrives. No live row can tell it that: the live row is labelled
/// at its decision, and which label the fourth answer takes is that
/// decision's reading. This copy takes no decision. Its labels are every
/// spelling the label home says an answer can take
/// (<see cref="CubeLabels.Spellings"/>), and it never labels an answer.
/// </para>
///
/// <para>
/// <b>Every state, so the fonts decide.</b> It draws one copy of the row for
/// each state the live row can show in the form: every combination of the
/// answers' spellings (today the fourth answer's two readings), each with
/// nothing selected and with each pill selected in turn, since the selected
/// pill is drawn heavier. It assumes no state is the widest. The copies
/// stack in one box sized to its content, so the box is exactly as wide as
/// the widest of them: the host measures this component's root and reads
/// nothing else. Its height is not a row's, because the copies stack.
/// </para>
///
/// <para>
/// <b>One markup source.</b> Each copy is drawn by
/// <see cref="BackgammonCubeActions"/>' own pill-row markup, the markup the
/// live row draws itself with, so each copy carries that component's
/// scoped-style attribute and its stylesheet reaches the copy's pills as it
/// reaches the live row's. A change to the pill's look therefore reaches both.
/// What the copy leaves out is the live row's wiring: the radio group's role
/// and name, the radios, and their events.
/// </para>
///
/// <para>
/// <b>Inert, and its box its own.</b> The root is hidden from assistive
/// technology (<c>aria-hidden</c>) and <c>inert</c>, and its stylesheet keeps
/// it unpainted and sized to its widest copy. Its class, <c>aria-hidden</c>
/// and <c>inert</c> are written after the host's attributes, so no host
/// attribute can undo them; and a host <c>class</c> or <c>style</c> is
/// refused outright (see <see cref="AdditionalAttributes"/>), since either
/// could only restyle the box whose width is the measurement.
/// It holds no input, no control, no name, no id and no event handler, so
/// nothing in it can take focus or a pointer, raise an event, or join the
/// live row's radio group. It is still laid out, which is what makes it
/// measurable: keeping it out of the page's flow, and from widening the
/// page, is the host's, as is placing it where it inherits the live row's
/// fonts.
/// </para>
/// </summary>
public partial class BackgammonCubeActionsRuler : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Parameters
    // -----------------------------------------------------------------------

    /// <summary>
    /// The label form to measure: <c>true</c> for the short labels,
    /// <c>false</c> (the default) for the full ones — the same choice, under
    /// the same name, as <see cref="BackgammonCubeActions.ShortLabels"/>. A
    /// host that needs both forms renders one ruler for each.
    /// </summary>
    [Parameter]
    public bool ShortLabels { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. a <c>data-</c> attribute
    /// a host finds the ruler by) splatted onto the root <c>div</c>
    /// (<c>bg-cube-actions-ruler</c>). They cannot undo the root's class,
    /// <c>aria-hidden</c> or <c>inert</c>, which come after them.
    ///
    /// <para>
    /// A <c>class</c> or a <c>style</c> is not accepted, in any letter case:
    /// the root's box is the measurement, and a host class beside the
    /// ruler's own, or an inline style, could only restyle it — stretch or
    /// shrink it, paint it — and the host would read a wrong width with no
    /// error. Either is refused with an <see cref="ArgumentException"/> whose
    /// <c>ParamName</c> is <c>AdditionalAttributes</c>, rather than dropped
    /// silently.
    /// </para>
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  The copies — one per state the live row can show in the form
    // -----------------------------------------------------------------------

    private ImmutableArray<ImmutableArray<Pill>> _rows = [];

    /// <summary>
    /// Refuses a host attribute that would restyle the root's box, then draws
    /// the copies for the form <see cref="ShortLabels"/> names.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="AdditionalAttributes"/> holds a <c>class</c> or a
    /// <c>style</c>.
    /// </exception>
    protected override void OnParametersSet()
    {
        if (AdditionalAttributes?.Keys.FirstOrDefault(IsBoxStyling) is { } refused)
        {
            throw new ArgumentException(
                $"BackgammonCubeActionsRuler takes no `{refused}` from a host: its root's box is the " +
                "measurement, and a host class or style could only restyle it. Find the ruler by a " +
                "data- attribute instead.",
                nameof(AdditionalAttributes));
        }

        _rows = RowsIn(ShortLabels);
    }

    /// <summary>
    /// Whether a host attribute named <paramref name="name"/> would restyle
    /// the root's box: a <c>class</c> or a <c>style</c>, whose names HTML
    /// reads in any letter case.
    /// </summary>
    private static bool IsBoxStyling(string name) =>
        name.Equals("class", StringComparison.OrdinalIgnoreCase)
        || name.Equals("style", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One copy of the row for each state the live row can show in the form:
    /// each combination of spellings (<see cref="SpellingCombinations"/>)
    /// under each selection (<see cref="Selections"/>).
    /// </summary>
    private static ImmutableArray<ImmutableArray<Pill>> RowsIn(bool shortLabels) =>
        [.. SpellingCombinations().SelectMany(spellings =>
            Selections().Select(selection => Row(spellings, selection, shortLabels)))];

    /// <summary>
    /// Every way the offered answers can be spelled together: one of each
    /// answer's spellings, in the offered order, for every combination of
    /// them. The label home says which spellings an answer can take, not
    /// which of them a decision takes together, so every combination is
    /// drawn; today only the fourth answer has two, so there are two.
    /// </summary>
    private static IEnumerable<ImmutableArray<CubeAnswerSpelling>> SpellingCombinations()
    {
        IEnumerable<ImmutableArray<CubeAnswerSpelling>> combinations = [ImmutableArray<CubeAnswerSpelling>.Empty];
        foreach (var answer in BackgammonCubeActions.OfferedAnswers)
        {
            var spellings = CubeLabels.Spellings(answer);
            combinations = [.. combinations.SelectMany(combination => spellings.Select(combination.Add))];
        }
        return combinations;
    }

    /// <summary>
    /// Every selection the live row can show: nothing selected, then each
    /// offered answer selected in turn.
    /// </summary>
    private static IEnumerable<CubeAnswer?> Selections() =>
        BackgammonCubeActions.OfferedAnswers.Select(answer => (CubeAnswer?)answer).Prepend(null);

    /// <summary>
    /// One copy: each offered answer's pill in its spelling from
    /// <paramref name="spellings"/>, captioned in the form, with its full
    /// label as the tooltip, as the live row's is, drawn selected where it
    /// is <paramref name="selection"/>, and with no radio.
    /// </summary>
    private static ImmutableArray<Pill> Row(
        ImmutableArray<CubeAnswerSpelling> spellings, CubeAnswer? selection, bool shortLabels) =>
        [.. BackgammonCubeActions.OfferedAnswers.Zip(spellings, (answer, spelling) =>
            new Pill(CaptionOf(spelling, shortLabels), spelling.Full, answer == selection, Radio: null))];

    /// <summary>
    /// The caption <paramref name="spelling"/> gives a pill in the form: its
    /// short label in the short form, its full label otherwise.
    /// </summary>
    private static string CaptionOf(CubeAnswerSpelling spelling, bool shortLabels) =>
        shortLabels ? spelling.Short : spelling.Full;
}
