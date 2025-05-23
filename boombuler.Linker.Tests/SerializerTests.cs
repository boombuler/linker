namespace boombuler.Linker.Tests;

using System.Collections.Frozen;
using System.Numerics;
using boombuler.Linker.Module;
using boombuler.Linker.Patches;

[TestClass]
public class SerializerTests
{
    private static byte[] Serialize<TAddr>(Module<TAddr> module)
        where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>, IShiftOperators<TAddr, int, TAddr>
    {
        using var stream = new MemoryStream();
        var serializer = new ModuleSerializer<TAddr>();
        serializer.Serialize(module, stream);
        return stream.ToArray();
    }

    [TestMethod]
    public void Modules_Headers__Contain_the_Address_Length()
    {
        var mod = Serialize(new Module<byte>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x01, 0x01, // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00,                   // Symbol + Section Count
        }, mod);

        mod = Serialize(new Module<ushort>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] { 
            0x42, 0x4C, 0x4B, 0x01, 0x02, // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00,                   // Symbol + Section Count
        }, mod);

        
        mod = Serialize(new Module<uint>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x01, 0x04, // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00,                   // Symbol + Section Count
        }, mod);

        mod = Serialize(new Module<ulong>() { Name = "Test" });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x01, 0x08, // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x00, 0x00,                   // Symbol + Section Count
        }, mod);
    }

    [TestMethod]
    public void Symbols__Are_Written_to_Output()
    {
        var mod = Serialize(new Module<ushort>() { 
            Name = "Test",
            Symbols = [
                new Symbol(new SymbolName("Export"), SymbolType.Exported),
                new Symbol(new SymbolName("Import", "Local"), SymbolType.Imported),
                Symbol.Internal
            ]
        });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x01, 0x02,       // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74,       // Module Name
            0x03,                               // Symbol Count
            0x01, 0x06,                         // Export + Global Length
            0x45, 0x78, 0x70, 0x6F, 0x72, 0x74, //   .GlobalName
            0x00,                               //   .Local Length
            0x02, 0x06,                         // Import + Global Length
            0x49, 0x6D, 0x70, 0x6F, 0x72, 0x74, //   .GlobalName
            0x05, 0x4C, 0x6F, 0x63, 0x61, 0x6C, //   .LocalLength + .LocalName
            0x00, 0x00, 0x00,                   // Internal Symbol without Name
            0x00, 
        }, mod);
    }

    [TestMethod]
    public void Section__Are_Written_to_Output() 
    {
        var mod = Serialize(new Module<ushort>()
        {
            Name = "😀",
            Symbols = [],
            Sections = [
                new Section<ushort>() {
                    Region = new RegionName(".bss"),
                    Alignment = 1,
                    Size = 0xFF,
                    Origin = 0x1234,
                    Patches = [
                        new Patch<ushort>(){
                            Size = 1,
                            Location = 5,
                            Expressions = [
                                Expression.SymbolAdress(new SymbolId(5)),
                                Expression.Push(0x1234),
                                Expression.Add,
                            ]
                        }
                    ]
                },
                new Section<ushort>() {
                    Region = new RegionName(".text"),
                    Alignment = 3,
                    Data = new byte[] { 0x01, 0x02, 0x03, 0x04 },
                    Size = 0x04,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(1)] = 0x1234,
                        [new SymbolId(5)] = 0x5678,
                    }.ToFrozenDictionary(),
                }
            ]
        });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x01, 0x02,       // 0x00: Magic Number + Address Length
            0x04, 0xF0, 0x9F, 0x98, 0x80,       // 0x05: Module Name
            0x00, 0x02,                         // 0x0A: Symbol + Section Count
            // Section 1
            0x04, 0x2E, 0x62, 0x73, 0x73,       // 0x0C: Region Name
            0x00,                               // 0x11: Symbol Address Count
            0x01, 0x34, 0x12,                   // 0x12: Origin
            0x01, 0x00,                         // 0x15: Alignment
            0xFF, 0x00,                         // 0x17: Size
            0x00,                               // 0x19: Data Length
            0x01,                               // 0x1A: Patch Count
            0x05, 0x00, 0x01,                   // 0x1B: Patch Location + Size
            0x03,                               // 0x1E: Expression Count
            0x1B, 0x05,                         // 0x1F: Symbol Address
            0x20, 0x34, 0x12,                   // 0x21: Push
            0x01,                               // 0x24: Add
            // Section 2
            0x05, 0x2E, 0x74, 0x65, 0x78, 0x74, // 0x25: Region Name
            0x02,                               // 0x2B: Symbol Address Count
            0x01, 0x34, 0x12,                   // 0x2C: Symbol Address 
            0x05, 0x78, 0x56,                   // 0x2F: Symbol Address
            0x00,                               // 0x32: Origin
            0x03, 0x00,                         // 0x33: Alignment
            0x04, 0x00,                         // 0x35: Size
            0x04,                               // 0x37: Data Length
            0x01, 0x02, 0x03, 0x04,             // 0x38: Data
            0x00,                               // 0x3C: Patch Count
        }, mod);
    }

    [TestMethod]
    public void Not_passing_a_Module__Throws_an_Exception()
    {
        using var stream = new MemoryStream();
        var serializer = new ModuleSerializer<uint>();
        Assert.ThrowsException<ArgumentNullException>(() => serializer.Serialize(null!, stream));
    }

    [TestMethod]
    public void Not_passing_a_Stream__Throws_an_Exception()
    {
        var serializer = new ModuleSerializer<uint>();
        var module = new Module<uint>()
        {
            Name = "😀",
            Symbols = [],
            Sections = []
        };

        Assert.ThrowsException<ArgumentNullException>(() => serializer.Serialize(module, null!));
    }

    [TestMethod]
    public void Patch_Parameters__Encodes_as_small_as_possible()
    {
        var mod = Serialize(new Module<ushort>()
        {
            Name = "😀",
            Symbols = [],
            Sections = [
                new Section<ushort>() {
                    Region = new RegionName(".bss"),
                    Alignment = 1,
                    Size = 0xFF,
                    Origin = 0x1234,
                    Patches = [
                        new Patch<ushort>(){
                            Size = 1,
                            Location = 5,
                            Expressions = [
                                Expression.Push(0xFFFF),
                                Expression.Push(0xFFFFFFFF),
                                Expression.Push(0xFF),
                                Expression.Push(0x12FFFFFFFF),
                            ]
                        }
                    ]
                },
            ]
        });
        CollectionAssert.AreEqual(new byte[] {
            0x42, 0x4C, 0x4B, 0x01, 0x02,                        // 0x00: Magic Number + Address Length
            0x04, 0xF0, 0x9F, 0x98, 0x80,                        // 0x05: Module Name
            0x00, 0x01,                                          // 0x0A: Symbol + Section Count
            // Section 1
            0x04, 0x2E, 0x62, 0x73, 0x73,                        // 0x0C: Region Name
            0x00,                                                // 0x11: Symbol Address Count
            0x01, 0x34, 0x12,                                    // 0x12: Origin
            0x01, 0x00,                                          // 0x15: Alignment
            0xFF, 0x00,                                          // 0x17: Size
            0x00,                                                // 0x19: Data Length
            0x01,                                                // 0x1A: Patch Count
            0x05, 0x00, 0x01,                                    // 0x1B: Patch Location + Size
            0x04,                                                // 0x1E: Expression Count
            0x20, 0xFF, 0xFF,                                    // 0x1F: Push 0xFFFF
            0x40, 0xFF, 0xFF, 0xFF, 0xFF,                        // 0x22: Push 0xFFFFFFFF
            0x10, 0xFF,                                          // 0x27: Push 0xFF
            0x80, 0xFF, 0xFF, 0xFF, 0xFF, 0x12, 0x00, 0x00, 0x00 // 0x29: Push 0x1FFFFFFFF
        }, mod);
    }
}