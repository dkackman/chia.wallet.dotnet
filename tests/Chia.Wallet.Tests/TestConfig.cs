using DotNetEnv;
using uniffi.chia_wallet_sdk;

namespace Chia.Wallet.Tests;

public static class TestConfig
{
    static TestConfig()
    {
        Env.Load(".env", Env.NoClobber());
    }

    public static string? PeerHost => Environment.GetEnvironmentVariable("CHIA_PEER_HOST");
    public static string NetworkId => Environment.GetEnvironmentVariable("CHIA_NETWORK_ID") ?? "mainnet";
    public static string? CertPath => Environment.GetEnvironmentVariable("CHIA_CERT_PATH");
    public static string? KeyPath => Environment.GetEnvironmentVariable("CHIA_KEY_PATH");
    public static string? CertPem => Environment.GetEnvironmentVariable("CHIA_CERT_PEM");
    public static string? KeyPem => Environment.GetEnvironmentVariable("CHIA_KEY_PEM");

    public static bool HasCertConfig =>
        (!string.IsNullOrWhiteSpace(CertPath) && !string.IsNullOrWhiteSpace(KeyPath)) ||
        (!string.IsNullOrWhiteSpace(CertPem) && !string.IsNullOrWhiteSpace(KeyPem));

    public static bool IsIntegrationConfigured => !string.IsNullOrWhiteSpace(PeerHost) && HasCertConfig;

    public static Certificate LoadCertificate()
    {
        if (!string.IsNullOrWhiteSpace(CertPath) && !string.IsNullOrWhiteSpace(KeyPath))
            return Certificate.Load(CertPath, KeyPath);

        return new Certificate(CertPem!, KeyPem!);
    }
}
