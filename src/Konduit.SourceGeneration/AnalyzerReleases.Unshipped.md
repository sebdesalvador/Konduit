; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
KDT001 | Konduit | Error | WithMiddleware<T>() must be chained onto a service registration
KDT002 | Konduit | Error | Konduit can only add middleware to services registered as an interface
KDT003 | Konduit | Warning | Method cannot be intercepted and will bypass the Konduit pipeline
KDT004 | Konduit | Error | Konduit could not determine the service type of this registration
KDT006 | Konduit | Warning | Init-only interface properties cannot be forwarded by a Konduit proxy
