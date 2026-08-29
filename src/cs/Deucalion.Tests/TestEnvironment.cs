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

    /// <summary>
    /// Narrower gate for the one test that expects an ICMP echo to *succeed*. GitHub-hosted
    /// runners (Linux and Windows alike) cannot send ICMP echo to the internet, so even the weekly
    /// network run cannot pass it there; verified on a workflow_dispatch run, it is the only
    /// network test that fails on those runners.
    /// </summary>
    public static bool IcmpEchoAvailable =>
        NetworkTestsEnabled && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true";
}
