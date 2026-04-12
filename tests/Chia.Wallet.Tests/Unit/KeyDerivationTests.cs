using ChiaWalletSdk;
using Xunit;

namespace Chia.Wallet.Tests.Unit;

public class KeyDerivationTests
{
    // Well-known all-abandon test mnemonic — never use for real funds.
    private const string TestMnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon art";

    [Fact]
    public void FromSeed_ProducesNonNullKey()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        Assert.NotNull(sk);
    }

    [Fact]
    public void PublicKey_IsNotInfinity()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var pk = sk.PublicKey();
        Assert.False(pk.IsInfinity());
    }

    [Fact]
    public void DeriveHardenedThenSynthetic_DiffersFromMasterPublicKey()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var hardenedSk = masterSk.DeriveHardened(0);
        using var childSk = hardenedSk.DeriveSynthetic();
        using var childPk = childSk.PublicKey();
        using var masterPk = masterSk.PublicKey();
        Assert.NotEqual(
            Convert.ToHexString(childPk.ToBytes()),
            Convert.ToHexString(masterPk.ToBytes())
        );
    }

    [Fact]
    public void StandardPuzzleHash_Returns32Bytes()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var hardenedSk = sk.DeriveHardened(0);
        using var syntheticSk = hardenedSk.DeriveSynthetic();
        using var pk = syntheticSk.PublicKey();
        var puzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(pk);
        Assert.Equal(32, puzzleHash.Length);
    }

    [Fact]
    public void StandardPuzzleHash_IsDeterministic()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk1 = SecretKey.FromSeed(seed);
        using var sk2 = SecretKey.FromSeed(seed);
        using var hardenedSk1 = sk1.DeriveHardened(0);
        using var hardenedSk2 = sk2.DeriveHardened(0);
        using var syntheticSk1 = hardenedSk1.DeriveSynthetic();
        using var syntheticSk2 = hardenedSk2.DeriveSynthetic();
        using var pk1 = syntheticSk1.PublicKey();
        using var pk2 = syntheticSk2.PublicKey();
        Assert.Equal(
            Convert.ToHexString(ChiaWalletSdkMethods.StandardPuzzleHash(pk1)),
            Convert.ToHexString(ChiaWalletSdkMethods.StandardPuzzleHash(pk2))
        );
    }
}
