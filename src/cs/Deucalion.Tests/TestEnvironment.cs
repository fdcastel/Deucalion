namespace Deucalion.Tests;

public static class TestEnvironment
{
    /// <summary>
    /// Gate for tests that probe the public internet (DNS resolvers, ICMP). They are skipped by
    /// default -- including in CI, where they cost an unauthenticated api.github.com rate-limit
    /// slot shared across every GitHub-hosted runner, need outbound :53, and need raw ICMP that
    /// most containers block. Opt in with DEUCALION_TESTS_NETWORK=1 when touching a monitor.
    /// </summary>
    public static bool NetworkTestsEnabled =>
        Environment.GetEnvironmentVariable("DEUCALION_TESTS_NETWORK") == "1";
}
