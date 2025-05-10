namespace boombuler.Linker.Module;

using System.Collections.Frozen;
using System.Numerics;

public record Section<TAddr> 
    where TAddr: struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>
{
    public required RegionName Region { get; init; }

    public FrozenDictionary<SymbolId, TAddr> SymbolAddresses { get; init; } = FrozenDictionary<SymbolId, TAddr>.Empty;

    public TAddr? Origin { get; init; } = null;
    public TAddr? Alignment { get; init; } = null;

    public TAddr Size { get; init; } = TAddr.Zero;

    public ReadOnlyMemory<byte> Data { get; init; } = ReadOnlyMemory<byte>.Empty;
}
