﻿using BeanIO.Internal.Util;
using BeanIO.Types.Xml;

using Xunit;

namespace BeanIO.Types
{
    public class XmlBooleanTypeHandlerTest
    {
        private readonly TypeHandlerFactory _factory = new TypeHandlerFactory();

        [Fact]
        public void TestParse()
        {
            var handler = _factory.GetTypeHandlerFor(typeof(bool), "xml");
            Assert.Equal(true, handler.Parse("true"));
            Assert.Equal(true, handler.Parse("1"));
            Assert.Equal(false, handler.Parse("false"));
            Assert.Equal(false, handler.Parse("0"));
            Assert.Null(handler.Parse(string.Empty));
            Assert.Null(handler.Parse(null));
        }

        [Fact]
        public void TestTextualFormat()
        {
            var handler = new XmlBooleanTypeHandler();
            Assert.False(handler.IsNumericFormatEnabled);
            Assert.Null(handler.Format(null));
            Assert.Equal("false", handler.Format(false));
            Assert.Equal("true", handler.Format(true));
        }

        [Fact]
        public void TestNumericFormat()
        {
            var handler = new XmlBooleanTypeHandler() { IsNumericFormatEnabled = true };
            Assert.True(handler.IsNumericFormatEnabled);
            Assert.Null(handler.Format(null));
            Assert.Equal("0", handler.Format(false));
            Assert.Equal("1", handler.Format(true));
        }
    }
}
