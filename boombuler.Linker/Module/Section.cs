namespace boombuler.Linker.Module;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using boombuler.Linker.Patches;

public record Section<TAddr> 
    where TAddr: struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>
{
    public required RegionName Region { get; init; }

    public FrozenDictionary<SymbolId, TAddr> SymbolAddresses { get; init; } = FrozenDictionary<SymbolId, TAddr>.Empty;

    public TAddr? Origin { get; init; } = null;
    public TAddr Alignment { get; init; } = TAddr.One;

    public TAddr Size { get; init; } = TAddr.Zero;

    public ReadOnlyMemory<byte> Data { get; init; } = ReadOnlyMemory<byte>.Empty;

    public ImmutableArray<Patch<TAddr>> Patches { get; init; } = ImmutableArray<Patch<TAddr>>.Empty;
}
