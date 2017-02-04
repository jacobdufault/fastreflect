using System;
using System.Reflection;
using System.Reflection.Emit;

namespace FastReflect.Internal {
    public class frJitManager {
        private static frJitManager _instance;

        public static frJitManager Instance {
            get {
                if (_instance == null)
                    _instance = new frJitManager();
                return _instance;
            }
        }

        private AssemblyBuilder jitAssembly;
        private ModuleBuilder jitModule;
        private int nextTypeId;

        public frJitManager() {
            jitAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("FrJitAsm"), AssemblyBuilderAccess.Run);
            jitModule = jitAssembly.DefineDynamicModule("FrJitAsmModule");
        }

        public TypeBuilder CreateType(params Type[] interfaces) {
            return jitModule.DefineType("JitType_" + nextTypeId++, TypeAttributes.Public, /*parent:*/typeof(object), /*interfaces:*/interfaces);
        }
    }
}