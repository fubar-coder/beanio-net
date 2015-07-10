﻿using System;
using System.Globalization;

namespace BeanIO.Types
{
    /// <summary>
    /// Converts a <see cref="bool"/> to an <see cref="int"/> value.
    /// </summary>
    public class BooleanIntegerTypeHandler : CultureSupport, ITypeHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BooleanIntegerTypeHandler"/> class.
        /// </summary>
        public BooleanIntegerTypeHandler()
        {
            TrueValue = 1;
            FalseValue = 0;
        }

        /// <summary>
        /// Gets or sets the <see cref="int"/> value to be used for <code>true</code>
        /// </summary>
        public int TrueValue { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="int"/> value to be used for <code>false</code>
        /// </summary>
        public int? FalseValue { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="int"/> value to be used for <code>null</code>
        /// </summary>
        public int? NullValue { get; set; }

        /// <summary>
        /// Gets the class type supported by this handler.
        /// </summary>
        public Type TargetType
        {
            get { return typeof(bool); }
        }

        /// <summary>
        /// Parses field text into an object.
        /// </summary>
        /// <param name="text">The field text to parse, which may be null if the field was not passed in the record</param>
        /// <returns>The parsed object</returns>
        public virtual object Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            const NumberStyles styles = NumberStyles.Integer;
            int intValue;
            if (!int.TryParse(text, styles, Culture, out intValue))
                throw new FormatException(string.Format("Number value '{0}' doesn't match the number styles {1}", text, styles));

            if (intValue == TrueValue)
                return true;
            if (FalseValue.HasValue && FalseValue == intValue)
                return false;

            throw new FormatException(string.Format("Invalid value '{0}' for type '{1}'", text, TargetType.Name));
        }

        /// <summary>
        /// Formats an object into field text.
        /// </summary>
        /// <param name="value">The value to format, which may be null</param>
        /// <returns>The formatted field text, or <code>null</code> to indicate the value is not present</returns>
        public virtual string Format(object value)
        {
            if (value == null)
                return string.Format(Culture, "{0}", NullValue);
            var boolValue = (bool)value;
            return string.Format(Culture, "{0}", boolValue ? TrueValue : FalseValue);
        }
    }
}
