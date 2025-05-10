namespace boombuler.Linker;

using System;
using System.Collections.Generic;
using System.Numerics;
using boombuler.Linker.Module;
using boombuler.Linker.Target;

public class Linker<TAddr>
    where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>, IComparable<TAddr>, IComparisonOperators<TAddr, TAddr, bool>
{
    private readonly ITargetConfiguration<TAddr> fTargetSystem;

    class ResolvedSection
    {
        public required Module<TAddr> Module { get; init; }
        public required Section<TAddr> Section { get; init; }
        public TAddr? Origin { get; set; }
    }

    public Linker(ITargetConfiguration<TAddr> targetSystem)
    {
        fTargetSystem = targetSystem ?? throw new ArgumentNullException(nameof(targetSystem));
    }

    public void Link(IEnumerable<Module<TAddr>> modules, Stream target)
    {
        var sections = (
            from mod in modules
            from sect in mod.Sections
            select new ResolvedSection()
            {
                Module = mod,
                Section = sect,
                Origin = GetFixedOrigin(mod, sect)
            }
        ).ToList();
        var regionMap = sections.ToLookup(s => s.Section.Region);

        TAddr start = TAddr.Zero;
        foreach (var region in fTargetSystem.Regions)
        {
            if (region.StartAddress.HasValue)
                start = region.StartAddress.Value;
            start = AssignOrigins(start, regionMap[region.Name]);
        }

        // ToDo: Patch the sections

        WriteOutput(target, regionMap);
    }

    private void WriteOutput(Stream target, ILookup<RegionName, ResolvedSection> regionMap)
    {
        var pos = TAddr.Zero;
        void FillTo(TAddr newPos)
        {
            var delta = int.CreateTruncating(newPos - pos);
            for (int i = 0; i < delta; i++)
                target.WriteByte(0x00);
            pos = newPos;
        }

        foreach (var region in fTargetSystem.Regions.Where(r => r.Output))
        {
            if (region.StartAddress is TAddr start)
            {
                if (pos == TAddr.Zero)
                    pos = start;
                else 
                    FillTo(start);
            }

            foreach (var sect in regionMap[region.Name].OrderBy(s => s.Origin))
            {
                FillTo(sect.Origin!.Value);

                if (sect.Section.Data.Length != int.CreateTruncating(sect.Section.Size))
                    throw new InvalidOperationException($"Section {sect.Section} has a size mismatch. Expected {sect.Section.Size}, but got {sect.Section.Data.Length}.");
                target.Write(sect.Section.Data.Span);
                pos += sect.Section.Size;
            }
        }
    }

    private TAddr AssignOrigins(TAddr regionStart, IEnumerable<ResolvedSection> sections)
    {
        var usedSpace = new List<(TAddr start, TAddr end)>();
        if (regionStart != TAddr.Zero)
            usedSpace.Add((TAddr.Zero, regionStart));

        foreach (var sect in sections.Where(s => s.Origin.HasValue))
        {
            var addr = sect.Origin!.Value;
            if (addr < regionStart)
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
                addr = end;
            }
            sect.Origin = addr;
            var range = (addr, addr + sect.Section.Size);

            var idx = usedSpace.FindIndex(o => o.start > addr);
            if (idx == -1)
                usedSpace.Add(range);
            else
                usedSpace.Insert(idx, range);
        }
        return usedSpace[^1].end;
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
