#if NETSTANDARD2_0

// Types the compiler and the trimmer expect, which netstandard2.0 does not define. They are
// internal, so they never reach Konduit's public surface, and every other target framework uses the
// real ones instead. This file is linked into the companion packages rather than duplicated.

namespace System.Runtime.CompilerServices
{
    /// <summary>Enables <c>init</c> accessors and records.</summary>
    internal static class IsExternalInit;

    /// <summary>Lets a helper capture the caller's expression text for an argument.</summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>The member kinds a trimmer must preserve on an annotated type.</summary>
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 1,
        PublicConstructors = 3,
        NonPublicConstructors = 4,
        PublicMethods = 8,
        NonPublicMethods = 16,
        PublicFields = 32,
        NonPublicFields = 64,
        PublicNestedTypes = 128,
        NonPublicNestedTypes = 256,
        PublicProperties = 512,
        NonPublicProperties = 1024,
        PublicEvents = 2048,
        NonPublicEvents = 4096,
        Interfaces = 8192,
        All = -1,
    }

    /// <summary>Tells the trimmer which members of an annotated type are reached dynamically.</summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter
        | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method,
        Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) : Attribute
    {
        public DynamicallyAccessedMemberTypes MemberTypes { get; } = memberTypes;
    }

    /// <summary>Marks code the trimmer cannot analyse.</summary>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
        Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute(string message) : Attribute
    {
        public string Message { get; } = message;

        public string? Url { get; set; }
    }

    /// <summary>Marks code that needs runtime code generation, so it cannot be AOT compiled.</summary>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
        Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute(string message) : Attribute
    {
        public string Message { get; } = message;

        public string? Url { get; set; }
    }

    /// <summary>Suppresses a trimming or AOT warning unconditionally.</summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute(string category, string checkId) : Attribute
    {
        public string Category { get; } = category;

        public string CheckId { get; } = checkId;

        public string? Justification { get; set; }
    }
}

#endif
