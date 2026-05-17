using System.Collections.Generic;
using EpochsOfHumanity.Core.Geography;

namespace EpochsOfHumanity.Sim.Characters;

/// <summary>
/// Read-only list of all tribes at game start, with lookup by id and by home hex.
/// </summary>
public sealed class TribeRegistry
{
    private readonly Dictionary<string, Tribe> _byId;
    private readonly Dictionary<HexCoord, Tribe> _byHex;
    private readonly Tribe[] _ordered;

    public TribeRegistry(IEnumerable<Tribe> tribes)
    {
        _byId = new Dictionary<string, Tribe>(System.StringComparer.Ordinal);
        _byHex = new Dictionary<HexCoord, Tribe>();
        var temp = new List<Tribe>();
        foreach (var t in tribes)
        {
            if (!_byId.TryAdd(t.Id, t))
                throw new System.ArgumentException($"Duplicate tribe id: '{t.Id}'");
            if (!_byHex.TryAdd(t.HomeHex, t))
                throw new System.ArgumentException($"Two tribes at same hex {t.HomeHex}: '{_byHex[t.HomeHex].Id}' and '{t.Id}'");
            temp.Add(t);
        }
        temp.Sort((a, b) => System.StringComparer.Ordinal.Compare(a.Id, b.Id));
        _ordered = temp.ToArray();
    }

    public int Count => _ordered.Length;

    public Tribe Get(string id) => _byId[id];
    public Tribe? AtHex(HexCoord hex) => _byHex.TryGetValue(hex, out var t) ? t : null;

    public IReadOnlyList<Tribe> All => _ordered;

    public Tribe Player => System.Linq.Enumerable.First(_ordered, t => t.IsPlayerControlled);
}
