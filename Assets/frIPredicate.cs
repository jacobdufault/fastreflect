namespace FastReflect {
    public interface frIPredicate {
        bool IsMatch(frMember member);
    }

    public abstract class frFieldPredicate : frIPredicate {
        bool frIPredicate.IsMatch(frMember member) {
            var field = member as frField;
            if (field != null)
                return IsMatch(field);
            return false;
        }

        protected abstract bool IsMatch(frField field);
    }

    public abstract class frMethodPredicate : frIPredicate {
        bool frIPredicate.IsMatch(frMember member) {
            var method = member as frMethod;
            if (method != null)
                return IsMatch(method);
            return false;
        }

        protected abstract bool IsMatch(frMethod method);
    }
}