namespace boombuler.Linker.Tests;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using boombuler.Linker.Module;

[TestClass]
public class SerializerTests
{
    private static byte[] Serialize<TAddr>(Module<TAddr> module)
        where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>
    {
        using var stream = new MemoryStream();
        var serializer = new ModuleSerializer();
        serializer.Serialize(module, stream);
        return stream.ToArray();
    }

    [TestMethod]
    public void Modules_Headers__Contain_the_Address_Length()
    {
        var mod = Serialize(new Module<byte>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x01,       // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00                    // Symbol Count
        }, mod);

        mod = Serialize(new Module<ushort>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] { 
            0x42, 0x4C, 0x4B, 0x02,       // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00                    // Symbol Count
        }, mod);

        
        mod = Serialize(new Module<uint>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x04,       // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00                    // Symbol Count
        }, mod);

        mod = Serialize(new Module<ulong>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x08,       // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00                    // Symbol Count
        }, mod);
    }

    [TestMethod]
    public void Symbols__Are_Written_to_Output()
    {
        var mod = Serialize(new Module<ushort>() { 
            Name = "Test",
            Symbols = new [] {
                new Symbol(new SymbolName("Export"), SymbolType.Exported),
                new Symbol(new SymbolName("Import", "Local"), SymbolType.Imported),
                Symbol.Internal
            }.ToImmutableArray()
        });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x02,             // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74,       // Module Name
            0x03, 0x00,                         // Symbol Count
            0x01, 0x06,                         // Export + Global Length
            0x45, 0x78, 0x70, 0x6F, 0x72, 0x74, //   .GlobalName
            0x00,                               //   .Local Length
            0x02, 0x06,                         // Import + Global Length
            0x49, 0x6D, 0x70, 0x6F, 0x72, 0x74, //   .GlobalName
            0x05, 0x4C, 0x6F, 0x63, 0x61, 0x6C, //   .LocalLength + .LocalName
            0x00, 0x00, 0x00,                   // Internal Symbol without Name
        }, mod);
    }
}