namespace boombuler.Linker.Tests;

using System.Collections.Frozen;
using System.Collections.Generic;
using boombuler.Linker.Module;
using boombuler.Linker.Patches;
using boombuler.Linker.Target;

[TestClass]
public sealed class LinkerTests
{
    private static readonly RegionName _Text = new RegionName(".text");
    private static readonly RegionName _Data = new RegionName(".data");
    private static readonly SymbolName Anchor = new SymbolName("Anchor");

    class SimpleTarget(ushort textStart = 0x0000, ushort? dataStart = null) : ITargetConfiguration<ushort>
    {
        public IEnumerable<Anchor<ushort>> Anchors => [new Anchor<ushort>(Anchor, 0x05)];

        public IEnumerable<Region<ushort>> Regions => [
            new Region<ushort>(_Text, textStart, Output: true),
            new Region<ushort>(_Data, dataStart, Output: true),
        ];
    }


    [TestMethod]
    public void Linking_Two_Static_Sections__Creates_Correct_Output()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
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
            ]
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
            Sections = [
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
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x07, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x01, 0x02, 0x03, 0x13, 0x37 }, ms.ToArray());
    }

    [TestMethod]
    public void Linking_a_Region_with_Origin_Outside_of_Region__Throws_an_Exception()
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x0100));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0010,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
            ]
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    public void The_Order_Of_Sections___Does_not_Matter()
    {
        var linker = new Linker<ushort>(new SimpleTarget());
        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0002,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0000,
                    Data = new byte[] { 0x8F },
                    Size = 1,
                },
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x11 },
                    Size = 1,
                },
            ]
        };
        using var ms = new MemoryStream();
        linker.Link([module], ms);
        Assert.AreEqual(0x05, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x8F, 0x11, 0x01, 0x02, 0x03 }, ms.ToArray());
    }

    [TestMethod]
    public void Overlapping_Sections__Throws_an_Exception()
    {
        var linker = new Linker<ushort>(new SimpleTarget());
        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0002,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0001,
                    Data = new byte[] { 0x00, 0x00 },
                    Size = 2,
                },
            ]
        };
        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    public void Non_Overlapping_Sections__Dont_throw_exceptions()
    {
        var linker = new Linker<ushort>(new SimpleTarget());
        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0002,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0000,
                    Data = new byte[] { 0xF0, 0x0F },
                    Size = 2,
                },
            ]
        };
        using var ms = new MemoryStream();
        linker.Link([module], ms);
        CollectionAssert.AreEqual(new byte[] { 0xF0, 0x0F, 0x01, 0x02, 0x03 }, ms.ToArray());
    }

    [TestMethod]
    public void Region_Start_Addresses__Move_the_output()
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x01, 0x02, 0x03 },
                    Size = 3,
                },
            ]
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
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 1,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(symbolId)] = 0x0000,
                    }.ToFrozenDictionary()
                },
            ]
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
            Symbols = [
                Symbol.Internal, 
                Symbol.Internal,
            ],
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x00, 0x00, 0x00 },
                    Size = 3,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(0)] = 0x0002,
                        [new SymbolId(1)] = 0x0000,
                    }.ToFrozenDictionary(),
                    Patches = [
                        new Patch<ushort>() {
                            Size = 1,
                            Location = 2,
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
                    ]
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x03, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x00, 0x07 }, ms.ToArray());
    }

    [TestMethod]
    public void Current_Address__Can_be_patched_in()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0001,
                    Size = 3,
                    Patches = [
                        new Patch<ushort>() {
                            Size = 1,
                            Location = 2,
                            Expressions =
                            [
                                Expression.CurrentAdress,
                            ],
                        },
                    ]
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x04, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x03 }, ms.ToArray());
    }

    [TestMethod]
    public void Non_Imported_Symbols__Have_to_be_resolved()
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Symbols = [ Symbol.Internal ],
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 1,
                    Data = new byte[] { 0x00 },
                },
            ]
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
            Symbols = [ Symbol.Internal ],
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 0,
                },
            ]
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
            Symbols = [ new Symbol(new SymbolName("Export"), SymbolType.Exported) ],
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x0F },
                    Origin = 0x0A,
                    Size = 1,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(0)] = 0x0000,
                    }.ToFrozenDictionary(),
                },
            ]
        };

        var modImport = new Module<ushort>()
        {
            Name = "Import",
            Symbols = [ new Symbol(new SymbolName("Export"), SymbolType.Imported) ],
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x00 },
                    Size = 1,
                    Patches = [
                        new Patch<ushort>() {
                            Size = 1,
                            Location = 0,
                            Expressions =
                            [
                                Expression.SymbolAdress(new SymbolId(0)),
                            ],
                        },
                    ]
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([modExport, modImport], ms);

        Assert.AreEqual(11, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F }, ms.ToArray());
    }

    [TestMethod]
    public void Section_with_too_little_Data__Are_filled_with_Zeros()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 3,
                    Data = new byte[] { 0x01 },
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00 }, ms.ToArray());
    }

    [TestMethod]
    public void Alignment__Is_applied_when_arranging_Sections()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 1,
                    Origin = 0,
                    Data = new byte[] { 0x01 },
                },
                new Section<ushort>() {
                    Region = _Text,
                    Size = 1,
                    Alignment = 4,
                    Data = new byte[] { 0x02 },
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02 }, ms.ToArray());
    }

    [TestMethod]
    public void Oversized_Sections__throw_an_Exception()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 0x10,
                },
                new Section<ushort>() {
                    Region = _Text,
                    Size = 0xFFF7,
                },
            ]
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    [DataRow(0xFFFFu)]
    [DataRow(0x000Bu)]
    public void Oversized_Sections_in_fixed_size_Regions__throw_an_Exception(uint size)
    {
        var linker = new Linker<ushort>(new SimpleTarget(0x0000, 0x000A));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = (ushort)size,
                },
            ]
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }


    [TestMethod]
    public void Anchors__Set_Section_Origin()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Symbols = [ new Symbol(Anchor, SymbolType.Internal) ],
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(0)] = 0x0001,
                    }.ToFrozenDictionary(),
                    Data = new byte[] { 0x02, 0x01 },
                    Size = 1,
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x02, 0x01 }, ms.ToArray());
    }


    [TestMethod]
    public void Clashing_Origins__Throw_an_Exception()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Symbols = [new Symbol(Anchor, SymbolType.Internal)],
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0000,
                    SymbolAddresses = new Dictionary<SymbolId, ushort>() {
                        [new SymbolId(0)] = 0x0000,
                    }.ToFrozenDictionary(),
                    Data = new byte[] { 0x01 },
                    Size = 1,
                },
            ]
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    public void Larger_Sections__are_placed_first()
    {
        var linker = new Linker<ushort>(new SimpleTarget());

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x02 },
                    Size = 1,
                },

                new Section<ushort>() {
                    Region = _Text,
                    Origin = 0x0A,
                    Data = new byte[] { 0x01, 0x01, 0x01 },
                    Size = 3,
                },

                new Section<ushort>() {
                    Region = _Text,
                    Data = new byte[] { 0x03, 0x03, 0x03, 0x03 },
                    Size = 4,
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x03, 0x03, 0x03, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01 }, ms.ToArray());
    }

    [TestMethod]
    public void Section_filling_the_last_byte_of_the_region__links_without_exceptions()
    {
        var linker = new Linker<ushort>(new SimpleTarget(textStart: 0x00, dataStart: 0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 5,
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);
    }

    [TestMethod]
    public void Section_filling_the_last_byte_of_the_address_space__links_without_exceptions()
    {
        var linker = new Linker<ushort>(new SimpleTarget(textStart: 0x00, dataStart: 0xFF00));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 0xFF,
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);
    }

    [TestMethod]
    public void Section_not_fitting_the_region__throw_an_Exception()
    {
        var linker = new Linker<ushort>(new SimpleTarget(textStart: 0x00, dataStart: 0x05));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Text,
                    Size = 10,
                },
            ]
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    public void Section_overflowing_the_address_space__throw_an_Exception()
    {
        var linker = new Linker<ushort>(new SimpleTarget(textStart: 0, dataStart: 0xFF00));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Data,
                    Size = 0x0100,
                },
            ]
        };

        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => linker.Link([module], ms));
    }

    [TestMethod]
    [DataRow(0x0010u)]
    [DataRow(0x0080u)]
    [DataRow(0x0100u)]
    public void Empty_spaces_between_regions__are_filled_with_zeros(uint dataStart)
    {
        var linker = new Linker<ushort>(new SimpleTarget(textStart: 0, dataStart: (ushort)dataStart));

        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = [
                new Section<ushort>() {
                    Region = _Data,
                    Size = 1,
                    Data = new byte[]{ 0x01 }
                },
            ]
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(dataStart + 1, ms.Length);
        var result = ms.ToArray();
        Assert.AreEqual(0x01, result[^1]);
        for (int i = 0; i < result.Length-1; i++)
            Assert.AreEqual(0x00, result[i]);
    }
}
