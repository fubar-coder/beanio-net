﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Xunit;

namespace BeanIO.Parser.Bean
{
    public class BeanParserTest : ParserTest
    {
        [Fact]
        public void TestDelimitedPositions()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w1", LoadStream("w1_position.txt"));
            try
            {
                var w = (Widget)reader.Read();
                Assert.Equal(3, w.Id);
                Assert.Equal("Widget3", w.Name);
                Assert.NotNull(w.Top);
                Assert.Equal(2, w.Top.Id);
                Assert.Equal("Widget2", w.Top.Name);
                Assert.NotNull(w.Bottom);
                Assert.Equal(1, w.Bottom.Id);
                Assert.Equal("Widget1", w.Bottom.Name);

                var text = new StringWriter();
                factory.CreateWriter("w1", text).Write(w);
                Assert.Equal(",Widget1,1,2,Widget2,Widget3,3" + Environment.NewLine, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestCollectionsAndDefaultDelmitedPositions()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w2", LoadStream("w2_collections.txt"));
            try
            {
                var w = (Widget)reader.Read();
                Assert.Equal("3", w.Name);
                Assert.True(w.PartsList.Count > 1);
                Assert.True(w.PartsList[1].PartsList.Count > 1);
                Assert.Equal("2B", w.GetPart(1).GetPart(1).Name);

                var text = new StringWriter();
                factory.CreateWriter("w2", text).Write(w);
                Assert.Equal("1,1M,1A,1AM,1B,1BM,2,2M,2A,2AM,2B,2BM,3" + Environment.NewLine, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestFixedLengthAndOptionalFields()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w3", LoadStream("w3_fixedLength.txt"));
            try
            {
                var w = (Widget)reader.Read();
                Assert.Equal(1, w.Id);
                Assert.Equal("name1", w.Name);
                Assert.Equal("mode1", w.Model);

                var text = new StringWriter();
                factory.CreateWriter("w3", text).Write(w);
                Assert.Equal(" 1name1mode1" + Environment.NewLine, text.ToString());

                w = (Widget)reader.Read();
                Assert.Equal(1, w.Id);
                Assert.Equal("name1", w.Name);
                Assert.Equal("mode1", w.Model);
                Assert.Collection(
                    w.PartsList,
                    part =>
                        {
                            Assert.Equal(2, part.Id);
                            Assert.Equal("name2", part.Name);
                            Assert.Equal("mode2", part.Model);
                        },
                    part =>
                        {
                            Assert.Equal(3, part.Id);
                        },
                    part =>
                        {
                            Assert.Equal(4, part.Id);
                            Assert.Equal("name4", part.Name);
                            Assert.Equal(string.Empty, part.Model);
                        });

                text = new StringWriter();
                factory.CreateWriter("w3", text).Write(w);
                Assert.Equal(" 1name1mode1 2name2mode2 3           4name4     " + Environment.NewLine, text.ToString());

                w = (Widget)reader.Read();
                text = new StringWriter();
                factory.CreateWriter("w3", text).Write(w);
                Assert.Equal(" 1name1mode1 2name2mode2 0           4name4mode4" + Environment.NewLine, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestFixedLengthMap()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w4", LoadStream("w4_map.txt"));
            try
            {
                var w = (Widget)reader.Read();
                Assert.Equal(1, w.Id);
                Assert.Equal("name1", w.Name);
                Assert.NotNull(w.PartsMap);
                Assert.True(w.PartsMap.ContainsKey("part1"));
                Assert.Equal(2, w.GetPart("part1").Id);
                Assert.Equal("name2", w.GetPart("part1").Name);

                var text = new StringWriter();
                factory.CreateWriter("w4", text).Write(w);
                Assert.Equal("1name12name2" + Environment.NewLine, text.ToString());

                w = (Widget)reader.Read();
                Assert.NotNull(w.PartsMap);
                Assert.True(w.PartsMap.ContainsKey("part2"));
                Assert.Equal(3, w.GetPart("part2").Id);
                Assert.Equal("name3", w.GetPart("part2").Name);

                text = new StringWriter();
                factory.CreateWriter("w4", text).Write(w);
                Assert.Equal("1name12name23name3" + Environment.NewLine, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestFixedLengthOutOfOrder()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w5", LoadStream("w5_outOfOrder.txt"));
            try
            {
                var map = (IDictionary)reader.Read();
                var w = (Widget)map["part3"];
                Assert.Equal(3, w.Id);
                Assert.Equal("name3", w.Name);

                var text = new StringWriter();
                factory.CreateWriter("w5", text).Write(map);
                Assert.Equal("123name1name2name3" + Environment.NewLine, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestFieldError()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w6", LoadStream("w6_fieldError.txt"));
            try
            {
                AssertFieldError(reader, 1, "record1", "Id", 2, "A", "Type conversion error: Invalid Integer value 'A'");
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestWriteNull()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var w = new Widget();

            var text = new StringWriter();
            factory.CreateWriter("w6", text).Write(w);
            Assert.Equal(string.Empty + Environment.NewLine, text.ToString());

            var part1 = new Widget()
                {
                    Id = 1,
                    Name = "part1",
                };
            w.AddPart(part1);

            text = new StringWriter();
            factory.CreateWriter("w6", text).Write(w);
            Assert.Equal("1,part1" + Environment.NewLine, text.ToString());

            w.AddPart(null);

            text = new StringWriter();
            factory.CreateWriter("w6", text).Write(w);
            Assert.Equal("1,part1,," + Environment.NewLine, text.ToString());

            var part2 = new Widget()
            {
                Id = 2,
                Name = "part2",
            };
            w.AddPart(part2);

            text = new StringWriter();
            factory.CreateWriter("w6", text).Write(w);
            Assert.Equal("1,part1,,,2,part2" + Environment.NewLine, text.ToString());
        }

        [Fact]
        public void TestBackfill()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var w = new Widget
                {
                    Id = 1,
                };

            var text = new StringWriter();
            factory.CreateWriter("w7", text).Write(w);
            Assert.Equal("1" + Environment.NewLine, text.ToString());

            w.Model = "model";

            text = new StringWriter();
            factory.CreateWriter("w7", text).Write(w);
            Assert.Equal("1,,model" + Environment.NewLine, text.ToString());
        }

        [Fact]
        public void TestRecordIdentifier()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var w = new Widget()
                {
                    Id = 1,
                    Name = "name1",
                };

            var map = new Dictionary<string, Widget>();
            map["widget"] = w;

            var text = new StringWriter();
            factory.CreateWriter("w8", text).Write(map);
            Assert.Equal("R1,1,name1" + Environment.NewLine, text.ToString());

            w.Id = 2;
            w.Name = "name2";

            text = new StringWriter();
            factory.CreateWriter("w8", text).Write(map);
            Assert.Equal("R2,2,name2" + Environment.NewLine, text.ToString());

            w.Id = 3;
            Assert.Throws<BeanWriterException>(
                () =>
                    {
                        factory.CreateWriter("w8", text).Write(map);
                    });
        }

        [Fact]
        public void TestFixedLengthCollection()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w9", LoadStream("w9_flcollections.txt"));
            try
            {
                var w = (Widget)reader.Read();
                Assert.Equal(1, w.Id);
                Assert.Equal("name", w.Name);

                var text = new StringWriter();
                factory.CreateWriter("w9", text).Write(w);
                Assert.Equal(" 1 1part1 2part2name " + Environment.NewLine, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        [Fact]
        public void TestNestedBeans()
        {
            var factory = NewStreamFactory("BeanIO.Parser.Bean.widget.xml");
            var reader = factory.CreateReader("w10", LoadStream("w10_nestedBeans.txt"));
            try
            {
                reader.Read();
                var map = (IDictionary)reader.Read();
                Assert.True(map.Contains("eof"));
                Assert.Equal("eof", map["eof"]);

                Assert.True(map.Contains("b1"));
                var list = (IList)map["b1"];
                Assert.NotNull(list);
                Assert.Equal(2, list.Count);

                var b1 = (IDictionary)list[0];
                Assert.NotNull(b1);
                Assert.True(b1.Contains("f0"));
                Assert.Equal("a", b1["f0"]);
                Assert.True(b1.Contains("f2"));
                Assert.Equal("d", b1["f2"]);

                Assert.True(b1.Contains("b2"));
                var b2List = (IList)b1["b2"];
                Assert.NotNull(b2List);
                Assert.Equal(2, b2List.Count);
                var b2Map0 = (IDictionary)b2List[0];
                Assert.NotNull(b2Map0);
                Assert.True(b2Map0.Contains("f1"));
                Assert.Equal("b", b2Map0["f1"]);
                b1 = (IDictionary)b2List[1];
                Assert.NotNull(b1);
                Assert.True(b1.Contains("f1"));
                Assert.Equal("c", b1["f1"]);

                b1 = (IDictionary)list[1];
                Assert.True(b1.Contains("f0"));
                Assert.Equal("e", b1["f0"]);
                Assert.True(b1.Contains("f2"));
                Assert.Equal("h", b1["f2"]);

                Assert.True(b1.Contains("b2"));
                b2List = (IList)b1["b2"];
                Assert.NotNull(b2List);
                Assert.Equal(2, b2List.Count);
                b2Map0 = (IDictionary)b2List[0];
                Assert.NotNull(b2Map0);
                Assert.True(b2Map0.Contains("f1"));
                Assert.Equal("f", b2Map0["f1"]);
                b1 = (IDictionary)b2List[1];
                Assert.NotNull(b1);
                Assert.True(b1.Contains("f1"));
                Assert.Equal("g", b1["f1"]);

                var text = new StringWriter();
                factory.CreateWriter("w10", text).Write(map);
                Assert.Equal("a b c d e f g h eof" + Environment.NewLine, text.ToString());
            }
            finally
            {
                reader.Close();
            }
        }

        private static TextReader LoadStream(string fileName)
        {
            var resourceName = string.Format("BeanIO.Parser.Bean.{0}", fileName);
            var asm = typeof(BeanParserTest).Assembly;
            var resStream = asm.GetManifestResourceStream(resourceName);
            return new StreamReader(resStream);
        }
    }
}
