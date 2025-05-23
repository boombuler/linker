namespace boombuler.Linker.Tests;

using System.Text;
using System.Linq;
using boombuler.Linker.Module;



[TestClass]
public class WriterTests
{
    [TestMethod]
    public void Writer_initialized_with_buffer_smaller_than_128_throws_exception()
    {
        using var stream = new MemoryStream();
        Assert.ThrowsException<ArgumentException>(() => new Writer(new byte[127], stream));
    }

    [TestMethod]
    public void Writer_initialized_without_a_Stream__throws_an_exception()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new Writer(new byte[512], null!));
    }

    [TestMethod]
    public void Writing_less_then_buffer_size__does_not_flush_the_buffer()
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        for (int i = 0; i < 127; i++)
            writer.WriteByte((byte)i);
        Assert.AreEqual(0, stream.Length);
    }

    [TestMethod]
    public void Writing_more_bytes_then_buffer_size__writes_the_buffer_to_target_Stream()
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        for (int i = 0; i < 256; i++)
            writer.WriteByte((byte)i);
        writer.Flush();
        var result = stream.ToArray();
        for (int i = 0; i < 256; i++)
            Assert.AreEqual((byte)i, result[i]);
    }

    [TestMethod]
    public void Writing_more_bools_then_buffer_size__writes_the_buffer_to_target_Stream()
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        for (int i = 0; i < 256; i++)
            writer.WriteBool(i % 2 == 0);
        writer.Flush();
        var result = stream.ToArray();
        for (int i = 0; i < 256; i++)
            Assert.AreEqual(i % 2 == 0, result[i] == 1);
    }

    [TestMethod]
    public void VarInts__are_written_correctly()
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        writer.WriteVarInt(0x80);
        writer.WriteVarInt(0x123);
        writer.Flush();
        var result = stream.ToArray();
        CollectionAssert.AreEqual(new byte[] { 
            0b1000_0000, 0b0000_0001,
            0b1010_0011, 0b0000_0010,
        }, result);
    }

    [TestMethod]
    public void Writing_uint16_at_the_end_of_the_buffer__flushes_the_buffer_before_writing()
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        var seek = 128 - (sizeof(ushort) / 2);
        for (int i = 0; i < seek; i++)
            writer.WriteByte((byte)i);
        Assert.AreEqual(0, stream.Length);
        writer.WriteUInt16(0x1234);
        Assert.IsTrue(stream.Length > 0);
        writer.Flush();
        CollectionAssert.AreEqual(new byte[] { 0x34, 0x12 }, stream.ToArray()[seek..]);
    }

    [TestMethod]
    public void Writing_uint32_at_the_end_of_the_buffer__flushes_the_buffer_before_writing()
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        var seek = 128 - (sizeof(uint) / 2);
        for (int i = 0; i < seek; i++)
            writer.WriteByte((byte)i);
        Assert.AreEqual(0, stream.Length);
        writer.WriteUInt32(0x12345678);
        Assert.IsTrue(stream.Length > 0);
        writer.Flush();
        CollectionAssert.AreEqual(new byte[] { 0x78, 0x56, 0x34, 0x12 }, stream.ToArray()[seek..]);
    }

    [TestMethod]
    public void Writing_uint64_at_the_end_of_the_buffer__flushes_the_buffer_before_writing()
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        var seek = 128 - (sizeof(ulong) / 2);
        for (int i = 0; i < seek; i++)
            writer.WriteByte((byte)i);
        Assert.AreEqual(0, stream.Length);
        writer.WriteUInt64(0x12345678_9ABCDEF0);
        Assert.IsTrue(stream.Length > 0);
        writer.Flush();
        CollectionAssert.AreEqual(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }, stream.ToArray()[seek..]);
    }


    [TestMethod]
    [DataRow("Hello World", 0)]
    [DataRow("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.", 0)]
    [DataRow("😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀", 0)]
    [DataRow("😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀", 0)]
    [DataRow("😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀", 1)]
    [DataRow("😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀", 36)]
    [DataRow("0123456789012345678901234567890123456789012", 127)]
    [DataRow("Hello World", 120)]
    public void Writing_strings__should_write_correct_output(string data, int prefillBufferSize)
    {
        var stream = new MemoryStream();
        var writer = new Writer(new byte[128], stream);
        for (int i = 0; i < prefillBufferSize; i++)
            writer.WriteByte(0);
        writer.WriteString(data);
        writer.Flush();
        var encodedData = Encoding.UTF8.GetBytes(data);

        var prefix = new byte[8];
        int len = encodedData.Length;
        int idx = 0;
        while (len >= 0x80)
        {
            prefix[idx++] = (byte)(len | 0x80);
            len >>= 7;
        }
        prefix[idx++] = (byte)len;
        var result = stream.ToArray();
        CollectionAssert.AreEqual(prefix[..idx], result[prefillBufferSize..(prefillBufferSize+idx)]);
        CollectionAssert.AreEqual(encodedData, result[(prefillBufferSize+idx)..]);
    }
}
