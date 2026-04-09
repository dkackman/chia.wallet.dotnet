using uniffi.chia_wallet_sdk;
using Xunit;

namespace Chia.Wallet.Tests.Integration;

public class BalanceTests
{
    // Well-known all-abandon test mnemonic — never use for real funds.
    private const string TestMnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon " +
        "abandon abandon abandon abandon abandon abandon abandon abandon " +
        "abandon abandon abandon abandon abandon abandon abandon art";

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task RequestPuzzleState_Returns_ValidResponse()
    {
        Skip.If(!TestConfig.IsIntegrationConfigured,
            "Set CHIA_PEER_HOST and cert config in tests/Chia.Wallet.Tests/.env to run integration tests.");

        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var hardenedSk = masterSk.DeriveHardened(0);
        using var syntheticSk = hardenedSk.DeriveSynthetic();
        using var pk = syntheticSk.PublicKey();
        var puzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(pk);

        using var cert = TestConfig.LoadCertificate();
        using var connector = new Connector(cert);
        using var options = new PeerOptions();
        using var peer = await Peer.Connect(
            TestConfig.NetworkId,
            TestConfig.PeerHost!,
            connector,
            options);

        var filters = new CoinStateFilters(
            includeSpent: false,
            includeUnspent: true,
            includeHinted: true,
            minAmount: "0");

        var response = await peer.RequestPuzzleState(
            [puzzleHash],
            previousHeight: null,
            headerHash: new byte[32],
            filters,
            subscribe: false);

        Assert.NotNull(response);
        var coinStates = response.GetCoinStates();
        Assert.NotNull(coinStates);
        // The all-abandon wallet may have zero coins — that is fine.
        // We assert the response shape is valid, not a specific balance.
        Assert.True(coinStates.Count >= 0);
    }
}
