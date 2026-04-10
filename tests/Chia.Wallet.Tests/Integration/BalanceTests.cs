using ChiaWalletSdk;
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
            "Set CHIA_PEER_HOST in tests/Chia.Wallet.Tests/.env to run integration tests.");

        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var sk1 = masterSk.DeriveUnhardened(12381);
        using var sk2 = sk1.DeriveUnhardened(8444);
        using var walletSk = sk2.DeriveUnhardened(2);
        using var childPk = walletSk.PublicKey().DeriveUnhardened(0);
        using var syntheticPk = childPk.DeriveSynthetic();
        var puzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(syntheticPk);

        using var cert = Certificate.Generate();
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
