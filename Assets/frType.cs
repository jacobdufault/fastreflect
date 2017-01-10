using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FastReflect {
    // Wraps a general Type. Provides a consistent API across platforms.
    // Various operations can be accelerated so they are much faster by
    // using build process baking.
    [DebuggerDisplay("{RawType}")]
    public class frType {
        private static readonly frField[] EmptyFields = new frField[0];
        private static readonly frMethod[] EmptyMethods = new frMethod[0];

        // Public API: metadata, raw data access
        public Type RawType;
        public frType Parent;
        /// <summary>
        /// Contains all interfaces on the type. This includes all interfaces
        /// defined on the parent type as well (including explicit interfaces).
        /// </summary>
        public frType[] Interfaces;
        public frField[] Fields;
        public frMethod[] Methods;

        // Caching.
        private Dictionary<string, frField> _declaredNameToField;
        private Dictionary<string, frMethod[]> _declaredNameToMethods;
        private Dictionary<frIPredicate, frField[]> _declaredPredicateToFields;
        private Dictionary<frIPredicate, frMethod[]> _declaredPredicateToMethods;
        private Dictionary<string, frField[]> _flattenedNameToFields;
        private Dictionary<string, frMethod[]> _flattenedNameToMethods;
        private Dictionary<frIPredicate, frField[]> _flattenedPredicateToFields;
        private Dictionary<frIPredicate, frMethod[]> _flattenedPredicateToMethods;

        // Initialization.
        private bool _initialized;
        private void EnsureInitialized() {
            if (_initialized)
                return;
            _initialized = true;

            _declaredNameToField = Fields.ToDictionary(v => v.Name);
            _declaredNameToMethods = new Dictionary<string, frMethod[]>();
            AppendMultiDict(_declaredNameToMethods, Methods, v => v.RawMethod.Name);
            _declaredPredicateToFields = new Dictionary<frIPredicate, frField[]>();
            _declaredPredicateToMethods = new Dictionary<frIPredicate, frMethod[]>();

            _flattenedNameToFields = new Dictionary<string, frField[]>();
            _flattenedNameToMethods = new Dictionary<string, frMethod[]>();
            _flattenedPredicateToFields = new Dictionary<frIPredicate, frField[]>();
            _flattenedPredicateToMethods = new Dictionary<frIPredicate, frMethod[]>();

            frType type = this;
            while (type != null) {
                AppendMultiDict(_flattenedNameToFields, type.Fields, v => v.Name);
                AppendMultiDict(_flattenedNameToMethods, type.Methods, v => v.RawMethod.Name);
                type = type.Parent;
            }
        }

        // Public API: declared fields/methods
        public frField GetDeclaredFieldByName(string name) {
            EnsureInitialized();
            frField result;
            if (!_declaredNameToField.TryGetValue(name, out result))
                result = null;
            return _declaredNameToField[name];
        }
        public frMethod[] GetDeclaredMethodsByName(string name) {
            EnsureInitialized();
            frMethod[] result;
            if (!_declaredNameToMethods.TryGetValue(name, out result))
                result = null;
            return _declaredNameToMethods[name];
        }
        public frField[] GetDeclaredFieldsByPredicate(frIPredicate predicate) {
            EnsureInitialized();
            frField[] result;
            if (!_declaredPredicateToFields.TryGetValue(predicate, out result))
                _declaredPredicateToFields[predicate] = result = RunPredicate(predicate, Fields).ToArray();
            return result;
        }
        public frMethod[] GetDeclaredMethodsByPredicate(frIPredicate predicate) {
            EnsureInitialized();
            frMethod[] result;
            if (!_declaredPredicateToMethods.TryGetValue(predicate, out result))
                _declaredPredicateToMethods[predicate] = result = RunPredicate(predicate, Methods).ToArray();
            return result;
        }

        // Public API: flattened fields/methods
        public frField[] GetFlattenedFieldsByName(string name) {
            EnsureInitialized();
            frField[] result;
            if (!_flattenedNameToFields.TryGetValue(name, out result))
                result = EmptyFields;
            return _flattenedNameToFields[name];
        }
        public frMethod[] GetFlattenedMethodsByName(string name) {
            EnsureInitialized();
            frMethod[] result;
            if (!_flattenedNameToMethods.TryGetValue(name, out result))
                result = EmptyMethods;
            return _flattenedNameToMethods[name];
        }
        public frField[] GetFlattenedFieldsByPredicate(frIPredicate predicate) {
            EnsureInitialized();
            frField[] result;
            if (!_flattenedPredicateToFields.TryGetValue(predicate, out result)) {
                var fields = new List<frField>();
                frType type = this;
                while (type != null) {
                    RunPredicate(predicate, type.Fields, fields);
                    type = type.Parent;
                }
                _flattenedPredicateToFields[predicate] = result = fields.ToArray();
            }
            return result;
        }
        public frMethod[] GetFlattenedMethodsByPredicate(frIPredicate predicate) {
            EnsureInitialized();
            frMethod[] result;
            if (!_flattenedPredicateToMethods.TryGetValue(predicate, out result)) {
                var members = new List<frMethod>();
                frType type = this;
                while (type != null) {
                    RunPredicate(predicate, type.Methods, members);
                    type = type.Parent;
                }
                _flattenedPredicateToMethods[predicate] = result = members.ToArray();
            }
            return result;
        }

        // Public API: Helpers to lookup an interface by type.
        public frType GetInterface<T>() {
            return GetInterface(typeof(T));
        }
        public frType GetInterface(Type type) {
            for (int i = 0; i < Interfaces.Length; ++i) {
                if (Interfaces[i].RawType == type)
                    return Interfaces[i];
            }
            return null;
        }

        // Various helpers.
        private static void AppendMultiDict<TKey, TValue>(Dictionary<TKey, TValue[]> dict, IEnumerable<TValue> enumerable, Func<TValue, TKey> selector) {
            foreach (TValue value in enumerable) {
                TKey key = selector(value);

                TValue[] values;
                if (dict.TryGetValue(key, out values)) {
                    Array.Resize(ref values, values.Length + 1);
                    values[values.Length - 1] = value;
                }
                else {
                    values = new[] { value };
                }

                dict[key] = values;
            }
        }
        private static List<T> RunPredicate<T>(frIPredicate predicate, T[] enumerable) where T : frMember {
            var result = new List<T>();
            RunPredicate(predicate, enumerable, result);
            return result;
        }
        private static void RunPredicate<T>(frIPredicate predicate, T[] enumerable, List<T> result) where T : frMember {
            foreach (T value in enumerable) {
                if (predicate.IsMatch(value))
                    result.Add(value);
            }
        }
    }
}