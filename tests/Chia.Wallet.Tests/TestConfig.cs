using DotNetEnv;

namespace Chia.Wallet.Tests;

public static class TestConfig
{
    static TestConfig()
    {
        Env.Load(".env", Env.NoClobber());
    }

    public static string? PeerHost => Environment.GetEnvironmentVariable("CHIA_PEER_HOST");
    public static string NetworkId => Environment.GetEnvironmentVariable("CHIA_NETWORK_ID") ?? "mainnet";

    public static bool IsIntegrationConfigured => !string.IsNullOrWhiteSpace(PeerHost);
}
