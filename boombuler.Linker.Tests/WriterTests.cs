using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using boombuler.Linker.Module;

namespace boombuler.Linker.Tests
{
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
    }
}
