using ChiaWalletSdk;
using Xunit;

namespace Chia.Wallet.Tests.Unit;

/// <summary>
/// Tests public-key-only derivation paths (no secret key required).
/// Verifies the BIP32 unhardened property: sk.DeriveUnhardened(i).PublicKey()
/// must equal sk.PublicKey().DeriveUnhardened(i).
/// </summary>
public class PublicKeyDerivationTests
{
    // Well-known all-abandon test mnemonic — never use for real funds.
    private const string TestMnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon art";

    [Fact]
    public void PublicKey_ToBytes_Returns48Bytes()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var pk = sk.PublicKey();
        Assert.Equal(48, pk.ToBytes().Length);
    }

    [Fact]
    public void PublicKey_FromBytes_ToBytes_RoundTrip()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var original = sk.PublicKey();
        var bytes = original.ToBytes();

        using var restored = PublicKey.FromBytes(bytes);
        Assert.Equal(
            Convert.ToHexString(original.ToBytes()),
            Convert.ToHexString(restored.ToBytes())
        );
    }

    /// <summary>
    /// BIP32 unhardened property: deriving unhardened child from SK then taking PK
    /// must equal taking PK first then deriving unhardened child from it.
    /// </summary>
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(12381u)]
    public void DeriveUnhardened_OnPK_MatchesDeriveUnhardenedOnSK(uint index)
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);

        // Via SK: derive child SK, then take public key
        using var childSk = masterSk.DeriveUnhardened(index);
        using var pkFromSk = childSk.PublicKey();

        // Via PK: take master PK, then derive unhardened child PK
        using var masterPk = masterSk.PublicKey();
        using var pkFromPk = masterPk.DeriveUnhardened(index);

        Assert.Equal(
            Convert.ToHexString(pkFromSk.ToBytes()),
            Convert.ToHexString(pkFromPk.ToBytes())
        );
    }

    [Fact]
    public void DeriveUnhardened_DifferentIndices_ProduceDifferentKeys()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var masterPk = masterSk.PublicKey();

        using var child0 = masterPk.DeriveUnhardened(0);
        using var child1 = masterPk.DeriveUnhardened(1);

        Assert.NotEqual(
            Convert.ToHexString(child0.ToBytes()),
            Convert.ToHexString(child1.ToBytes())
        );
    }

    [Fact]
    public void DeriveSynthetic_OnPK_ProducesNonInfinityKey()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var masterPk = masterSk.PublicKey();
        using var childPk = masterPk.DeriveUnhardened(0);
        using var syntheticPk = childPk.DeriveSynthetic();

        Assert.False(syntheticPk.IsInfinity());
    }

    [Fact]
    public void DeriveSynthetic_OnPK_IsDeterministic()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var masterPk = masterSk.PublicKey();

        using var childPk1 = masterPk.DeriveUnhardened(0);
        using var childPk2 = masterPk.DeriveUnhardened(0);
        using var syntheticPk1 = childPk1.DeriveSynthetic();
        using var syntheticPk2 = childPk2.DeriveSynthetic();

        Assert.Equal(
            Convert.ToHexString(syntheticPk1.ToBytes()),
            Convert.ToHexString(syntheticPk2.ToBytes())
        );
    }

    /// <summary>
    /// Verifies the full BIP44 watch-only path used in Program.cs:
    /// masterPk -> DeriveUnhardened(12381) -> DeriveUnhardened(8444)
    ///          -> DeriveUnhardened(2) -> DeriveUnhardened(i) -> DeriveSynthetic
    /// produces the same puzzle hash as the SK-based path.
    /// </summary>
    [Fact]
    public void FullWalletPath_PKOnly_MatchesSKDerivedPath()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);

        // SK path
        using var skPath1 = masterSk.DeriveUnhardened(12381);
        using var skPath2 = skPath1.DeriveUnhardened(8444);
        using var walletSk = skPath2.DeriveUnhardened(2);
        using var childSk = walletSk.DeriveUnhardened(0);
        using var syntheticSk = childSk.DeriveSynthetic();
        using var syntheticSkPk = syntheticSk.PublicKey();

        // PK-only path
        using var masterPk = masterSk.PublicKey();
        using var pkPath1 = masterPk.DeriveUnhardened(12381);
        using var pkPath2 = pkPath1.DeriveUnhardened(8444);
        using var walletPk = pkPath2.DeriveUnhardened(2);
        using var childPk = walletPk.DeriveUnhardened(0);
        using var syntheticPkOnly = childPk.DeriveSynthetic();

        var puzzleHashFromSk = ChiaWalletSdkMethods.StandardPuzzleHash(syntheticSkPk);
        var puzzleHashFromPk = ChiaWalletSdkMethods.StandardPuzzleHash(syntheticPkOnly);

        Assert.Equal(Convert.ToHexString(puzzleHashFromSk), Convert.ToHexString(puzzleHashFromPk));
    }

    /// <summary>
    /// Cross-validated against chia-wallet-sdk index.spec.ts "public key roundtrip":
    ///   PublicKey.infinity().toBytes() → FromBytes → still infinity.
    /// </summary>
    [Fact]
    public void Infinity_ToBytes_FromBytes_RoundTrip()
    {
        using var infinity = PublicKey.Infinity();
        Assert.True(infinity.IsInfinity());

        var bytes = infinity.ToBytes();
        using var restored = PublicKey.FromBytes(bytes);
        Assert.True(restored.IsInfinity());
        Assert.Equal(
            Convert.ToHexString(infinity.ToBytes()),
            Convert.ToHexString(restored.ToBytes())
        );
    }

    [Fact]
    public void DerivedKey_IsValid()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk = SecretKey.FromSeed(seed);
        using var pk = sk.PublicKey();
        Assert.True(pk.IsValid());
    }

    [Fact]
    public void Fingerprint_IsDeterministic()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var sk1 = SecretKey.FromSeed(seed);
        using var sk2 = SecretKey.FromSeed(seed);
        using var pk1 = sk1.PublicKey();
        using var pk2 = sk2.PublicKey();
        Assert.Equal(pk1.Fingerprint(), pk2.Fingerprint());
    }

    [Fact]
    public void Fingerprint_DiffersAcrossKeys()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var masterPk = masterSk.PublicKey();
        using var childPk = masterPk.DeriveUnhardened(0);
        Assert.NotEqual(masterPk.Fingerprint(), childPk.Fingerprint());
    }

    [Fact]
    public void DeriveUnhardenedPath_MatchesStepByStepDerivation()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var masterPk = masterSk.PublicKey();

        // Path-based derivation
        using var pathPk = masterPk.DeriveUnhardenedPath(new List<uint> { 12381, 8444, 2, 0 });

        // Step-by-step derivation
        using var step1 = masterPk.DeriveUnhardened(12381);
        using var step2 = step1.DeriveUnhardened(8444);
        using var step3 = step2.DeriveUnhardened(2);
        using var stepPk = step3.DeriveUnhardened(0);

        Assert.Equal(Convert.ToHexString(pathPk.ToBytes()), Convert.ToHexString(stepPk.ToBytes()));
    }

    [Fact]
    public void Aggregate_TwoKeys_IsNotInfinity()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var pk1 = masterSk.PublicKey();
        using var pk2 = masterSk.DeriveUnhardened(1).PublicKey();
        using var agg = PublicKey.Aggregate(new List<PublicKey> { pk1, pk2 });
        Assert.False(agg.IsInfinity());
    }
}
