using DotNetEnv;
using uniffi.chia_wallet_sdk;

Env.Load(".env", Env.NoClobber());

var peerHost = GetArg(args, "--peer") ?? Environment.GetEnvironmentVariable("CHIA_PEER_HOST");
var mnemonicPhrase = GetArg(args, "--mnemonic") ?? Environment.GetEnvironmentVariable("CHIA_MNEMONIC");
var networkId = GetArg(args, "--network") ?? Environment.GetEnvironmentVariable("CHIA_NETWORK_ID") ?? "mainnet";

if (string.IsNullOrWhiteSpace(peerHost))
{
    Console.Error.WriteLine("Error: --peer <wss://host:port> or CHIA_PEER_HOST required.");
    return 1;
}

if (string.IsNullOrWhiteSpace(mnemonicPhrase))
{
    Console.Error.WriteLine("Error: --mnemonic \"<24 words>\" or CHIA_MNEMONIC required.");
    return 1;
}

// 1. Derive keys and puzzle hash
using var mnemonic = new Mnemonic(mnemonicPhrase);
var seed = mnemonic.ToSeed("");
using var masterSk = SecretKey.FromSeed(seed);
using var hardenedSk = masterSk.DeriveHardened(0);
using var syntheticSk = hardenedSk.DeriveSynthetic();
using var pk = syntheticSk.PublicKey();
var puzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(pk);

Console.WriteLine($"Puzzle Hash : {Convert.ToHexString(puzzleHash).ToLower()}");

// 2. Connect to peer
Console.WriteLine($"Connecting to {peerHost} ({networkId})...");
using var cert = Certificate.Generate();
using var connector = new Connector(cert);
using var options = new PeerOptions();
using var peer = await Peer.Connect(networkId, peerHost, connector, options);

// 3. Fetch unspent coins for this puzzle hash
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

// 4. Sum and print
var coinStates = response.GetCoinStates();
long totalMojos = 0;
foreach (var coinState in coinStates)
{
    using var coin = coinState.GetCoin();
    totalMojos += long.Parse(coin.GetAmount());
}

Console.WriteLine($"Coin Count  : {coinStates.Count}");
Console.WriteLine($"Balance     : {totalMojos} mojos");
Console.WriteLine($"Balance     : {totalMojos / 1_000_000_000_000.0:F12} XCH");

return 0;

static string? GetArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
