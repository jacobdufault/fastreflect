using System;

namespace FastReflect {
    public struct frTypeAotData {
        public Type ProviderFor;
        public frField.AotData[] FieldAotData;
        public frMethod.AotData[] MethodAotData;
    }
}