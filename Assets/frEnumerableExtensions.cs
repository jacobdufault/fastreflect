using System.Collections.Generic;

namespace FastReflect.Internal {
    public static class frEnumerableExtensions {
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> items) {
            if (items == null)
                return true;
            foreach (T t in items)
                return false;
            return true;
        }
    }
}