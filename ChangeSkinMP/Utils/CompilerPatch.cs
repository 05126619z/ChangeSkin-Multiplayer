global using Exception = System.Exception;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }

    internal sealed class RequiredMemberAttribute : Attribute { }

    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) { }
    }
}

// Для System.Range и System.Index
namespace System
{
    internal readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false) => _value = fromEnd ? ~value : value;

        public static Index FromEnd(int value) => new(value, fromEnd: true);

        public int GetOffset(int length) => _value < 0 ? length + _value + 1 : _value;

        public static implicit operator Index(int value) => new(value);
    }

    internal readonly struct Range
    {
        public Index Start { get; }
        public Index End { get; }

        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public static Range All => new(Index.FromEnd(0), Index.FromEnd(0));
    }
}
