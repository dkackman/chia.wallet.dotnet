using uniffi.chia_wallet_sdk;
using Xunit;

namespace Chia.Wallet.Tests.Integration;

public class PeerConnectionTests
{
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Connect_ValidPeer_ReturnsNonNullPeer()
    {
        Skip.If(!TestConfig.IsIntegrationConfigured,
            "Set CHIA_PEER_HOST in tests/Chia.Wallet.Tests/.env to run integration tests.");

        using var cert = Certificate.Generate();
        using var connector = new Connector(cert);
        using var options = new PeerOptions();

        using var peer = await Peer.Connect(
            TestConfig.NetworkId,
            TestConfig.PeerHost!,
            connector,
            options);

        Assert.NotNull(peer);
    }
}
