using ChiaWalletSdk;
using Xunit;

namespace Chia.Wallet.Tests.Unit;

public class CoinIdTests
{
    [Fact]
    public void CoinId_Returns32Bytes()
    {
        using var coin = new Coin(new byte[32], new byte[32], "1000000000000");
        Assert.Equal(32, coin.CoinId().Length);
    }

    [Fact]
    public void CoinId_IsDeterministic()
    {
        using var coin1 = new Coin(new byte[32], new byte[32], "1000000000000");
        using var coin2 = new Coin(new byte[32], new byte[32], "1000000000000");
        Assert.Equal(
            Convert.ToHexString(coin1.CoinId()),
            Convert.ToHexString(coin2.CoinId()));
    }

    [Fact]
    public void CoinId_ChangesWhenAmountChanges()
    {
        using var coin1 = new Coin(new byte[32], new byte[32], "1000000000000");
        using var coin2 = new Coin(new byte[32], new byte[32], "2000000000000");
        Assert.NotEqual(
            Convert.ToHexString(coin1.CoinId()),
            Convert.ToHexString(coin2.CoinId()));
    }

    [Fact]
    public void CoinId_ChangesWhenPuzzleHashChanges()
    {
        var puzzleHash1 = new byte[32];
        var puzzleHash2 = new byte[32];
        puzzleHash2[0] = 1;
        using var coin1 = new Coin(new byte[32], puzzleHash1, "1000000000000");
        using var coin2 = new Coin(new byte[32], puzzleHash2, "1000000000000");
        Assert.NotEqual(
            Convert.ToHexString(coin1.CoinId()),
            Convert.ToHexString(coin2.CoinId()));
    }

    [Fact]
    public void GetAmount_ReturnsConstructedValue()
    {
        using var coin = new Coin(new byte[32], new byte[32], "1000000000000");
        Assert.Equal("1000000000000", coin.GetAmount());
    }

    [Fact]
    public void GetParentCoinInfo_ReturnsConstructedValue()
    {
        var parentCoinInfo = new byte[32];
        parentCoinInfo[0] = 0xAB;
        using var coin = new Coin(parentCoinInfo, new byte[32], "0");
        Assert.Equal(Convert.ToHexString(parentCoinInfo), Convert.ToHexString(coin.GetParentCoinInfo()));
    }

    [Fact]
    public void GetPuzzleHash_ReturnsConstructedValue()
    {
        var puzzleHash = new byte[32];
        puzzleHash[31] = 0xCD;
        using var coin = new Coin(new byte[32], puzzleHash, "0");
        Assert.Equal(Convert.ToHexString(puzzleHash), Convert.ToHexString(coin.GetPuzzleHash()));
    }

    /// <summary>
    /// Known vector cross-validated against chia-wallet-sdk index.spec.ts:
    ///   new Coin(fromHex("4bf5122f…"), fromHex("dbc1b4c9…"), 100n).coinId()
    ///   == "fd3e669c27be9d634fe79f1f7d7d8aaacc3597b855cffea1d708f4642f1d542a"
    /// </summary>
    [Fact]
    public void CoinId_KnownVector_MatchesSdkOutput()
    {
        var parentCoinInfo = Convert.FromHexString("4bf5122f344554c53bde2ebb8cd2b7e3d1600ad631c385a5d7cce23c7785459a");
        var puzzleHash    = Convert.FromHexString("dbc1b4c900ffe48d575b5da5c638040125f65db0fe3e24494b76ea986457d986");
        using var coin = new Coin(parentCoinInfo, puzzleHash, "100");
        var coinId = coin.CoinId();
        Assert.Equal(
            "fd3e669c27be9d634fe79f1f7d7d8aaacc3597b855cffea1d708f4642f1d542a",
            Convert.ToHexString(coinId).ToLowerInvariant());
    }
}
