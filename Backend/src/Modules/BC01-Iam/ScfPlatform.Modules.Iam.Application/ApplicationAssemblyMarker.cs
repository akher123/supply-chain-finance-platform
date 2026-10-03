namespace ScfPlatform.Modules.Iam.Application;

/// <summary>Anchor type so the host composition root can resolve this module's Application assembly (e.g. for MediatR/FluentValidation assembly scanning) without a fragile by-name load.</summary>
public sealed class ApplicationAssemblyMarker;
