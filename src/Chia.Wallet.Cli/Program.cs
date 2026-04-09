using DotNetEnv;
using uniffi.chia_wallet_sdk;

Env.Load(".env", Env.NoClobber());

var peerHost = GetArg(args, "--peer") ?? Environment.GetEnvironmentVariable("CHIA_PEER_HOST");
var mnemonicPhrase = GetArg(args, "--mnemonic") ?? Environment.GetEnvironmentVariable("CHIA_MNEMONIC");
var secretKeyHex = GetArg(args, "--secret-key") ?? Environment.GetEnvironmentVariable("CHIA_SECRET_KEY");
var publicKeyHex = GetArg(args, "--public-key") ?? Environment.GetEnvironmentVariable("CHIA_PUBLIC_KEY");
var networkId = GetArg(args, "--network") ?? Environment.GetEnvironmentVariable("CHIA_NETWORK_ID") ?? "mainnet";
var certPath = GetArg(args, "--cert") ?? Environment.GetEnvironmentVariable("CHIA_CERT_PATH");
var keyPath = GetArg(args, "--key") ?? Environment.GetEnvironmentVariable("CHIA_KEY_PATH");
var certPem = Environment.GetEnvironmentVariable("CHIA_CERT_PEM");
var keyPem = Environment.GetEnvironmentVariable("CHIA_KEY_PEM");

if (string.IsNullOrWhiteSpace(peerHost))
{
    Console.Error.WriteLine("Error: --peer <wss://host:port> or CHIA_PEER_HOST required.");
    return 1;
}

var hasKey = !string.IsNullOrWhiteSpace(mnemonicPhrase)
    || !string.IsNullOrWhiteSpace(secretKeyHex)
    || !string.IsNullOrWhiteSpace(publicKeyHex);

if (!hasKey)
{
    Console.Error.WriteLine("Error: Provide one of:");
    Console.Error.WriteLine("  --mnemonic \"<24 words>\"  (or CHIA_MNEMONIC)");
    Console.Error.WriteLine("  --secret-key <hex>       (or CHIA_SECRET_KEY)");
    Console.Error.WriteLine("  --public-key <hex>       (or CHIA_PUBLIC_KEY)");
    return 1;
}

var hasCertFiles = !string.IsNullOrWhiteSpace(certPath) && !string.IsNullOrWhiteSpace(keyPath);
var hasCertPem = !string.IsNullOrWhiteSpace(certPem) && !string.IsNullOrWhiteSpace(keyPem);

if (!hasCertFiles && !hasCertPem)
{
    Console.Error.WriteLine("Error: TLS certificate required. Provide either:");
    Console.Error.WriteLine("  --cert <path> --key <path>  (or CHIA_CERT_PATH / CHIA_KEY_PATH)");
    Console.Error.WriteLine("  CHIA_CERT_PEM / CHIA_KEY_PEM environment variables");
    return 1;
}

Console.WriteLine("Welcome to the Chia Wallet DOTNET CLI!");

// 1. Derive public key and puzzle hash
PublicKey pk;
if (!string.IsNullOrWhiteSpace(publicKeyHex))
{
    pk = PublicKey.FromBytes(Convert.FromHexString(publicKeyHex));
}
else if (!string.IsNullOrWhiteSpace(secretKeyHex))
{
    using var sk = SecretKey.FromBytes(Convert.FromHexString(secretKeyHex));
    using var syntheticSk = sk.DeriveSynthetic();
    pk = syntheticSk.PublicKey();
}
else
{
    using var mnemonic = new Mnemonic(mnemonicPhrase!);
    var seed = mnemonic.ToSeed("");
    using var masterSk = SecretKey.FromSeed(seed);
    using var hardenedSk = masterSk.DeriveHardened(0);
    using var syntheticSk = hardenedSk.DeriveSynthetic();
    pk = syntheticSk.PublicKey();
}

using var _ = pk;
var puzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(pk);

Console.WriteLine($"Puzzle Hash : {Convert.ToHexString(puzzleHash).ToLower()}");

// 2. Connect to peer
Console.WriteLine($"Connecting to {peerHost} ({networkId})...");
using var cert = hasCertFiles
    ? Certificate.Load(certPath!, keyPath!)
    : new Certificate(certPem!, keyPem!);
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
