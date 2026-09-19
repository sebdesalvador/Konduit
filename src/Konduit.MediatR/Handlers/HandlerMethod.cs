using System.Diagnostics.CodeAnalysis;

namespace Konduit.MediatR.Handlers;

/// <summary>
/// Builds the <see cref="KonduitMethod"/> describing a handler's <c>Handle</c> method.
/// </summary>
/// <remarks>
/// Every MediatR handler interface declares exactly one method, taking the message and a
/// cancellation token, so the descriptor can be built without any code generation.
/// </remarks>
internal static class HandlerMethod
{
    public static KonduitMethod For(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type handlerInterface,
        string messageParameterName,
        Type messageType,
        Type returnType) =>
        new(
            handlerInterface,
            "Handle",
            returnType,
            [
                new KonduitParameter(messageParameterName, messageType, 0),
                new KonduitParameter("cancellationToken", typeof(CancellationToken), 1),
            ]);
}
