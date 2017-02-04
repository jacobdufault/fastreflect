using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using FastReflect.Internal;

namespace FastReflect.Internal {
    // Do not use - this is a JIT implementation detail and may change at any
    // time. This is used to fetch a fast delegate to a method inside of a
    // runtime-compiled type.
    public interface IAotFieldReader {
        object Read(object instance);
    }
}

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

            if (frConfig.HasJit && aot.Read == null)
                TryToAotCompileReader(field);
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

            if (frConfig.HasJit && aot.Read == null)
                TryToAotCompileReader(property);
        }

        private void TryToAotCompileReader(MemberInfo field) {
            TypeBuilder typeBuilder = frJitManager.Instance.CreateType(typeof(IAotFieldReader));
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                "Read",
                MethodAttributes.Public | MethodAttributes.Virtual,
                /*returnType:*/ typeof(object),
                /*parameterTypes:*/ new[] { typeof(object) });
            typeBuilder.DefineMethodOverride(
                methodBuilder,
                typeof(IAotFieldReader).GetMethod("Read"));

            if (field is FieldInfo)
                EmitFieldRead(methodBuilder.GetILGenerator(), (FieldInfo)field);
            else
                EmitPropertyRead(methodBuilder.GetILGenerator(), (PropertyInfo)field);


            Type jitType = typeBuilder.CreateType();
            RawReader = ((IAotFieldReader)Activator.CreateInstance(jitType)).Read;
        }

        private static void EmitFieldRead(ILGenerator il, FieldInfo field) {
            // We need to load the field on the object.
            if (field.IsStatic) {
                il.Emit(OpCodes.Ldsfld, field);
            }
            else {
                // Load parameter
                il.Emit(OpCodes.Ldarg_1);
                // This Castclass doesn't seem to be strictly necessary.
                il.Emit(OpCodes.Castclass, field.DeclaringType);

                // Load field in parameter
                il.Emit(OpCodes.Ldfld, field);
            }


            if (field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);
        }

        private static void EmitPropertyRead(ILGenerator il, PropertyInfo property) {
            MethodInfo getMethod = property.GetGetMethod(/*nonPublic:*/ true);

            if (getMethod.IsStatic) {
                // Dispatch call to static method; the object instance is not needed.
                il.Emit(OpCodes.Call, getMethod);
            }
            else {
                // We need to load the object so we can dispatch a call to the getter.
                il.Emit(OpCodes.Ldarg_1);
                // This Castclass doesn't seem to be strictly necessary.
                il.Emit(OpCodes.Castclass, property.DeclaringType);
                il.Emit(OpCodes.Callvirt, getMethod);
            }

            if (property.PropertyType.IsValueType)
                il.Emit(OpCodes.Box, property.PropertyType);

            il.Emit(OpCodes.Ret);
        }
    }
}