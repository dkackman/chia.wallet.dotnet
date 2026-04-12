using ChiaWalletSdk;
using Xunit;

namespace Chia.Wallet.Tests.Unit;

/// <summary>
/// Tests BLS signing, verification, and aggregation.
/// Cross-validated against chia-wallet-sdk ReadmeTests and chia-dotnet-bls ChiaVectorsTests.
/// </summary>
public class SignatureTests
{
    // Well-known all-abandon test mnemonic — never use for real funds.
    private const string TestMnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon " +
        "abandon abandon abandon abandon abandon abandon abandon abandon " +
        "abandon abandon abandon abandon abandon abandon abandon art";

    private static readonly byte[] Message1 = [1, 2, 3, 4, 5];
    private static readonly byte[] Message2 = [10, 11, 12, 13, 14];

    [Fact]
    public void Sign_Produces96ByteSignature()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var sig = sk.Sign(Message1);
        Assert.Equal(96, sig.ToBytes().Length);
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var pk = sk.PublicKey();
        using var sig = sk.Sign(Message1);
        Assert.True(pk.Verify(Message1, sig));
    }

    [Fact]
    public void Verify_WrongMessage_ReturnsFalse()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var pk = sk.PublicKey();
        using var sig = sk.Sign(Message1);
        Assert.False(pk.Verify(Message2, sig));
    }

    [Fact]
    public void Verify_WrongPublicKey_ReturnsFalse()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var wrongSk = sk.DeriveUnhardened(1);
        using var wrongPk = wrongSk.PublicKey();
        using var sig = sk.Sign(Message1);
        Assert.False(wrongPk.Verify(Message1, sig));
    }

    [Fact]
    public void Sign_IsDeterministic()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var sig1 = sk.Sign(Message1);
        using var sig2 = sk.Sign(Message1);
        Assert.Equal(
            Convert.ToHexString(sig1.ToBytes()),
            Convert.ToHexString(sig2.ToBytes()));
    }

    [Fact]
    public void Signature_FromBytes_ToBytes_RoundTrip()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var original = sk.Sign(Message1);
        var bytes = original.ToBytes();

        using var restored = Signature.FromBytes(bytes);
        Assert.Equal(
            Convert.ToHexString(original.ToBytes()),
            Convert.ToHexString(restored.ToBytes()));
    }

    [Fact]
    public void Signature_Infinity_IsInfinity()
    {
        using var infinity = Signature.Infinity();
        Assert.True(infinity.IsInfinity());
    }

    [Fact]
    public void Signature_Infinity_ToBytes_FromBytes_RoundTrip()
    {
        using var infinity = Signature.Infinity();
        var bytes = infinity.ToBytes();
        Assert.Equal(96, bytes.Length);

        using var restored = Signature.FromBytes(bytes);
        Assert.True(restored.IsInfinity());
    }

    [Fact]
    public void Signature_Aggregate_ProducesNonInfinityResult()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk1 = SecretKey.FromSeed(seed);
        using var sk2 = sk1.DeriveUnhardened(1);
        using var sig1 = sk1.Sign(Message1);
        using var sig2 = sk2.Sign(Message2);

        using var agg = Signature.Aggregate(new List<Signature> { sig1, sig2 });
        Assert.False(agg.IsInfinity());
        Assert.Equal(96, agg.ToBytes().Length);
    }

    [Fact]
    public void Signature_DifferentMessages_ProduceDifferentSignatures()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var sig1 = sk.Sign(Message1);
        using var sig2 = sk.Sign(Message2);
        Assert.NotEqual(
            Convert.ToHexString(sig1.ToBytes()),
            Convert.ToHexString(sig2.ToBytes()));
    }

    /// <summary>
    /// Cross-validates the sign+verify chain against the full wallet derivation
    /// path used in Program.cs, matching the pattern in chia-dotnet-bls ReadmeTests.
    /// </summary>
    [Fact]
    public void Sign_WalletDerivedKey_VerifiesCorrectly()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var sk1 = masterSk.DeriveUnhardened(12381);
        using var sk2 = sk1.DeriveUnhardened(8444);
        using var walletSk = sk2.DeriveUnhardened(2);
        using var childSk = walletSk.DeriveUnhardened(0);
        using var syntheticSk = childSk.DeriveSynthetic();
        using var syntheticPk = syntheticSk.PublicKey();

        using var sig = syntheticSk.Sign(Message1);
        Assert.True(syntheticPk.Verify(Message1, sig));
    }
}
