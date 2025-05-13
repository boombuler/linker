using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace boombuler.Linker.Module;

public class ModuleSerializer
{
    private const string MagicNumber = "BLK";
    public void Serialize<TAddr>(Module<TAddr> module, Stream target)
        where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);

        using var writer = new BinaryWriter(target, Encoding.UTF8);

        writer.Write(Encoding.UTF8.GetBytes(MagicNumber));
        writer.Write((byte)Marshal.SizeOf<TAddr>());
        writer.Write(module.Name);
        writer.Write((ushort)module.Symbols.Length);
        foreach (var symbol in module.Symbols)
        {
            writer.Write((byte)symbol.Type);
            writer.Write(symbol.Name.Global ?? string.Empty);
            writer.Write(symbol.Name.Local ?? string.Empty);
        }
    }
}
