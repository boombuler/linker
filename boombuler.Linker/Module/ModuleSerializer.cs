using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace boombuler.Linker.Module;

public class ModuleSerializer<TAddr>
    where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>
{
    private readonly byte[] MagicNumber = [ 0x42, 0x4C, 0x4B, 0x00 ];

    public ModuleSerializer()
    {
        var addrLength = (byte)Marshal.SizeOf<TAddr>();
        if (!(addrLength is 1 or 2 or 4 or 8))
            throw new InvalidOperationException("Unsupported address size");
        MagicNumber[^1] = addrLength;
    }

    public void Serialize(Module<TAddr> module, Stream target)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);
        target.Write(MagicNumber);
        
        Span<byte> buffer = stackalloc byte[1024];
        var writer = new Writer(buffer, target);
        writer.WriteString(module.Name);
        writer.WriteVarInt(module.Symbols.Length);
        foreach (var symbol in module.Symbols)
            WriteSymbol(ref writer, symbol);

        writer.WriteVarInt(module.Sections.Length);
        writer.Flush();
    }

    private void WriteSection(BinaryWriter writer, Section<TAddr> section) 
    {
        /*
         * RegionName Region
         * FrozenDictionary<SymbolId, TAddr> SymbolAddresses
         * TAddr? Origin
         * TAddr Alignment
         * TAddr Size
         * ReadOnlyMemory<byte> Data
         * ReadOnlyCollection<Patch<TAddr>> Patches
         */
    }

    private static void WriteSymbol(ref Writer writer, Symbol symbol)
    {
        writer.WriteByte((byte)symbol.Type);
        writer.WriteString(symbol.Name.Global ?? string.Empty);
        writer.WriteString(symbol.Name.Local ?? string.Empty);
    }
}
