using Xunit;

namespace Deucalion.Tests;

/// <summary>
/// Test classes that boot a host through <c>WebApplicationFactory</c> configure it via
/// process-global environment variables (<c>Deucalion__ConfigurationFile</c>,
/// <c>Deucalion__StoragePath</c>, ...) because the configuration is read during
/// <c>WebApplication.CreateBuilder</c>, before the factory's callbacks land. xunit runs
/// test classes in parallel, so every such class joins this collection to serialize them.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "ProcessEnvironment";
}
