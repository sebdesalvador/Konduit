namespace Konduit.SourceGeneration.Model;

/// <summary>How a proxied method hands its value back, which decides the shape of the emitted body.</summary>
internal enum ReturnKind
{
    /// <summary>Returns <see langword="void"/>.</summary>
    Void,

    /// <summary>Returns a value synchronously.</summary>
    Value,

    /// <summary>Returns a bare <c>Task</c> or <c>ValueTask</c>.</summary>
    AwaitableVoid,

    /// <summary>Returns <c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c>.</summary>
    AwaitableValue,
}
