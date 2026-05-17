namespace EpochsOfHumanity.Core.Era;

/// <summary>
/// Time range in years BP (Before Present, archaeological convention).
/// </summary>
/// <remarks>
/// In BP, "older" = larger number. So StartYearBP > EndYearBP makes the range
/// "from older to younger". e.g. Paleolithic = (45_000, 10_000).
/// </remarks>
public readonly record struct TimeRange(int StartYearBP, int EndYearBP)
{
    public bool Contains(int yearBP) => yearBP <= StartYearBP && yearBP >= EndYearBP;

    public int DurationYears => StartYearBP - EndYearBP;
}
