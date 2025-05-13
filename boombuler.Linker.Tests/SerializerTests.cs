namespace boombuler.Linker.Tests;

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
}