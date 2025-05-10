namespace boombuler.Linker.Tests;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using boombuler.Linker.Module;
using boombuler.Linker.Target;

[TestClass]
public class CpmTests
{
    [TestMethod]
    public void Cpm_Target__Outputs_text_and_data_regions()
    {
        var linker = new Linker<ushort>(Targets.CP_M);
        var module = new Module<ushort>()
        {
            Name = "TestModule",
            Sections = new[]
            {
                new Section<ushort>()
                {
                    Region = new RegionName(".text"),
                    Origin = 0x0101,
                    Data = new byte[] { 0x01 },
                    Size = 1,
                },
                new Section<ushort>()
                {
                    Region = new RegionName(".data"),
                    Data = new byte[] { 0x02 },
                    Size = 1,
                },
                new Section<ushort>()
                {
                    Region = new RegionName(".bss"),
                    Data = new byte[] { 0x03 },
                    Size = 1,
                },
            }.ToImmutableArray()
        };

        using var ms = new MemoryStream();
        linker.Link([module], ms);

        Assert.AreEqual(0x03, ms.Length);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x02 }, ms.ToArray());
    }
}
