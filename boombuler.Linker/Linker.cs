namespace boombuler.Linker;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Numerics;
using boombuler.Linker.Module;
using boombuler.Linker.Patches;
using boombuler.Linker.Target;

public class Linker<TAddr>
    where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>, 
                  IComparable<TAddr>, IComparisonOperators<TAddr, TAddr, bool>,
                  IModulusOperators<TAddr, TAddr, TAddr>
{
    private readonly ITargetConfiguration<TAddr> fTargetSystem;
    private readonly FrozenDictionary<Region<TAddr>, TAddr> fRegionEnd;

    private static readonly PatchRuntime<TAddr> StaticPatchRuntime =
        new PatchRuntime<TAddr>(s => throw new InvalidOperationException($"Unable to resolve symbol {s}")); 

    class ResolvedSection
    {
        public required Module<TAddr> Module { get; init; }
        public required Section<TAddr> Section { get; init; }
        public TAddr? Origin { get; set; }
    }

    public Linker(ITargetConfiguration<TAddr> targetSystem)
    {
        fTargetSystem = targetSystem ?? throw new ArgumentNullException(nameof(targetSystem));

        var endAddr = TAddr.Zero - TAddr.One;
        var endAddrs = new Dictionary<Region<TAddr>, TAddr>();
        foreach(var r in fTargetSystem.Regions.Reverse())
        {
            endAddrs[r] = endAddr;
            if (r.StartAddress.HasValue)
                endAddr = r.StartAddress.Value - TAddr.One;
        }

        fRegionEnd = endAddrs.ToFrozenDictionary();
    }

    public void Link(IEnumerable<Module<TAddr>> modules, Stream target)
    {
        var sections = (
            from mod in modules
            from sect in mod.Sections
            where sect.Size > TAddr.Zero
            select new ResolvedSection()
            {
                Module = mod,
                Section = sect,
                Origin = GetFixedOrigin(mod, sect)
            }
        ).ToList();

        AssertNotOverlapping((
            from s in sections
            where s.Origin.HasValue
            let origin = s.Origin!.Value
            orderby origin
            select (origin, origin + s.Section.Size)
        ).ToList());

        var regionMap = sections.ToLookup(s => s.Section.Region);

        TAddr start = TAddr.Zero;
        foreach (var region in fTargetSystem.Regions)
        {
            if (region.StartAddress.HasValue)
                start = region.StartAddress.Value;
            start = AssignOrigins(start, fRegionEnd[region], regionMap[region.Name]);
        }

        var patchers = CreatePatchRuntimesForModules(sections);

        WriteOutput(target, patchers, regionMap);
    }

    private void AssertNotOverlapping(List<(TAddr Start, TAddr End)> sections)
    {
        for (int i = 1; i < sections.Count; i++)
        {
            var cur = sections[i];
            var prev = sections[i - 1];
            if (prev.End > cur.Start)
                throw new InvalidOperationException($"Sections overlap at address {cur.Start}");
        }
    }

    private IReadOnlyDictionary<Module<TAddr>, PatchRuntime<TAddr>> CreatePatchRuntimesForModules(List<ResolvedSection> sections)
    {
        var symAddrs =
            from sect in sections
            from symAdr in sect.Section.SymbolAddresses
            let sym = sect.Module.GetSymbol(symAdr.Key)
            select new
            {
                sect.Module,
                SymbolId = symAdr.Key,
                Symbol = sym,
                Address = sect.Origin!.Value + symAdr.Value
            };

        var exports = symAddrs
            .Where(s => s.Symbol.Type == SymbolType.Exported)
            .ToLookup(s => s.Symbol.Name, s => new { s.Address, ModuleName = s.Module.Name });

        var runtimes = new Dictionary<Module<TAddr>, PatchRuntime<TAddr>>();

        foreach (var mod in sections.Select(s => s.Module).Distinct())
        {
            var modAddresses = symAddrs
                .Where(sa => sa.Module == mod)
                .ToDictionary(sa => sa.SymbolId, sa => sa.Address);

            foreach (var (id, sym) in mod.GetSymbols())
            {
                if (sym.Type == SymbolType.Imported)
                {
                    var addrs = exports[sym.Name].ToList();
                    switch (addrs.Count)
                    {
                        case 0:
                            throw new InvalidOperationException($"Symbol {sym.Name} is imported, but not found in any module.");
                        case 1:
                            modAddresses[id] = addrs[0].Address; break;
                        default:
                            throw new InvalidOperationException($"Symbol {sym.Name} is imported multiple times. Found in {string.Join(", ", addrs.Select(a => a.ModuleName))}.");
                    }
                }
                else if (!modAddresses.ContainsKey(id)) // Check Internal or exported symbols
                    throw new InvalidOperationException($"Symbol {sym.Name} is not defined in module {mod.Name}.");
            }

            runtimes[mod] = CreatePatchRuntime(s => modAddresses[s]);
        }
        return runtimes;
    }

    protected virtual PatchRuntime<TAddr> CreatePatchRuntime(Func<SymbolId, TAddr> symbolAddressResolver)
        => new(symbolAddressResolver);

    private void WriteOutput(Stream target, IReadOnlyDictionary<Module<TAddr>, PatchRuntime<TAddr>> patchers, ILookup<RegionName, ResolvedSection> regionMap)
    {
        var pos = TAddr.Zero;
        void FillTo(TAddr newPos)
        {
            const int FillBufferSize = 128;
            var delta = int.CreateTruncating(newPos - pos);
            Span<byte> buffer = stackalloc byte[FillBufferSize];
            // Stryker disable once Statement: The buffer might contain uninitialized data, that should not leak to the output.
            buffer.Clear();
            
            // Stryker disable once Equality: Mutating the > to >= does not change the behavior, but the code would run a little slower.
            while (delta > FillBufferSize)
            {
                target.Write(buffer);
                delta -= FillBufferSize;
            }
            target.Write(buffer.Slice(0, delta));
            pos = newPos;
        }

        bool firstRegion = true;
        foreach (var region in fTargetSystem.Regions.Where(r => r.Output))
        {
            if (region.StartAddress is TAddr start && firstRegion)
                pos = start;

            firstRegion = false;

            foreach (var sect in regionMap[region.Name].OrderBy(s => s.Origin))
            {
                FillTo(sect.Origin!.Value);

                ReadOnlyMemory<byte> data = sect.Section.Data;
                var expectedLength = int.CreateTruncating(sect.Section.Size);
                // Stryker disable once Equality: Mutating the < to <= does not change the behavior, but the code would run a little slower.
                if (data.Length < expectedLength)
                {
                    var paddedData = new byte[expectedLength];
                    data.CopyTo(paddedData);
                    data = paddedData;
                }

                var rt = patchers.GetValueOrDefault(sect.Module) ?? StaticPatchRuntime;
                int dataPos = 0;
                foreach(var p in sect.Section.Patches.OrderBy(o => o.Location))
                {
                    var patchOffset = int.CreateTruncating(p.Location);
                    target.Write(data.Span[dataPos..patchOffset]);
                    rt.Run(sect.Origin!.Value + p.Location, p.Size, p.Expressions, target);
                    dataPos = patchOffset + p.Size;
                }
                target.Write(data.Span[dataPos..]);
                pos += sect.Section.Size;
            }
        }
    }

    private TAddr AssignOrigins(TAddr firstUsableAddress, TAddr lastUsableAddress, IEnumerable<ResolvedSection> sections)
    {
        // used space including start but excluding end.
        var usedSpace = new List<(TAddr start, TAddr end)>();
        if (firstUsableAddress != TAddr.Zero)
            usedSpace.Add((TAddr.Zero, firstUsableAddress));

        foreach (var sect in sections.Where(s => s.Origin.HasValue))
        {
            var addr = sect.Origin!.Value;
            if (addr < firstUsableAddress)
                throw new InvalidOperationException($"Section origin can not be set to 0x{addr:X}. The address is not in the destination region.");
            usedSpace.Add((addr, addr + sect.Section.Size));
        }
        usedSpace.Sort((a, b) => a.start.CompareTo(b.start));

        foreach (var sect in sections.Where(s => !s.Origin.HasValue).OrderByDescending(s => s.Section.Size))
        {
            var addr = TAddr.Zero;
            foreach (var (start, end) in usedSpace)
            {
                if ((addr + sect.Section.Size) <= start)
                    break;
                addr = Align(end, sect.Section.Alignment);
            }
            sect.Origin = addr;

            var endAddr = addr + sect.Section.Size;

            // Stryker disable once Equality: Changing the comparison to <= would have the same effect.
            // But would require a value larger then the max value of the address datatype.
            if (endAddr < addr)
                throw new InvalidOperationException($"Unable to link {sect.Section.Region.Value}. Address space overflows.");

            if ((endAddr - TAddr.One) > lastUsableAddress)
                throw new InvalidOperationException($"Unable to link {sect.Section.Region.Value}. Not enough space.");

            var range = (addr, endAddr);

            // Stryker disable once Equality: changing the comparison to `>=` would not change a thing because the code above will not 
            // set two sections on the same starting index. We just want the next one with a larger start address.
            var idx = usedSpace.FindIndex(o => o.start > addr);
            if (idx == -1)
                usedSpace.Add(range);
            else
                usedSpace.Insert(idx, range);
        }
        if (usedSpace.Count > 0)
            return usedSpace[^1].end;
        return firstUsableAddress;
    }

    private static TAddr Align(TAddr addr, TAddr alignment)
    {
        if (alignment == TAddr.One)
            return addr;

        var overflow = addr % alignment;
        return addr + (alignment - overflow);
    }

    private TAddr? GetFixedOrigin(Module<TAddr> mod, Section<TAddr> sect)
    {
        var origins = (
                from symAdr in sect.SymbolAddresses
                let symbol = mod.GetSymbol(symAdr.Key)
                join anchor in fTargetSystem.Anchors on symbol.Name equals anchor.Name
                select anchor.Address - symAdr.Value);
        if (sect.Origin is TAddr origin)
            origins = origins.Append(origin);

        var distinctOrigins = origins.Distinct().GetEnumerator();
        if (!distinctOrigins.MoveNext())
            return null; // No Items
        var item = distinctOrigins.Current;
        if (!distinctOrigins.MoveNext())
            return item; // Only one item
        
        throw new InvalidOperationException($"Section {sect} has multiple origins.");
    }
}
