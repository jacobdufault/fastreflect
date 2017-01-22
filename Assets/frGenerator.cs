﻿using System.Linq;
using System.Text;

namespace FastReflect {
    public class frGenerator {
        /* Example output:

        // ***************************************************************************
        // ***************************************************************************
        // *** WARNING: This file was automatically generated by FastReflect.      ***
        // ***          Manual edits may get overwritten.                          ***
        // ***************************************************************************
        // ***************************************************************************

        using System;
        using FastReflect;

        public partial class foo {
            public static frTypeAotData Provider_FastReflect_MyDerivedType = new frTypeAotData {
                ProviderFor = typeof(FastReflect.MyDerivedType),
                FieldAotData = new frField.AotData[] {
                    new frField.AotData {
                        FieldName = "BaseField",
                        Read = (o) => ((FastReflect.MyDerivedType)o).BaseField,
                        Write = (ref object o, object v) => ((FastReflect.MyDerivedType)o).BaseField = (float)v
                    },
                    new frField.AotData {
                        FieldName = "ChildField",
                        Read = (o) => ((FastReflect.MyDerivedType)o).ChildField,
                        Write = (ref object o, object v) => ((FastReflect.MyDerivedType)o).ChildField = (float)v
                    },
                },
                MethodAotData = new frMethod.AotData[] {
                    new frMethod.AotData {
                        MethodName = "ChildMethod",
                        Parameters = Type.EmptyTypes,
                        Invoke = (o, args) => { ((FastReflect.MyDerivedType)o).ChildMethod(); return null; }
                    },
                    new frMethod.AotData {
                        MethodName = "ChildMethod",
                        Parameters = new Type[] { typeof(string) },
                        Invoke = (o, args) => { ((FastReflect.MyDerivedType)o).ChildMethod((string)args[0]); return null; }
                    },
                    new frMethod.AotData {
                        MethodName = "BaseChildMethod",
                        Parameters = Type.EmptyTypes,
                        Invoke = (o, args) => { ((FastReflect.MyDerivedType)o).BaseChildMethod(); return null; }
                    },
                    new frMethod.AotData {
                        MethodName = "FastReflect.IExplicitInterface.MyExplicitMethod",
                        Parameters = Type.EmptyTypes,
                        Invoke = (o, args) => { ((FastReflect.IExplicitInterface)o).MyExplicitMethod(); return null; }
                    },
                },
            };
        }
        */

        private struct Helper {
            public StringBuilder result;

            public void W(int indent, string format, params object[] args) {
                for (int i = 0; i < indent; ++i)
                    result.Append("    ");
                result.AppendLine(string.Format(format, args));
            }
        }

        public string GenerateForType(string providerTypeName, frType type) {
            var w = new Helper() { result = new StringBuilder() };

            string typeName = type.RawType.CSharpName(/*includeNamespace:*/ true);

            w.W(0, "// ***************************************************************************");
            w.W(0, "// ***************************************************************************");
            w.W(0, "// *** WARNING: This file was automatically generated by FastReflect.      ***");
            w.W(0, "// ***          Manual edits may get overwritten.                          ***");
            w.W(0, "// ***************************************************************************");
            w.W(0, "// ***************************************************************************");
            w.W(0, "");
            w.W(0, "using System;");
            w.W(0, "using FastReflect;");
            w.W(0, "");
            w.W(0, "public partial class {0} {{", providerTypeName);
            w.W(1, "public static frTypeAotData Provider_{0} = new frTypeAotData {{", type.RawType.CSharpName(/*includeNamespace:*/ true, /*ensureSafeDeclarationName:*/ true));
            w.W(2, "ProviderFor = typeof({0}),", typeName);
            w.W(2, "FieldAotData = new frField.AotData[] {{");

            foreach (frField field in type.Fields) {
                /*
                new frField.AotData {
                    FieldName = "Field",
                    Read = (o) => { ((AccelerationType)o).Field; },
                    Write = (ref object o, object v) => { var u = (AccelerationType)o; u.Field = (float)v; o = u; }
                }
                */
                w.W(3, "new frField.AotData {{");
                w.W(4, "FieldName = \"{0}\",", field.Name);
                w.W(4, "Read = (o) => (({0})o).{1},", typeName, field.Name);
                if (type.RawType.Resolve().IsValueType)
                    w.W(4, "Write = (ref object o, object v) => {{ var u = ({0})o; u.{1} = ({2})v; o = u; }}", typeName, field.Name, field.MemberType.CSharpName(/*includeNamespace:*/ true));
                else
                    w.W(4, "Write = (ref object o, object v) => (({0})o).{1} = ({2})v", typeName, field.Name, field.MemberType.CSharpName(/*includeNamespace:*/ true));
                w.W(3, "}},");
            }
            w.W(2, "}},");
            w.W(2, "MethodAotData = new frMethod.AotData[] {{");
            foreach (frMethod method in type.Methods) {
                /*
                new frMethod.AotData {
                    MethodName = "Method",
                    Parameters = new Type[] {},
                    Invoke = (o, args) => { return ((AccelerationType)o).Method(); }
                },
                */
                w.W(3, "new frMethod.AotData {{");
                w.W(4, "MethodName = \"{0}\",", method.RawMethod.Name);

                var parameters = method.RawMethod.GetParameters();
                if (parameters.Length > 0) {
                    // typeof(int), typeof(double)
                    var paramTypes = string.Join(", ", parameters.Select(p => "typeof(" + p.ParameterType.CSharpName(/*includeNamespace:*/ true) + ")").ToArray());
                    w.W(4, "Parameters = new Type[] {{ {0} }},", paramTypes);
                } else {
                    w.W(4, "Parameters = Type.EmptyTypes,");
                }

                // (int)args[0], (double)args[1]
                string unpackArgs = string.Join(", ", Enumerable.Range(0, method.RawMethod.GetParameters().Length).Select(p => "(" + parameters[p].ParameterType.CSharpName(/*includeNamespace*/true) + ")args[" + p + "]").ToArray());

                string methodHolderTypeName = typeName;
                string methodName = method.RawMethod.Name;
                if (methodName.Contains(".")) {
                    // Explicit method. We need to cast to the correct type.
                    methodHolderTypeName = methodName.Substring(0, methodName.LastIndexOf("."));
                    methodName = method.RawMethod.Name.Substring(methodName.LastIndexOf(".") + 1);
                }

                if (method.RawMethod.ReturnType == typeof(void))
                    w.W(4, "Invoke = (o, args) => {{ (({0})o).{1}({2}); return null; }}", methodHolderTypeName, methodName, unpackArgs);
                else
                    w.W(4, "Invoke = (o, args) => (({0})o).{1}({2})", methodHolderTypeName, methodName, unpackArgs);

                w.W(3, "}},");
            }
            w.W(2, "}},");
            w.W(1, "}};");
            w.W(0, "}}");

            return w.result.ToString();
        }
    }
}