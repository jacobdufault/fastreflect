using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FastReflect.Internal;

namespace FastReflect {
    public class frManager {
        public static frManager Instance = new frManager(typeof(frTypeProviderRegistry));

        private static frMethod.AotData[] EmptyMethodAot = new frMethod.AotData[0];

        private Dictionary<Type, frType> _typeToFrTypeCache;
        private Dictionary<Type, Dictionary<string, frField.AotData>> _fieldAot;
        private Dictionary<Type, Dictionary<string, frMethod.AotData[]>> _methodAot;

        public frManager(params Type[] aotProviderTypes) {
            _typeToFrTypeCache = new Dictionary<Type, frType>();
            _fieldAot = new Dictionary<Type, Dictionary<string, frField.AotData>>();
            _methodAot = new Dictionary<Type, Dictionary<string, frMethod.AotData[]>>();

            GetAotData(aotProviderTypes);
        }

        public frType Get(Type type) {
            frType result;
            if (_typeToFrTypeCache.TryGetValue(type, out result) == false) {
                result = new frType();
                // Immediately insert result into the cache so if there are any
                // cyclic calls we resolve to |result|.
                _typeToFrTypeCache[type] = result;

                result.RawType = type;
                if (type.Resolve().BaseType != null)
                    result.Parent = Get(type.Resolve().BaseType);
                result.Interfaces = type.GetInterfaces().Select(t => Get(t)).ToArray();
                GetFieldsAndMethods(type, out result.Fields, out result.Methods);
            }
            return result;
        }

        private void GetAotData(Type[] providerSourceTypes) {
            foreach (Type providerSourceType in providerSourceTypes) {
                foreach (FieldInfo field in providerSourceType.GetDeclaredFields()) {
                    if (!field.Name.StartsWith("Provider_"))
                        continue;
                    if (!field.IsStatic) {
                        throw new InvalidOperationException(
                            string.Format("Expected {0}.{1} to be static", providerSourceType.CSharpName(), field.Name));
                    }

                    object result_ = field.GetValue(null);
                    if (result_ is frTypeAotData == false) {
                        throw new InvalidOperationException(
                            string.Format("Expected {0}.{1} to return a frTypeAotData instance", providerSourceType.CSharpName(), field.Name));
                    }
                    var result = (frTypeAotData)result_;

                    if (_fieldAot.ContainsKey(result.ProviderFor) || _methodAot.ContainsKey(result.ProviderFor)) {
                        throw new InvalidOperationException(
                            string.Format("Multiple aot providers for {0}", result.ProviderFor.CSharpName()));
                    }
                    _fieldAot[result.ProviderFor] = result.FieldAotData.ToDictionary(v => v.FieldName);
                    _methodAot[result.ProviderFor] = result.MethodAotData.GroupBy(v => v.MethodName).ToDictionary(k => k.Key, v => v.ToArray());
                }
            }
        }

        private frField.AotData GetFieldAotDataFor(Type type, string fieldName) {
            var aot = new frField.AotData();
            Dictionary<string, frField.AotData> typeFieldAot;
            if (_fieldAot.TryGetValue(type, out typeFieldAot))
                typeFieldAot.TryGetValue(fieldName, out aot);
            return aot;
        }

        private frMethod.AotData[] GetMethodAotDataFor(Type type, string methodName) {
            frMethod.AotData[] aot = EmptyMethodAot;
            Dictionary<string, frMethod.AotData[]> typeMethodAot;
            if (_methodAot.TryGetValue(type, out typeMethodAot))
                typeMethodAot.TryGetValue(methodName, out aot);
            return aot;
        }

        private bool DoParamsMatch(frMethod.AotData aotData, MethodInfo method) {
            return Enumerable.SequenceEqual(
                aotData.Parameters,
                method.GetParameters().Select(p => p.ParameterType));
        }

        private void GetFieldsAndMethods(Type type, out frField[] fields, out frMethod[] methods) {
            var fields0 = new List<frField>();
            var methods0 = new List<frMethod>();

            foreach (MemberInfo member in type.GetDeclaredMembers()) {
                PropertyInfo property = member as PropertyInfo;
                FieldInfo field = member as FieldInfo;

                // Properties
                if (property != null) {
                    // If either the get or set methods are overridden, then the
                    // property is not considered local and will appear on a
                    // parent type.
                    var getMethod = property.GetGetMethod(/*nonPublic:*/ true);
                    var setMethod = property.GetSetMethod(/*nonPublic:*/ true);
                    if ((getMethod != null && getMethod != getMethod.GetBaseDefinition()) ||
                        (setMethod != null && setMethod != setMethod.GetBaseDefinition())) {
                        continue;
                    }

                    fields0.Add(new frField(property, GetFieldAotDataFor(type, property.Name)));
                }

                // Fields
                else if (field != null) {
                    fields0.Add(new frField(field, GetFieldAotDataFor(type, field.Name)));
                }
            }

            foreach (MethodInfo method in type.GetDeclaredMethods()) {
                // This is a method override. Skip it as it is not a "local"
                // property -- it will appear in a parent type.
                if (method != method.GetBaseDefinition())
                    continue;
                // Skip any compiler-generated methods, ie, auto-properties.
                if (method.GetCustomAttributes(typeof(CompilerGeneratedAttribute), /*inherit:*/false).IsNullOrEmpty() == false)
                    continue;

                // Find correct method aot data to use. Types can define
                // multiple methods with the same name if they have different
                // parameters.
                frMethod.AotData aotData = default(frMethod.AotData);
                frMethod.AotData[] aotDataCandidates = GetMethodAotDataFor(type, method.Name);
                if (aotDataCandidates.Length == 1) {
                    aotData = aotDataCandidates[0];
                }
                else {
                    foreach (frMethod.AotData candidate in aotDataCandidates) {
                        if (DoParamsMatch(candidate, method)) {
                            aotData = candidate;
                            break;
                        }
                    }
                }
                methods0.Add(new frMethod(method, aotData));
            }

            fields = fields0.ToArray();
            methods = methods0.ToArray();
        }
    }
}