namespace boombuler.Linker.Tests;

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using boombuler.Linker.Module;
using boombuler.Linker.Patches;
using boombuler.Linker.Target;

[TestClass]
public sealed class LinkerTests
{
    private static readonly RegionName _Text = new RegionName(".text");
    private static readonly RegionName _Data = new RegionName(".data");

    class SimpleTarget(ushort textStart = 0x0000) : ITargetConfiguration<ushort>
    {
        public IEnumerable<Anchor<ushort>> Anchors => [];

        public IEnumerable<Region<ushort>> Regions => [
            new Region<ushort>(_Text, textStart, Output: true),
            new Region<ushort>(_Data, Output: true),
        ];
    }


    [TestMethod]
    public void Linking_Two_Static_Sections__Creates_Correct_Output()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = new []
            {
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0002,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x8F },
                    Size = 1,
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x05, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x8F, 0x00, 0x01, 0x02, 0x03 }, ms.ToArray());
    }

    [TestMethod]
    public void Linking_a_Region_Without_Start_Address__Places_the_region_directly_after_the_previous_section()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0002,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
                new Section<ushort>() {
                    Region = _Data,
                    Data = new byte[] { 0x13, 0x37 },
                    Size = 2,
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x07, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x01, 0x02, 0x03, 0x13, 0x37 }, ms.ToArray());
    }

    [TestMethod]
    public void Region_Start_Addresses__Move_the_output()
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x03, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void Invalid_SymbolIds__Throws_an_Exception(int symbolId)
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Size = 1,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(symbolId)] = 0x0000,
                    }.ToFrozenDictionary()
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    public void Applying_Patches__Resolves_Symbols()
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Symbols = new[] {
                Symbol.Internal, 
                Symbol.Internal,
            }.ToImmutableArray(),
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x00, 0x00, 0x00 },
                    Size = 3,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(0)] = 0x0002,
                        [new SymbolId(1)] = 0x0000,
                    }.ToFrozenDictionary(),
                    Patches = new ReadOnlyCollection<Patch<ushort>>([
                        new Patch<ushort>() {
                            Size = 1,
                            Location = 1,
                            Expressions =
                            [
                                Expression.SymbolAdress(new SymbolId(0)),
                            ],
                        },
                        new Patch<ushort>() {
                            Size = 1,
                            Location = 0,
                            Expressions =
                            [
                                Expression.SymbolAdress(new SymbolId(1)),
                            ],
                        },
                    ])
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x03, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x07, 0x00 }, ms.ToArray());
    }

    [TestMethod]
    public void Non_Imported_Symbols__Have_to_be_resolved()
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Symbols = new[] {
                Symbol.Internal,
            }.ToImmutableArray(),
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Size = 1,
                    Data = new byte[] { 0x00 },
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    public void Empty_Sections__Are_Ignored()
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Symbols = new[] {
                Symbol.Internal,
            }.ToImmutableArray(),
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Size = 0,
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0, ms.Length);
    }

    [TestMethod]
    public void Exported_Symbols__Can_Be_Resolved()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var modExport = new Module<ushort>()
        {
            Name = "Export",
            Symbols = new[] {
                new Symbol(new SymbolName("Export"), SymbolType.Exported)
            }.ToImmutableArray(),
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x0F },
                    Origin = 0x0A,
                    Size = 1,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(0)] = 0x0000,
                    }.ToFrozenDictionary(),
                },
            }.ToImmutableArray()
        };

        var modImport = new Module<ushort>()
        {
            Name = "Import",
            Symbols = new[] {
                new Symbol(new SymbolName("Export"), SymbolType.Imported)
            }.ToImmutableArray(),
            Sections = new[]
            {
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x00 },
                    Size = 1,
                    Patches = new ReadOnlyCollection<Patch<ushort>>([
                        new Patch<ushort>() {
                            Size = 1,
                            Location = 0,
                            Expressions =
                            [
                                Expression.SymbolAdress(new SymbolId(0)),
                            ],
                        },
                    ])
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        linker.Link([modExport, modImport], ms);

        Assert.AreEqual(11, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F }, ms.ToArray());
    }
}
