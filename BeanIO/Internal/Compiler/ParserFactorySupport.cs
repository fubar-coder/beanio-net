﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BeanIO.Builder;
using BeanIO.Internal.Compiler.Accessor;
using BeanIO.Internal.Config;
using BeanIO.Internal.Parser;
using BeanIO.Internal.Util;

using JetBrains.Annotations;

namespace BeanIO.Internal.Compiler
{
    /// <summary>
    /// Base <see cref="IParserFactory"/> implementation
    /// </summary>
    /// <remarks>
    /// A <see cref="StreamConfig"/> is "compiled" into a <see cref="Parser.Stream"/> in two passes.  First, a
    /// <see cref="Preprocessor"/> is used to validate and set default configuration settings. And
    /// secondly, the finalized configuration is walked again (using a <see cref="ProcessorSupport"/>,
    /// to create the parser and property tree structure.  As components are initialized they can
    /// be added to the tree structure using stacks with the <see cref="PushParser"/> and
    /// <see cref="PushProperty"/> methods.  After a component is finalized, it should be
    /// removed from the stack using the <see cref="PopParser"/> or <see cref="PopProperty"/> method.
    /// </remarks>
    public abstract class ParserFactorySupport : ProcessorSupport, IParserFactory
    {
        private static readonly string CONSTRUCTOR_PREFIX = "#";

        private static readonly bool AllowProtectedPropertyAccess = Settings.Instance.GetBoolean(Settings.ALLOW_PROTECTED_PROPERTY_ACCESS);

        private static readonly Component _unbound = new UnboundComponent();

        private readonly Stack<Component> _parserStack = new Stack<Component>();

        private readonly Stack<Component> _propertyStack = new Stack<Component>();

        private IPropertyAccessorFactory _accessorFactory;

        private Parser.Stream _stream;

        private string _streamFormat;

        private bool _readEnabled = true;

        private bool _writeEnabled = true;

        /// <summary>
        /// Gets or sets the type handler factory to use for resolving type handlers
        /// </summary>
        public TypeHandlerFactory TypeHandlerFactory { get; set; }

        /// <summary>
        /// Gets a value indicating whether a property has been pushed onto the property stack, indicating
        /// that further properties will be bound to a parent property.
        /// </summary>
        protected bool IsBound
        {
            get { return _propertyStack.Count != 0 && _propertyStack.Peek() != _unbound; }
        }

        /// <summary>
        /// Creates a new stream parser from a given stream configuration
        /// </summary>
        /// <param name="config">the stream configuration</param>
        /// <returns>the created <see cref="Parser.Stream"/></returns>
        public Parser.Stream CreateStream(StreamConfig config)
        {
            if (config.Name == null)
                throw new BeanIOConfigurationException("stream name not configured");

            // pre-process configuration settings to set defaults and validate as much as possible 
            CreatePreprocessor(config).Process(config);

            _accessorFactory = new ReflectionAccessorFactory();

            try
            {
                Process(config);
            }
            catch (BeanIOConfigurationException)
            {
                // TODO: Use C# 6 exception filters
                throw;
            }
            catch (Exception ex)
            {
                throw new BeanIOConfigurationException(string.Format("Failed to compile stream '{0}'", config.Name), ex);
            }

            // calculate the heap size
            _stream.Init();

            return _stream;
        }

        /// <summary>
        /// Creates a stream configuration pre-processor
        /// </summary>
        /// <remarks>May be overridden to return a format specific version</remarks>
        /// <param name="config">the stream configuration to pre-process</param>
        /// <returns>the new <see cref="Preprocessor"/></returns>
        protected virtual Preprocessor CreatePreprocessor(StreamConfig config)
        {
            return new Preprocessor(config);
        }

        protected abstract IStreamFormat CreateStreamFormat(StreamConfig config);

        protected abstract IRecordFormat CreateRecordFormat(RecordConfig config);

        protected abstract IFieldFormat CreateFieldFormat(FieldConfig config);

        protected virtual void PushParser(Component component)
        {
            if (_parserStack.Count != 0)
                _parserStack.Peek().Add(component);
            _parserStack.Push(component);
        }

        protected virtual Component PopParser()
        {
            return _parserStack.Pop();
        }

        protected virtual void PushProperty(Component component)
        {
            if (IsBound && component != _unbound)
            {
                // add properties to their parent bean or Map
                var parent = _propertyStack.Peek();
                switch (((IProperty)parent).Type)
                {
                    case PropertyType.Simple:
                        throw new InvalidOperationException();
                    case PropertyType.Collection:
                    case PropertyType.Complex:
                    case PropertyType.Map:
                        parent.Add(component);
                        break;
                }

                // if the parent property is an array or collection, the parser already holds
                // a reference to the child component when pushParser was called
            }

            _propertyStack.Push(component);
        }

        protected virtual IProperty PopProperty()
        {
            var c = _propertyStack.Pop();
            if (c == _unbound)
                return null;

            var last = (IProperty)c;
            if (_propertyStack.Count != 0)
            {
                if (last.IsIdentifier)
                {
                    if (_propertyStack.Any(x => x != _unbound))
                        ((IProperty)_propertyStack.Peek()).IsIdentifier = true;
                }
            }

            return last;
        }

        /// <summary>
        /// Updates a <see cref="Bean"/>'s constructor if one or more of its properties are
        /// constructor arguments.
        /// </summary>
        /// <param name="bean">the <see cref="Bean"/> to check</param>
        protected virtual void UpdateConstructor(Bean bean)
        {
            var args = bean.Children.Cast<IProperty>().Where(x => x.Accessor.IsConstructorArgument).OrderBy(x => x.Accessor.ConstructorArgumentIndex).ToList();

            // return if no constructor arguments were found
            if (args.Count == 0)
                return;

            var count = args.Count;

            // verify the number of constructor arguments matches the provided constructor index
            if (count != args[count - 1].Accessor.ConstructorArgumentIndex + 1)
                throw new BeanIOConfigurationException(string.Format("Missing constructor argument for bean class '{0}'", bean.GetType().GetFullName()));

            // find a suitable constructor
            ConstructorInfo constructor = null;
            foreach (var testConstructor in bean.GetType().GetTypeInfo().DeclaredConstructors.Where(x => x.GetParameters().Length == count))
            {
                var argsMatching = testConstructor.GetParameters().Select((p, i) => p.ParameterType.IsAssignableFrom(args[i].PropertyType)).All(x => x);
                if (argsMatching && (testConstructor.IsPublic || AllowProtectedPropertyAccess))
                {
                    constructor = testConstructor;
                    break;
                }
            }

            // verify a constructor was found
            if (constructor == null)
                throw new BeanIOConfigurationException(string.Format("No suitable constructor found for bean class '{0}'", bean.PropertyType.GetFullName()));

            bean.Constructor = constructor;
        }

        /// <summary>
        /// Initializes a stream configuration before its children have been processed
        /// </summary>
        /// <param name="config">the stream configuration to process</param>
        protected override void InitializeStream(StreamConfig config)
        {
            _streamFormat = config.Format;
            var format = CreateStreamFormat(config);
            _stream = new Parser.Stream(format);

            // set the stream mode, defaults to read/write, the stream mode may be used
            // to enforce or relax validation rules specific to marshalling or unmarshalling
            var mode = config.Mode;
            if (mode == null || mode == AccessMode.ReadWrite)
            {
                _stream.Mode = AccessMode.ReadWrite;
            }
            else if (mode == AccessMode.Read)
            {
                _stream.Mode = AccessMode.Read;
                _writeEnabled = false;
            }
            else if (mode == AccessMode.Write)
            {
                _stream.Mode = AccessMode.Write;
                _readEnabled = false;
            }
            else
            {
                throw new BeanIOConfigurationException(string.Format("Invalid mode '{0}'", mode));
            }
        }

        private class UnboundComponent : Component
        {
            public UnboundComponent()
            {
                Name = "unbound";
            }
        }
    }
}
