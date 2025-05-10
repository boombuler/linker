namespace boombuler.Linker.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using boombuler.Linker.Module;
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
}
