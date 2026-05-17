using EpochsOfHumanity.Core.Prng;
using Xunit;

namespace EpochsOfHumanity.Tests.Core.Prng;

public class RngTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new Rng("paleolithic-2026");
        var b = new Rng("paleolithic-2026");

        for (var i = 0; i < 10_000; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
    }

    [Fact]
    public void DifferentSeeds_DivergeQuickly()
    {
        var a = new Rng("seed-a");
        var b = new Rng("seed-b");

        var mismatches = 0;
        for (var i = 0; i < 100; i++)
        {
            if (a.NextUInt64() != b.NextUInt64()) mismatches++;
        }
        // Some collisions possible but should be ≥ 99% different
        Assert.True(mismatches >= 99, $"Only {mismatches}/100 mismatches");
    }

    [Fact]
    public void Fork_WithSameName_ProducesSameChild()
    {
        var parent = new Rng("parent-seed");
        var child1 = parent.Fork("climate");
        var child2 = parent.Fork("climate");

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(child1.NextUInt64(), child2.NextUInt64());
        }
    }

    [Fact]
    public void Fork_WithDifferentNames_ProducesDifferentChildren()
    {
        var parent = new Rng("parent-seed");
        var climate = parent.Fork("climate");
        var ai = parent.Fork("ai");

        var mismatches = 0;
        for (var i = 0; i < 100; i++)
        {
            if (climate.NextUInt64() != ai.NextUInt64()) mismatches++;
        }
        Assert.True(mismatches >= 99, $"Only {mismatches}/100 mismatches between forks");
    }

    [Fact]
    public void Fork_DoesNotMutateParent()
    {
        var parent = new Rng("parent-seed");
        var beforeFork = parent.NextUInt64();

        parent.Fork("subsystem-x"); // should not advance parent

        // Recreate parent and skip one — same value as beforeFork's "next"
        var parentClone = new Rng("parent-seed");
        var first = parentClone.NextUInt64();
        Assert.Equal(beforeFork, first);

        var nextAfterFork = parent.NextUInt64();
        var nextClone = parentClone.NextUInt64();
        Assert.Equal(nextClone, nextAfterFork);
    }

    [Fact]
    public void NextDouble_StaysInUnitInterval()
    {
        var rng = new Rng("interval-test");
        for (var i = 0; i < 10_000; i++)
        {
            var d = rng.NextDouble();
            Assert.InRange(d, 0.0, 1.0);
            Assert.NotEqual(1.0, d); // exclusive upper bound
        }
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        var rng = new Rng("int-test");
        for (var i = 0; i < 10_000; i++)
        {
            var n = rng.NextInt(10, 20);
            Assert.InRange(n, 10, 19);
        }
    }

    [Fact]
    public void StableHash_IsStableAcrossInstances()
    {
        // The critical property: rerunning the program gives same hash.
        // We can only assert it's deterministic within this process,
        // but the algorithm (FNV-1a on UTF-8) is well-known stable.
        Assert.Equal(StableHash.Of("paleolithic"), StableHash.Of("paleolithic"));
        Assert.NotEqual(StableHash.Of("paleolithic"), StableHash.Of("mesolithic"));
    }
}
