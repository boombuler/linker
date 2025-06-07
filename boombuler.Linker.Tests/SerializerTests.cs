namespace boombuler.Linker.Tests;

using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Numerics;
using boombuler.Linker.Module;
using boombuler.Linker.Patches;

[TestClass]
public class SerializerTests
{
    #region Sample Data

    private static readonly Module<ushort> SectionSampleModule = new Module<ushort>()
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
    };

    private static readonly byte[] SerializedSectionSampleModule = [
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
    ];

    private static readonly Module<ushort> SymbolSampleModule = new Module<ushort>()
    {
        Name = "Test",
        Symbols = [
                new Symbol(new SymbolName("Export"), SymbolType.Exported),
                new Symbol(new SymbolName("Import", "Local"), SymbolType.Imported),
                Symbol.Internal
            ]
    };

    private static readonly byte[] SerializedSymbolSampleModule = [
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
        0x00
    ];

    #endregion

    private static byte[] Serialize<TAddr>(Module<TAddr> module)
        where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>, IShiftOperators<TAddr, int, TAddr>, IBitwiseOperators<TAddr, TAddr, TAddr>
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
        var mod = Serialize(SymbolSampleModule);
        CollectionAssert.AreEqual(SerializedSymbolSampleModule, mod);
    }

    [TestMethod]
    public void Sections__Are_Written_to_Output() 
    {
        var mod = Serialize(SectionSampleModule);
        CollectionAssert.AreEqual(SerializedSectionSampleModule, mod);
    }

    [TestMethod]
    public void Not_passing_a_Module_to_Serialize__Throws_an_Exception()
    {
        using var stream = new MemoryStream();
        var serializer = new ModuleSerializer<uint>();
        Assert.ThrowsException<ArgumentNullException>(() => serializer.Serialize(null!, stream));
    }

    [TestMethod]
    public void Not_passing_a_Stream_to_Serialize__Throws_an_Exception()
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

    [TestMethod]
    public void Not_assing_a_Stream_on_Deserialize__Throws_an_Exception()
    {
        var serializer = new ModuleSerializer<uint>();
        try
        {
            _ = serializer.Deserialize(null!);
        }
        catch (ArgumentNullException ex) when (ex.ParamName == "source")
        {
            return;
        }
        Assert.Fail("Expected ArgumentNullException was not thrown.");
    }

    [TestMethod]
    public void Stream__Is_Still_Open_after_Deserialize()
    {
        using var ms = new MemoryStream(SerializedSectionSampleModule);
        var serializer = new ModuleSerializer<ushort>();
        _ = serializer.Deserialize(ms);
        Assert.IsTrue(ms.CanRead);
        Assert.IsTrue(ms.CanSeek);
        Assert.IsTrue(ms.CanWrite);
    }

    [TestMethod]
    [DataRow(0x42_4C_4B_01_01_00_00_00u, false)]
    [DataRow(0x42_4C_4B_00_02_00_00_00u, false)]
    [DataRow(0x42_4C_3B_01_02_00_00_00u, false)]
    [DataRow(0x42_1C_4B_01_02_00_00_00u, false)]
    [DataRow(0x02_4C_4B_01_02_00_00_00u, false)]
    [DataRow(0x42_4C_4B_01_02_00_00_00u, true)]
    public void Deserialize_checks_for_MagicNumber(ulong data, bool headerValid)
    {
        using var ms = new MemoryStream();
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, data);
        ms.Write(buffer);
        ms.Position = 0;

        var serializer = new ModuleSerializer<ushort>();
        if (headerValid) 
            _ = serializer.Deserialize(ms);
        else
            Assert.ThrowsException<ArgumentException>(() => serializer.Deserialize(ms));
    }

    [TestMethod]
    public void Deserialze_throws_Exception_if_Stream_is_not_a_Module()
    {
        using var ms = new MemoryStream([0x01]);
        var serializer = new ModuleSerializer<ushort>();
        Assert.ThrowsException<ArgumentException>(() => serializer.Deserialize(ms));
    }

    [TestMethod]
    public void All_Sections_are_read_from_Stream()
    {
        var serializer = new ModuleSerializer<ushort>();
        using var ms = new MemoryStream(SerializedSectionSampleModule);
        var mod = serializer.Deserialize(ms);
        AssertModule(SectionSampleModule, mod);
    }

    [TestMethod]
    public void All_Symbols_are_read_from_Stream()
    {
        var serializer = new ModuleSerializer<ushort>();
        using var ms = new MemoryStream(SerializedSymbolSampleModule);
        var mod = serializer.Deserialize(ms);
        AssertModule(SymbolSampleModule, mod);
    }

    [TestMethod]
    public void Deserializing_invalid_symbol_types_Throws_Exception()
    {
        var invalidModule = new byte[] {
            0x42, 0x4C, 0x4B, 0x01, 0x02, // Magic Number + Address Length
            0x04, 0x54, 0x65, 0x73, 0x74, // Module Name
            0x01,                         // Symbol Count
            0xFF,                         // Invalid Symbol Type
            0x00,                         // Section Count
        };
        using var ms = new MemoryStream(invalidModule);
        var serializer = new ModuleSerializer<ushort>();
        Assert.ThrowsException<ArgumentException>(() => serializer.Deserialize(ms));
    }

    private void AssertModule(Module<ushort> expectedModule, Module<ushort> actualModule)
    {
        Assert.AreEqual(expectedModule.Name, actualModule.Name);
        Assert.AreEqual(expectedModule.Symbols.Length, actualModule.Symbols.Length);
        for (int i = 0; i < expectedModule.Symbols.Length; i++)
        {
            Assert.AreEqual(expectedModule.Symbols[i], actualModule.Symbols[i]);
        }
        for (int i = 0; i < expectedModule.Sections.Length; i++)
        {
            var expected = expectedModule.Sections[i];
            var actual = actualModule.Sections[i];
            Assert.AreEqual(expected.Region, actual.Region);
            Assert.AreEqual(expected.Alignment, actual.Alignment);
            Assert.AreEqual(expected.Size, actual.Size);
            Assert.AreEqual(expected.Origin, actual.Origin);
            Assert.AreEqual(expected.Data.Length, actual.Data.Length);
            CollectionAssert.AreEqual(expected.Data.ToArray(), actual.Data.ToArray());
            Assert.AreEqual(expected.Patches.Length, actual.Patches.Length);
            for (int j = 0; j < expected.Patches.Length; j++)
            {
                var expectedPatch = expected.Patches[j];
                var actualPatch = actual.Patches[j];
                Assert.AreEqual(expectedPatch.Location, actualPatch.Location);
                Assert.AreEqual(expectedPatch.Size, actualPatch.Size);
                Assert.AreEqual(expectedPatch.Expressions.Count, actualPatch.Expressions.Count);
                Assert.IsTrue(expectedPatch.Expressions.SequenceEqual(actualPatch.Expressions));
            }
        }
    }
}