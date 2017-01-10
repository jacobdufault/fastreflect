using System;
using System.Diagnostics;
using System.Reflection;

namespace FastReflect {
    [DebuggerDisplay("{RawMethod}")]
    public class frMethod : frMember {
        public struct AotData {
            public string MethodName;
            public Type[] Parameters;
            public Invoker Invoke;
        }

        public MethodInfo RawMethod;

        // run method
        public delegate object Invoker(object instance, object[] parameters);
        public Invoker Invoke;

        public frMethod(MethodInfo method, AotData aot) {
            RawMethod = method;
            Invoke = aot.Invoke ?? method.Invoke;
        }
    }
}