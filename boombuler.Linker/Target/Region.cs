namespace boombuler.Linker.Target;

using System.Numerics;

public record Region<TAddr>(RegionName Name, TAddr? StartAddress = null, bool Output = true) where TAddr: struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>;