// <copyright file="BeanUtil.cs" company="Fubar Development Junker">
// Copyright (c) 2016 Fubar Development Junker. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using BeanIO.Config;
using BeanIO.Types;

using JetBrains.Annotations;

namespace BeanIO.Internal.Util
{
    /// <summary>
    /// Utility class for instantiating configurable bean classes
    /// </summary>
    internal class BeanUtil
    {
        private static readonly ConcurrentDictionary<Type, MethodInfo> _addMethods = new ConcurrentDictionary<Type, MethodInfo>();

        private readonly TypeHandlerFactory _typeHandlerFactory = new TypeHandlerFactory();

        public BeanUtil(ISettings settings)
        {
            var nullEscapingEnabled = settings.GetBoolean(ConfigurationKeys.NULL_ESCAPING_ENABLED);
            _typeHandlerFactory.RegisterHandlerFor(typeof(string[]), () => new StringArrayTypeHandler());
            if (settings.GetBoolean(ConfigurationKeys.PROPERTY_ESCAPING_ENABLED))
            {
                _typeHandlerFactory.RegisterHandlerFor(typeof(string), () => new EscapedStringTypeHandler(nullEscapingEnabled));
                _typeHandlerFactory.RegisterHandlerFor(typeof(char), () => new EscapedCharacterTypeHandler(nullEscapingEnabled));
            }
        }

        public static object CreateBean([NotNull] string className)
        {
            if (className == null)
                throw new ArgumentNullException(nameof(className));

            Type type;
            try
            {
                // load the class
                type = Type.GetType(className, true);
            }
            catch (Exception ex)
            {
                throw new BeanIOConfigurationException($"Class not found '{className}'", ex);
            }

            try
            {
                // instantiate an instance of the class
                return type.NewInstance();
            }
            catch (Exception ex)
            {
                throw new BeanIOConfigurationException($"Could not instantiate class '{type}'", ex);
            }
        }

        public static object CreateBean([NotNull] string className, IReadOnlyCollection<object> availableObjects)
        {
            if (availableObjects == null || availableObjects.Count == 0)
                return CreateBean(className);
            if (className == null)
                throw new ArgumentNullException(nameof(className));

            Type type;
            try
            {
                // load the class
                type = Type.GetType(className, true);
            }
            catch (Exception ex)
            {
                throw new BeanIOConfigurationException($"Class not found '{className}'", ex);
            }

            try
            {
                // instantiate an instance of the class
                var match = GetBestConstructorMatch(type, availableObjects);
                if (match == null)
                    return type.NewInstance();
                return match.Constructor.Invoke(match.Arguments.ToArray());
            }
            catch (Exception ex)
            {
                throw new BeanIOConfigurationException($"Could not instantiate class '{type}'", ex);
            }
        }

        public static PropertyDescriptor GetPropertyDescriptor(Type type, string property, string getter, string setter, bool isConstructorArgument)
        {
            var detector = new PropertyDescriptorDetector(type, property, getter, setter, isConstructorArgument);
            return detector.Create();
        }

        public void Add(IEnumerable collection, object value)
        {
            var t = collection.GetType();
            var addMethod = _addMethods.GetOrAdd(
                t,
                collectionType =>
                {
                    var i = collectionType.GetTypeInfo();
                    var foundAddMethod =
                        i.DeclaredMethods.First(
                            x => x.IsPublic && !x.IsStatic && x.Name == "Add" && x.GetParameters().Length == 1);
                    return foundAddMethod;
                });
            addMethod.Invoke(collection, new[] { value });
        }

        public object CreateBean([NotNull] string className, Properties properties)
        {
            var bean = CreateBean(className);
            Configure(bean, properties);
            return bean;
        }

        public void Configure(object bean, Properties properties)
        {
            // if no properties, we're done...
            if (properties == null || properties.Count == 0)
                return;

            var type = bean.GetType();

            foreach (var property in properties)
            {
                var name = property.Key;
                var descriptor = GetPropertyDescriptor(type, name, null, null, false);

                var handler = _typeHandlerFactory.GetTypeHandlerFor(descriptor.PropertyType);
                if (handler == null)
                {
                    throw new BeanIOConfigurationException(
                        $"Property type '{descriptor.PropertyType}' not supported for property '{name}' on class '{type}'");
                }

                try
                {
                    var value = handler.Parse(property.Value);
                    if (value.CanSetTo(descriptor.PropertyType))
                        descriptor.SetValue(bean, value);
                }
                catch (FormatException ex)
                {
                    throw new BeanIOConfigurationException($"Type conversion failed for property '{name}' on class '{type}': {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    throw new BeanIOConfigurationException($"Failed to set property '{name}' on class '{type}': {ex.Message}", ex);
                }
            }
        }

        internal static ConstructorMatch GetBestConstructorMatch(Type type, IReadOnlyCollection<object> arguments)
        {
            return type.GetTypeInfo().DeclaredConstructors
                .Select(x => new ConstructorMatch(x, arguments))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault(x => x.Score > 0);
        }

        internal class ConstructorMatch
        {
            public ConstructorMatch(ConstructorInfo constructor, IReadOnlyCollection<object> arguments)
            {
                var score = 0;
                var parameters = constructor.GetParameters();
                var constructorArgs = new List<object>(parameters.Length);
                foreach (var parameter in parameters)
                {
                    var matchingArgument = arguments.FirstOrDefault(x => x != null && x.GetType().IsInstanceOf(parameter.ParameterType));
                    constructorArgs.Add(matchingArgument);
                    if (matchingArgument != null)
                    {
                        score += 1;
                    }
                    else
                    {
                        if (parameter.ParameterType.GetTypeInfo().IsValueType)
                        {
                            if (Nullable.GetUnderlyingType(parameter.ParameterType) != null)
                            {
                                score -= 2;
                            }
                            else
                            {
                                score -= 100;
                            }
                        }
                        else
                        {
                            score -= 1;
                        }
                    }
                }

                Constructor = constructor;
                Arguments = constructorArgs;
                Score = score;
            }

            public ConstructorInfo Constructor { get; }

            public IReadOnlyCollection<object> Arguments { get; }

            public int Score { get; }
        }

        private class PropertyDescriptorDetector
        {
            private readonly TypeInfo _typeInfo;

            private readonly string _property;

            private readonly string _getter;

            private readonly string _setter;

            private readonly bool _isConstructorArgument;

            private MethodInfo _getterInfo;

            private MethodInfo _setterInfo;

            public PropertyDescriptorDetector(Type type, string property, string getter, string setter, bool isConstructorArgument)
            {
                _typeInfo = type.GetTypeInfo();
                _property = property;
                _getter = getter;
                _setter = setter;
                _isConstructorArgument = isConstructorArgument;
            }

            public PropertyDescriptor Create()
            {
                if (!string.IsNullOrEmpty(_getter))
                {
                    _getterInfo = LoadMethodInfo(_getter);
                    if (_getterInfo == null)
                    {
                        _getterInfo = FindGetter(RemovePrefixForGetter(_getter));
                        if (_getterInfo == null)
                            ThrowMethodMissingException("Getter", _getter);
                    }
                }

                if (!string.IsNullOrEmpty(_setter))
                {
                    _setterInfo = LoadMethodInfo(_setter);
                    if (_setterInfo == null)
                    {
                        _setterInfo = FindSetter(RemovePrefixForSetter(_setter));
                        if (_setterInfo == null)
                            ThrowMethodMissingException("Setter", _setter);
                    }
                }

                var propertyNamesToTest = new[]
                    {
                        _property,
                        Introspector.Capitalize(_property),
                        Introspector.Decapitalize(_property),
                        "_" + Introspector.Decapitalize(_property),
                        "m_" + Introspector.Decapitalize(_property),
                    };
                PropertyInfo propertyInfo = null;
                FieldInfo fieldInfo = null;
                foreach (var propertyName in propertyNamesToTest)
                {
                    propertyInfo = GetDeclaredProperty(propertyName);
                    fieldInfo = GetDeclaredField(propertyName);
                    if (propertyInfo != null || fieldInfo != null)
                        break;
                }

                if (propertyInfo == null && fieldInfo == null && !_isConstructorArgument)
                {
                    if (_getterInfo == null)
                        _getterInfo = FindGetter(_property);
                    if (_setterInfo == null)
                        _setterInfo = FindSetter(_property);
                }

                if (_getterInfo != null && _setterInfo == null)
                {
                    _setterInfo = FindSetterForGetter(_getterInfo.Name);
                }
                else if (_setterInfo != null && _getterInfo == null)
                {
                    _getterInfo = FindGetterForSetter(_setterInfo.Name);
                }

                PropertyDescriptor descriptor;
                if (propertyInfo != null)
                {
                    descriptor = new PropertyDescriptor(propertyInfo, _getterInfo, _setterInfo);
                }
                else if (fieldInfo == null)
                {
                    if (!_isConstructorArgument && _setterInfo == null && _getterInfo == null)
                    {
                        throw new BeanIOConfigurationException(
                            $"Neither property or field found with name '{_property}' for type '{_typeInfo.AssemblyQualifiedName}'");
                    }

                    descriptor = new PropertyDescriptor(_property, _getterInfo, _setterInfo);
                }
                else
                {
                    descriptor = new PropertyDescriptor(fieldInfo, _getterInfo, _setterInfo);
                }

                return descriptor;
            }

            private PropertyInfo GetDeclaredProperty(string name)
            {
                var typeInfo = _typeInfo;
                while (typeInfo.AsType() != typeof(object))
                {
                    var result = typeInfo
                        .DeclaredProperties
                        .Where(x => !(x.GetMethod ?? x.SetMethod).IsStatic)
                        .SingleOrDefault(x => x.Name == name);
                    if (result != null)
                        return result;
                    if (typeInfo.BaseType == null)
                        break;
                    typeInfo = typeInfo.BaseType.GetTypeInfo();
                }

                return null;
            }

            private FieldInfo GetDeclaredField(string name)
            {
                var typeInfo = _typeInfo;
                while (typeInfo.AsType() != typeof(object))
                {
                    var result = typeInfo
                    .DeclaredFields
                    .Where(x => !x.IsStatic)
                        .SingleOrDefault(x => x.Name == name);
                    if (result != null)
                        return result;
                    if (typeInfo.BaseType == null)
                        break;
                    typeInfo = typeInfo.BaseType.GetTypeInfo();
                }

                return null;
            }

            private string RemovePrefixForGetter(string getterName)
            {
                var name = getterName;
                if (name.StartsWith("get", StringComparison.Ordinal) || name.StartsWith("Get", StringComparison.Ordinal))
                {
                    name = name.Substring(3);
                }
                else if (name.StartsWith("is", StringComparison.Ordinal) || name.StartsWith("Is", StringComparison.Ordinal))
                {
                    name = name.Substring(2);
                }

                return name;
            }

            private string RemovePrefixForSetter(string setterName)
            {
                var name = setterName;
                if (name.StartsWith("set", StringComparison.Ordinal) || name.StartsWith("Set", StringComparison.Ordinal))
                {
                    name = name.Substring(3);
                }

                return name;
            }

            private MethodInfo FindSetterForGetter(string getterName)
            {
                var name = RemovePrefixForGetter(getterName);
                return FindSetter(name);
            }

            private MethodInfo FindGetterForSetter(string setterName)
            {
                var name = RemovePrefixForSetter(setterName);
                return FindGetter(name);
            }

            private MethodInfo FindGetter(string name)
            {
                var probableGetterNames = new[]
                    {
                        Introspector.Capitalize(name),
                        "Get" + Introspector.Capitalize(name),
                        "get_" + Introspector.Capitalize(name),
                        "get_" + name,
                    };
                foreach (var getterName in probableGetterNames)
                {
                    var info = LoadMethodInfo(getterName);
                    if (info != null && info.GetParameters().Length == 0)
                    {
                        return info;
                    }
                }

                return null;
            }

            private MethodInfo FindSetter(string name)
            {
                var probableSetterNames = new[]
                    {
                        Introspector.Capitalize(name),
                        "Set" + Introspector.Capitalize(name),
                        "set_" + Introspector.Capitalize(name),
                        "set_" + name,
                    };
                foreach (var setterName in probableSetterNames)
                {
                    var info = LoadMethodInfo(setterName);
                    if (info != null && info.GetParameters().Length == 1)
                    {
                        return info;
                    }
                }

                return null;
            }

            private MethodInfo LoadMethodInfo(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;
                MethodInfo methodInfo = null;
                var typeInfo = _typeInfo;
                while (typeInfo.GetType() != typeof(object))
                {
                    methodInfo = typeInfo.GetDeclaredMethod(name);
                    if (methodInfo != null)
                        break;
                    if (typeInfo.BaseType == null)
                        break;
                    typeInfo = typeInfo.BaseType.GetTypeInfo();
                }

                return methodInfo;
            }

            [ContractAnnotation("=> halt")]
            private void ThrowMethodMissingException(string getterOrSetter, string name)
            {
                if (string.IsNullOrEmpty(_property))
                {
                    throw new BeanIOConfigurationException(
                        string.Format(
                            "{2} '{0}' not found in type '{1}'",
                            name,
                            _typeInfo.GetType().GetAssemblyQualifiedName(),
                            getterOrSetter));
                }

                throw new BeanIOConfigurationException(
                    string.Format(
                        "{3} '{0}' not found for property/field '{1}' of type '{2}'",
                        name,
                        _property,
                        _typeInfo.GetType().GetAssemblyQualifiedName(),
                        getterOrSetter));
            }
        }

        private class EscapedCharacterTypeHandler : ITypeHandler
        {
            private readonly bool _nullEscapingEnabled;

            public EscapedCharacterTypeHandler(bool nullEscapingEnabled)
            {
                _nullEscapingEnabled = nullEscapingEnabled;
            }

            /// <summary>
            /// Gets the class type supported by this handler.
            /// </summary>
            public Type TargetType => typeof(char);

            /// <summary>
            /// Parses field text into an object.
            /// </summary>
            /// <param name="text">The field text to parse, which may be null if the field was not passed in the record</param>
            /// <returns>The parsed object</returns>
            public object Parse(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return null;

                var ch = text[0];
                if (text.Length == 1 || ch != '\\')
                    return ch;

                ch = text[1];
                switch (ch)
                {
                    case '\\':
                        return '\\';
                    case 'n':
                        return '\n';
                    case 'r':
                        return '\r';
                    case 't':
                        return '\t';
                    case 'f':
                        return '\f';
                    case '0':
                        if (_nullEscapingEnabled)
                            return '\0';
                        break;
                }

                throw new FormatException($"Invalid character '{ch}'");
            }

            /// <summary>
            /// Formats an object into field text.
            /// </summary>
            /// <param name="value">The value to format, which may be null</param>
            /// <returns>The formatted field text, or <code>null</code> to indicate the value is not present</returns>
            public string Format(object value)
            {
                throw new NotSupportedException();
            }
        }

        private class EscapedStringTypeHandler : ITypeHandler
        {
            private readonly bool _nullEscapingEnabled;

            public EscapedStringTypeHandler(bool nullEscapingEnabled)
            {
                _nullEscapingEnabled = nullEscapingEnabled;
            }

            /// <summary>
            /// Gets the class type supported by this handler.
            /// </summary>
            public Type TargetType => typeof(string);

            /// <summary>
            /// Parses field text into an object.
            /// </summary>
            /// <param name="text">The field text to parse, which may be null if the field was not passed in the record</param>
            /// <returns>The parsed object</returns>
            public object Parse(string text)
            {
                if (text == null)
                    return null;

                var n = text.IndexOf('\\') + 1;
                if (n == 0)
                    return text;

                var start = 0;
                var value = new StringBuilder();
                var escaped = true;
                while (n < text.Length && n != -1)
                {
                    if (escaped)
                    {
                        value.Append(text.Substring(start, n - start - 1));
                        var c = text[n];
                        switch (c)
                        {
                            case 'n':
                                value.Append('\n');
                                break;
                            case 'r':
                                value.Append('\r');
                                break;
                            case 't':
                                value.Append('\t');
                                break;
                            case 'f':
                                value.Append('\f');
                                break;
                            case '0':
                                if (_nullEscapingEnabled)
                                {
                                    value.Append('\0');
                                }
                                else
                                {
                                    value.Append(c);
                                }

                                break;

                            default:
                                value.Append(c);
                                break;
                        }

                        escaped = false;
                        start = n + 1;
                        n = text.IndexOf('\\', start);
                    }
                    else
                    {
                        escaped = true;
                        n = n + 1;
                    }
                }

                if (start < text.Length)
                    value.Append(text.Substring(start, text.Length - start - (escaped ? 1 : 0)));

                return value.ToString();
            }

            /// <summary>
            /// Formats an object into field text.
            /// </summary>
            /// <param name="value">The value to format, which may be null</param>
            /// <returns>The formatted field text, or <code>null</code> to indicate the value is not present</returns>
            public string Format(object value)
            {
                throw new NotSupportedException();
            }
        }

        private class StringArrayTypeHandler : ITypeHandler
        {
            /// <summary>
            /// Gets the class type supported by this handler.
            /// </summary>
            public Type TargetType => typeof(string[]);

            /// <summary>
            /// Parses field text into an object.
            /// </summary>
            /// <param name="text">The field text to parse, which may be null if the field was not passed in the record</param>
            /// <returns>The parsed object</returns>
            public object Parse(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return null;

                var pos = text.IndexOf(',');
                if (pos < 0)
                    return new[] { text };

                var escaped = false;
                var item = new StringBuilder();
                var list = new List<string>();

                var ca = text.ToCharArray();
                foreach (var c in ca)
                {
                    if (escaped)
                    {
                        item.Append(c);
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == ',')
                    {
                        list.Add(item.ToString());
                        item = new StringBuilder();
                    }
                    else
                    {
                        item.Append(c);
                    }
                }

                list.Add(item.ToString());

                return list.ToArray();
            }

            /// <summary>
            /// Formats an object into field text.
            /// </summary>
            /// <param name="value">The value to format, which may be null</param>
            /// <returns>The formatted field text, or <code>null</code> to indicate the value is not present</returns>
            public string Format(object value)
            {
                throw new NotSupportedException();
            }
        }
    }
}
