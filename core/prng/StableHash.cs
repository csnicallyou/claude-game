namespace EpochsOfHumanity.Core.Prng;

/// <summary>
/// Detereministic, version-stable hash for strings.
/// </summary>
/// <remarks>
/// We cannot use <see cref="string.GetHashCode()"/> because .NET randomizes it
/// per-process for hash-flooding protection (Law 1: determinism, see CLAUDE.md §4).
/// This is FNV-1a 64-bit on UTF-8 bytes — simple, stable, deterministic
/// across runs, machines and .NET versions.
/// </remarks>
public static class StableHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public static ulong Of(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        var hash = FnvOffsetBasis;
        for (var i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= FnvPrime;
        }
        return hash;
    }

    public static ulong Combine(ulong a, ulong b)
    {
        // SplitMix64-style combination, deterministic
        var z = a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2));
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
