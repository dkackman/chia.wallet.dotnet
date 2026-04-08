using uniffi.chia_wallet_sdk;
using Xunit;

namespace Chia.Wallet.Tests.Unit;

public class MnemonicTests
{
    [Fact]
    public void Generate24Word_ReturnsExactly24Words()
    {
        using var mnemonic = Mnemonic.Generate(use24: true);
        var words = mnemonic.ToString().Split(' ');
        Assert.Equal(24, words.Length);
    }

    [Fact]
    public void Generate12Word_ReturnsExactly12Words()
    {
        using var mnemonic = Mnemonic.Generate(use24: false);
        var words = mnemonic.ToString().Split(' ');
        Assert.Equal(12, words.Length);
    }

    [Fact]
    public void FromEntropy_RoundTrips()
    {
        using var original = Mnemonic.Generate(use24: true);
        var entropy = original.ToEntropy();
        using var restored = Mnemonic.FromEntropy(entropy);
        Assert.Equal(original.ToString(), restored.ToString());
    }

    [Fact]
    public void ParsePhrase_ValidPhrase_Succeeds()
    {
        using var generated = Mnemonic.Generate(use24: true);
        var phrase = generated.ToString();
        using var parsed = new Mnemonic(phrase);
        Assert.Equal(phrase, parsed.ToString());
    }
}
