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
}
