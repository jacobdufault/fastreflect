namespace FastReflect {
    public static class frConfig {
        public static bool HasJit =
#if ENABLE_IL2CPP
            false
#else
            true
#endif
            ;
    }
}