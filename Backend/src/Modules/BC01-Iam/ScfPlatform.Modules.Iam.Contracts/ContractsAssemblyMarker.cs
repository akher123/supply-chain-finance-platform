namespace ScfPlatform.Modules.Iam.Contracts;

/// <summary>Anchor type so the host composition root (and other modules, once they consume this module's public surface) can resolve this module's Contracts assembly without a fragile by-name load.</summary>
public sealed class ContractsAssemblyMarker;
