namespace boombuler.Linker.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using boombuler.Linker.Patches;

[TestClass]
public class PatchTests
{
    [TestMethod]
    [DataRow(0x01u, 0x00u, 0x01u)]
    [DataRow(0xFFu, 0x01u, 0x00u)]
    [DataRow(0xF0u, 0x01u, 0xF1u)]
    public void Add__Adds_Two_Values_with_Overflow(uint a, uint b, uint expected)
    {
        var runtime = new PatchRt<ushort>(s => (ushort)s.Value);

        var buffer = new byte[2];
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Add], buffer);
        CollectionAssert.AreEqual(new byte[] { (byte)expected, 0x00 }, buffer);
    }

    [TestMethod]
    [DataRow(0x0000u)]
    [DataRow(0x1234u)]
    [DataRow(0xFFFFu)]
    public void CurrentAddress__Writes_the_Target_Address_To_The_Buffer(uint address)
    {
        var runtime = new PatchRt<ushort>(s => (ushort)s.Value);
        var buffer = new byte[3];
        runtime.Run((ushort)address, 2, [Expression.CurrentAdress], buffer);
        CollectionAssert.AreEqual(new byte[] { 
            (byte)(address & 0xFF),
            (byte)((address & 0xFF00) >> 8), 
            0x00
        }, buffer);
    }

    [TestMethod]
    [DataRow(0x01u, 0x01u, 0x00u)]
    [DataRow(0x03u, 0x01u, 0x02u)]
    [DataRow(0x00u, 0x01u, 0xFFu)]
    [DataRow(0xF1u, 0x01u, 0xF0u)]
    public void Sub__Substracts_Two_Values_with_Overflow(uint a, uint b, uint expected)
    {
        var runtime = new PatchRt<ushort>(s => (ushort)s.Value);

        var buffer = new byte[2];
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Sub], buffer);
        CollectionAssert.AreEqual(new byte[] { (byte)expected, 0x00 }, buffer);
    }

    [TestMethod]
    [DataRow(0x01u, 0x01u, 0x01u)]
    [DataRow(0x03u, 0x10u, 0x30u)]
    [DataRow(0x9Fu, 0x02u, 0x3Eu)]
    public void Mul__Multiplies_Two_Values_with_Overflow(uint a, uint b, uint expected)
    {
        var runtime = new PatchRt<ushort>(s => (ushort)s.Value);

        var buffer = new byte[2];
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Mul], buffer);
        CollectionAssert.AreEqual(new byte[] { (byte)expected, 0x00 }, buffer);
    }


    [TestMethod]
    [DataRow(0x0123456789ABCDEFu)]
    [DataRow(0x0u)]
    [DataRow(0xFFFFFFFFFFFFFFFFu)]
    public void Cpl__Flips_all_bits(ulong a)
    {
        ulong expected = ~a;
        var runtime = new PatchRt<ulong>(s => (ushort)s.Value);

        var buffer = new byte[8];
        runtime.Run(0x0000, 8, [Expression.Push(a), Expression.Cpl], buffer);
        CollectionAssert.AreEqual(new byte[] { 
            (byte)((expected >> 0) & 0xFF),
            (byte)((expected >> 8) & 0xFF),
            (byte)((expected >> 16) & 0xFF),
            (byte)((expected >> 24) & 0xFF),
            (byte)((expected >> 32) & 0xFF),
            (byte)((expected >> 40) & 0xFF),
            (byte)((expected >> 48) & 0xFF),
            (byte)((expected >> 56) & 0xFF),
        }, buffer);
    }

    [TestMethod]
    public void Runtime_without_Symbol_Resolver__Crashes()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new PatchRt<ushort>(null!));
    }

    [TestMethod]
    public void Patching_Undersized_Buffers__Raises_Exceptions()
    {
        var runtime = new PatchRt<ulong>(s => (ushort)s.Value);

        Assert.ThrowsException<ArgumentException>(() => runtime.Run(0x0000, 8, [Expression.Push(5)], new byte[1]));
    }

    [TestMethod]
    public void Incomplete_Patch_Expressions__Raises_Exceptions()
    {
        var runtime = new PatchRt<ulong>(s => (ushort)s.Value);

        Assert.ThrowsException<InvalidOperationException>(() => runtime.Run(0x0000, 1, [Expression.Push(5), Expression.Push(1)], new byte[1]));
    }

    [TestMethod]
    public void Encoded_Expression__Can_Be_Read()
    {
        var expressions = new byte[] { 
            0x20, 0x00, 0xFF,              // PUSH 0xFF00
            0x20, 0xF0, 0x00,              // PUSH 0x00F0
            0x05,                          // OR
            0x10, 0x01,                    // PUSH 0x01
            0x02,                          // SUB
            0x40, 0x00, 0x00, 0x34, 0x12 , // PUSH 0x12340000
            0x05,                          // OR
        };

        var runtime = new PatchRt<uint>(s => (uint)s.Value);
        var buffer = new byte[4];
        runtime.Run(0x00000000, 4, expressions, buffer);


        CollectionAssert.AreEqual(new byte[] { 0xEF, 0xFF, 0x34, 0x12 }, buffer);
    }
}
