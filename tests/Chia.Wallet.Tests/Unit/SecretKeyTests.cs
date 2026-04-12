using ChiaWalletSdk;
using Xunit;

namespace Chia.Wallet.Tests.Unit;

public class SecretKeyTests
{
    // Well-known all-abandon test mnemonic — never use for real funds.
    private const string TestMnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon art";

    [Fact]
    public void ToBytes_Returns32Bytes()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        Assert.Equal(32, sk.ToBytes().Length);
    }

    [Fact]
    public void FromBytes_ToBytes_RoundTrip()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var original = SecretKey.FromSeed(seed);
        var bytes = original.ToBytes();

        using var restored = SecretKey.FromBytes(bytes);
        Assert.Equal(
            Convert.ToHexString(original.ToBytes()),
            Convert.ToHexString(restored.ToBytes())
        );
    }

    [Fact]
    public void FromBytes_RestoredKey_ProducesSamePublicKey()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var original = SecretKey.FromSeed(seed);
        var bytes = original.ToBytes();

        using var restored = SecretKey.FromBytes(bytes);
        using var pk1 = original.PublicKey();
        using var pk2 = restored.PublicKey();

        Assert.Equal(Convert.ToHexString(pk1.ToBytes()), Convert.ToHexString(pk2.ToBytes()));
    }

    [Fact]
    public void DeriveUnhardened_ProducesDifferentKeyEachIndex()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);

        using var child0 = sk.DeriveUnhardened(0);
        using var child1 = sk.DeriveUnhardened(1);

        Assert.NotEqual(
            Convert.ToHexString(child0.ToBytes()),
            Convert.ToHexString(child1.ToBytes())
        );
    }

    [Fact]
    public void DeriveHardened_ProducesDifferentKeyEachIndex()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);

        using var child0 = sk.DeriveHardened(0);
        using var child1 = sk.DeriveHardened(1);

        Assert.NotEqual(
            Convert.ToHexString(child0.ToBytes()),
            Convert.ToHexString(child1.ToBytes())
        );
    }

    [Fact]
    public void DeriveHardened_DiffersFromDeriveUnhardened()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);

        using var hardened = sk.DeriveHardened(0);
        using var unhardened = sk.DeriveUnhardened(0);

        Assert.NotEqual(
            Convert.ToHexString(hardened.ToBytes()),
            Convert.ToHexString(unhardened.ToBytes())
        );
    }
}
