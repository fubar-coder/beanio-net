﻿using BeanIO.Internal.Config;
using BeanIO.Stream;
using BeanIO.Stream.FixedLength;

namespace BeanIO.Builder
{
    /// <summary>
    /// Builder for fixed length stream parsers.
    /// </summary>
    public class FixedLengthParserBuilder : IParserBuilder
    {
        private readonly FixedLengthRecordParserFactory _parser = new FixedLengthRecordParserFactory();

        /// <summary>
        /// Sets the record terminator.
        /// </summary>
        /// <param name="terminator">the record termination character</param>
        /// <returns>the current builder instance</returns>
        public FixedLengthParserBuilder RecordTerminator(string terminator)
        {
            _parser.RecordTerminator = terminator;
            return this;
        }

        /// <summary>
        /// Enables the given line continuation character.
        /// </summary>
        /// <param name="c">the line continuation character</param>
        /// <returns>the current builder instance</returns>
        public FixedLengthParserBuilder EnableLineContinuation(char c)
        {
            _parser.LineContinuationCharacter = c;
            return this;
        }

        /// <summary>
        /// Enables one or more line prefixes that indicate a commented line.
        /// </summary>
        /// <param name="comments">the list of prefixes</param>
        /// <returns>the current builder instance</returns>
        public FixedLengthParserBuilder EnableComments(params string[] comments)
        {
            _parser.Comments = comments;
            return this;
        }

        public BeanConfig<IRecordParserFactory> Build()
        {
            var config = new BeanConfig<IRecordParserFactory>(() => _parser);
            return config;
        }
    }
}
