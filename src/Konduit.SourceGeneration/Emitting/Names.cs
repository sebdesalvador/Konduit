namespace Konduit.SourceGeneration.Emitting;

/// <summary>Fully qualified names of the runtime types generated code refers to.</summary>
internal static class Names
{
    public const string Namespace = "Konduit.Generated";
    public const string Context = "global::Konduit.KonduitContext";
    public const string Delegate = "global::Konduit.KonduitDelegate";
    public const string Method = "global::Konduit.KonduitMethod";
    public const string Parameter = "global::Konduit.KonduitParameter";
    public const string Proxy = "global::Konduit.IKonduitProxy";
    public const string Registry = "global::Konduit.KonduitProxyRegistry";
    public const string Sync = "global::Konduit.KonduitSync";
    public const string ServiceProvider = "global::System.IServiceProvider";
    public const string ModuleInitializer = "global::System.Runtime.CompilerServices.ModuleInitializerAttribute";
}
