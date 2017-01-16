using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FastReflect {
    // A field on a class or struct. Note that a frField encapsulates *both*
    // fields and properties.
    [DebuggerDisplay("{RawMember}")]
    public class frField : frMember {
        public struct AotData {
            public string FieldName;
            public Reader Read;
            public Writer Write;
        }

        private static object[] EmptyArgs = new object[0];
        [ThreadStatic]
        private static object[] OneArg = new object[1];

        public MemberInfo RawMember;

        // field/property/auto-property
        public bool IsField;
        public bool IsProperty;
        public bool IsAutoProperty;

        // name/field type/attributes
        public string Name;
        public Type MemberType;
        public Attribute[] Attributes;

        // read/write values
        public delegate object Reader(object instance);
        public delegate void Writer(ref object instance, object value);
        public bool CanRead;
        public bool CanWrite;
        public Reader RawReader;
        public Writer RawWriter;
        public T Read<T>(object instance) {
            return (T)RawReader(instance);
        }
        public void Write<T>(ref T instance, object value) {
            object boxed = instance;
            RawWriter(ref boxed, value);
            instance = (T)boxed;
        }

        public frField(FieldInfo field, AotData aot) {
            RawMember = field;

            IsField = true;
            IsProperty = false;
            IsAutoProperty = false;

            Name = field.Name;
            MemberType = field.FieldType;
            Attributes = (Attribute[])field.GetCustomAttributes(/*inherit:*/true);

            CanRead = true;
            CanWrite = !field.IsLiteral;
            RawReader = aot.Read ?? field.GetValue;
            RawWriter = aot.Write ?? ((ref object o, object v) => field.SetValue(o, v));
        }

        public frField(PropertyInfo property, AotData aot) {
            RawMember = property;

            var getMethod = property.GetGetMethod(/*nonPublic:*/ true);
            var setMethod = property.GetSetMethod(/*nonPublic:*/ true);

            IsField = false;
            IsProperty = true;
            IsAutoProperty = getMethod != null &&
                             setMethod != null &&
                             getMethod.GetCustomAttributes(typeof(CompilerGeneratedAttribute), /*inherit:*/true) != null;

            Name = property.Name;
            MemberType = property.PropertyType;
            Attributes = property.GetCustomAttributes(/*inherit:*/true).OfType<Attribute>().ToArray();

            CanRead = getMethod != null;
            CanWrite = setMethod != null;
            RawReader = aot.Read ?? ((o) => property.GetValue(o, EmptyArgs));
            RawWriter = aot.Write ?? ((ref object o, object v) => { OneArg[0] = v; setMethod.Invoke(o, OneArg); OneArg[0] = null; });
        }
    }
}