using ChiaWalletSdk;
using Xunit;

namespace Chia.Wallet.Tests.Unit;

public class AddressTests
{
    private static readonly byte[] KnownPuzzleHash;

    // Well-known all-abandon test mnemonic — never use for real funds.
    private const string TestMnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon abandon "
        + "abandon abandon abandon abandon abandon abandon abandon art";

    static AddressTests()
    {
        using var mnemonic = new Mnemonic(TestMnemonic);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var sk1 = masterSk.DeriveUnhardened(12381);
        using var sk2 = sk1.DeriveUnhardened(8444);
        using var walletSk = sk2.DeriveUnhardened(2);
        using var walletPk = walletSk.PublicKey();
        using var childPk = walletPk.DeriveUnhardened(0);
        using var syntheticPk = childPk.DeriveSynthetic();
        KnownPuzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(syntheticPk);
    }

    [Fact]
    public void Encode_MainnetPrefix_StartsWithXch()
    {
        using var address = new Address(KnownPuzzleHash, "xch");
        Assert.StartsWith("xch", address.Encode());
    }

    [Fact]
    public void Encode_IsDeterministic()
    {
        using var address1 = new Address(KnownPuzzleHash, "xch");
        using var address2 = new Address(KnownPuzzleHash, "xch");
        Assert.Equal(address1.Encode(), address2.Encode());
    }

    [Fact]
    public void Encode_DifferentPuzzleHashes_ProduceDifferentAddresses()
    {
        var puzzleHash2 = (byte[])KnownPuzzleHash.Clone();
        puzzleHash2[0] ^= 0xFF;

        using var address1 = new Address(KnownPuzzleHash, "xch");
        using var address2 = new Address(puzzleHash2, "xch");
        Assert.NotEqual(address1.Encode(), address2.Encode());
    }

    [Fact]
    public void Encode_Testnet11Prefix_StartsWithTxch()
    {
        using var address = new Address(KnownPuzzleHash, "txch");
        Assert.StartsWith("txch", address.Encode());
    }

    [Fact]
    public void Encode_MainnetAndTestnet_DifferForSamePuzzleHash()
    {
        using var mainnet = new Address(KnownPuzzleHash, "xch");
        using var testnet = new Address(KnownPuzzleHash, "txch");
        Assert.NotEqual(mainnet.Encode(), testnet.Encode());
    }

    [Fact]
    public void Decode_EncodedAddress_RoundTrips()
    {
        using var original = new Address(KnownPuzzleHash, "xch");
        var encoded = original.Encode();

        using var decoded = Address.Decode(encoded);
        Assert.Equal("xch", decoded.GetPrefix());
        Assert.Equal(
            Convert.ToHexString(KnownPuzzleHash),
            Convert.ToHexString(decoded.GetPuzzleHash())
        );
    }

    [Fact]
    public void Decode_TestnetAddress_RoundTrips()
    {
        using var original = new Address(KnownPuzzleHash, "txch");
        var encoded = original.Encode();

        using var decoded = Address.Decode(encoded);
        Assert.Equal("txch", decoded.GetPrefix());
        Assert.Equal(
            Convert.ToHexString(KnownPuzzleHash),
            Convert.ToHexString(decoded.GetPuzzleHash())
        );
    }

    [Fact]
    public void GetPuzzleHash_AfterDecode_Matches_GetPuzzleHash_AfterEncode()
    {
        using var address = new Address(KnownPuzzleHash, "xch");
        var encoded = address.Encode();
        using var decoded = Address.Decode(encoded);

        Assert.Equal(
            Convert.ToHexString(address.GetPuzzleHash()),
            Convert.ToHexString(decoded.GetPuzzleHash())
        );
    }
}
