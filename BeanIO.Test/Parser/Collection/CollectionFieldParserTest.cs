﻿using System;
using System.Diagnostics;
using System.IO;

using BeanIO.Parser.Bean;

using Xunit;

namespace BeanIO.Parser.Collection
{
    public class CollectionFieldParserTest : ParserTest
    {
        [Fact]
        public void TestCollectionDelimited()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Collection.collection.xml");
            var reader = factory.CreateReader("dc1", LoadStream("dc1_valid.txt"));
            try
            {
                var bean = (CollectionBean)reader.Read();
                Assert.Equal(new[] { "George", "Gary", "Jon" }, bean.List);
                Assert.Equal(new[] { 1, 2, 3, 4 }, bean.Array);

                var text = new StringWriter();
                factory.CreateWriter("dc1", text).Write(bean);
                Assert.Equal("George,Gary,Jon,1,2,3,4" + LineSeparator, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestNullPrimitive()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Collection.collection.xml");
            var reader = factory.CreateReader("dc2", LoadStream("dc2_nullPrimitive.txt"));
            try
            {
                var bean = (CollectionBean)reader.Read();
                Assert.Equal(new[] { 1, 0, 3 }, bean.Array);

                var text = new StringWriter();
                factory.CreateWriter("dc2", text).Write(new CollectionBean());
                Assert.Equal(",," + LineSeparator, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestCollectionFixedLength()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Collection.collection.xml");
            var reader = factory.CreateReader("fc1", LoadStream("fc1_valid.txt"));
            try
            {
                var bean = (CollectionBean)reader.Read();
                Assert.Equal(new[] { 1, 100, 24 }, bean.Array);
                Assert.Equal(new char?[] { 'A', 'B', 'C', ' ' }, bean.Set);

                var text = new StringWriter();
                factory.CreateWriter("fc1", text).Write(bean);
                Assert.Equal("001100024ABC " + LineSeparator, text.ToString());

                bean = (CollectionBean)reader.Read();
                Assert.Equal(new[] { 0, 400, 500 }, bean.Array);
                Assert.Empty(bean.Set);

                text = new StringWriter();
                factory.CreateWriter("fc1", text).Write(bean);
                Assert.Equal("000400500" + LineSeparator, text.ToString());

                text = new StringWriter();
                factory.CreateWriter("fc1", text).Write(new CollectionBean());
                Assert.Equal("000000000" + LineSeparator, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        private static TextReader LoadStream(string fileName)
        {
            var resourceName = string.Format("BeanIO.Parser.Collection.{0}", fileName);
            var asm = typeof(BeanParserTest).Assembly;
            var resStream = asm.GetManifestResourceStream(resourceName);
            Debug.Assert(resStream != null, "resStream != null");
            return new StreamReader(resStream);
        }
    }
}
