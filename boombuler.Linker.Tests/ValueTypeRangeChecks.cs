using boombuler.Linker.Module;

namespace boombuler.Linker.Tests;

[TestClass]
public class ValueTypeRangeChecks
{
    [TestMethod]
    public void SymbolId__Can_not_be_negative()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new SymbolId(-1), message:"Foo");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(5467)]
    [DataRow(int.MaxValue)]
    public void SymbolId_Equal_or_greater_to_zero__Are_valid(int id)
    {
        _ = new SymbolId(id);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void RegionName__Can_not_be_empty_or_null(string name)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RegionName(name));
    }

    [TestMethod]
    public void Exported_Symbols__Must_have_a_name()
    {
        Assert.ThrowsException<ArgumentException>(() => new Symbol(SymbolName.Internal, SymbolType.Exported));
    }

    [TestMethod]
    public void Imported_Symbols__Must_have_a_name()
    {
        Assert.ThrowsException<ArgumentException>(() => new Symbol(SymbolName.Internal, SymbolType.Imported));
    }
}
