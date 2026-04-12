using ChiaWalletSdk;
using DotNetEnv;

Env.Load(".env", Env.NoClobber());

var peerHost = GetArg(args, "--peer") ?? Environment.GetEnvironmentVariable("CHIA_PEER_HOST");
var mnemonicPhrase =
    GetArg(args, "--mnemonic") ?? Environment.GetEnvironmentVariable("CHIA_MNEMONIC");
var secretKeyHex =
    GetArg(args, "--secret-key") ?? Environment.GetEnvironmentVariable("CHIA_SECRET_KEY");
var publicKeyHex =
    GetArg(args, "--public-key") ?? Environment.GetEnvironmentVariable("CHIA_PUBLIC_KEY");
var networkId =
    GetArg(args, "--network") ?? Environment.GetEnvironmentVariable("CHIA_NETWORK_ID") ?? "mainnet";
var genesisChallenge =
    GetArg(args, "--genesis-challenge")
    ?? Environment.GetEnvironmentVariable("CHIA_GENESIS_CHALLENGE")
    ?? "ccd5bb71183532bff220ba46c268991a3ff07eb358e8255a65c30a2dce0e5fbb";

if (string.IsNullOrWhiteSpace(peerHost))
{
    Console.Error.WriteLine("Error: --peer <host-ip:port> or CHIA_PEER_HOST required.");
    return 1;
}

var hasKey =
    !string.IsNullOrWhiteSpace(mnemonicPhrase)
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

// 1. Derive wallet-level intermediate keys and generate puzzle hashes for first N addresses
//    Unhardened: master -> unhardened(12381/8444/2) -> unhardened(i) -> synthetic
//    Hardened:   master -> hardened(12381/8444/2)   -> hardened(i)   -> synthetic (requires SK)
const int addressCount = 7500;

PublicKey walletPk;
SecretKey? walletSk = null; // unhardened intermediate SK
SecretKey? walletSkHardened = null; // hardened intermediate SK

if (!string.IsNullOrWhiteSpace(publicKeyHex))
{
    // Master public key — unhardened path only (hardened derivation requires SK)
    using var masterPk = PublicKey.FromBytes(Convert.FromHexString(publicKeyHex));
    using var pk1 = masterPk.DeriveUnhardened(12381);
    using var pk2 = pk1.DeriveUnhardened(8444);
    walletPk = pk2.DeriveUnhardened(2);
}
else if (!string.IsNullOrWhiteSpace(secretKeyHex))
{
    using var sk = SecretKey.FromBytes(Convert.FromHexString(secretKeyHex));

    // Unhardened intermediate
    using var sk1 = sk.DeriveUnhardened(12381);
    using var sk2 = sk1.DeriveUnhardened(8444);
    walletSk = sk2.DeriveUnhardened(2);
    walletPk = walletSk.PublicKey();

    // Hardened intermediate
    using var hsk1 = sk.DeriveHardened(12381);
    using var hsk2 = hsk1.DeriveHardened(8444);
    walletSkHardened = hsk2.DeriveHardened(2);
}
else
{
    using var mnemonic = new Mnemonic(mnemonicPhrase!);
    var seed = mnemonic.ToSeed("");
    using var masterSk = SecretKey.FromSeed(seed);

    // Unhardened intermediate
    using var sk1 = masterSk.DeriveUnhardened(12381);
    using var sk2 = sk1.DeriveUnhardened(8444);
    walletSk = sk2.DeriveUnhardened(2);
    walletPk = walletSk.PublicKey();

    // Hardened intermediate
    using var hsk1 = masterSk.DeriveHardened(12381);
    using var hsk2 = hsk1.DeriveHardened(8444);
    walletSkHardened = hsk2.DeriveHardened(2);
}

using var _ = walletPk;
using var _walletSk = walletSk;
using var _walletSkHardened = walletSkHardened;

// Generate puzzle hashes for the first N address indices (unhardened + hardened when SK available)
var puzzleHashes = new List<byte[]>();
var disposableKeys = new List<PublicKey>();
for (int i = 0; i < addressCount; i++)
{
    // Unhardened
    var childPk = walletPk.DeriveUnhardened((uint)i);
    var syntheticPk = childPk.DeriveSynthetic();
    childPk.Dispose();
    puzzleHashes.Add(ChiaWalletSdkMethods.StandardPuzzleHash(syntheticPk));
    disposableKeys.Add(syntheticPk);

    // Hardened (requires secret key)
    if (walletSkHardened is not null)
    {
        using var hardenedChildSk = walletSkHardened.DeriveHardened((uint)i);
        var hardenedChildPk = hardenedChildSk.PublicKey();
        var hardenedSyntheticPk = hardenedChildPk.DeriveSynthetic();
        hardenedChildPk.Dispose();
        puzzleHashes.Add(ChiaWalletSdkMethods.StandardPuzzleHash(hardenedSyntheticPk));
        disposableKeys.Add(hardenedSyntheticPk);
    }
}

// Print first address for verification against `chia keys show`
using var firstAddr = new Address(puzzleHashes[0], "xch");
Console.WriteLine($"First addr  : {firstAddr.Encode()}");
var hardenedNote = walletSkHardened is not null
    ? $" ({addressCount} unhardened + {addressCount} hardened)"
    : $" ({addressCount} unhardened only)";
Console.WriteLine($"Checking {puzzleHashes.Count} addresses...{hardenedNote}");

// 2. Connect to peer
Console.WriteLine($"Connecting to {peerHost} ({networkId})...");
using var cert = Certificate.Generate();
using var connector = new Connector(cert);
using var options = new PeerOptions();
using var peer = await Peer.Connect(networkId, peerHost, connector, options);

// 3. Fetch unspent coins for all puzzle hashes
var filters = new CoinStateFilters(
    includeSpent: false,
    includeUnspent: true,
    includeHinted: true,
    minAmount: "0"
);

var response = await peer.RequestPuzzleState(
    puzzleHashes.ToArray(),
    previousHeight: null,
    headerHash: Convert.FromHexString(genesisChallenge),
    filters,
    subscribe: false
);

// 4. Sum and print
var coinStates = response.GetCoinStates();
long totalMojos = 0;
foreach (var coinState in coinStates)
{
    using var coin = coinState.GetCoin();
    totalMojos += long.Parse(coin.GetAmount());
}

Console.WriteLine($"Coin Count  : {coinStates.Count()}");
Console.WriteLine($"Balance     : {totalMojos} mojos");
Console.WriteLine($"Balance     : {totalMojos / 1_000_000_000_000.0:F12} XCH");

// 5. Fetch blockchain state via public JSON-RPC (coinset.org)
Console.WriteLine();
Console.WriteLine($"Fetching blockchain state ({networkId})...");
using var rpc = networkId.Equals("testnet11", StringComparison.OrdinalIgnoreCase)
    ? RpcClient.Testnet11()
    : RpcClient.Mainnet();

using var stateResponse = await rpc.GetBlockchainState();
if (!stateResponse.GetSuccess())
{
    Console.Error.WriteLine($"RPC error   : {stateResponse.GetError()}");
}
else
{
    using var state = stateResponse.GetBlockchainState();
    if (state is null)
    {
        Console.WriteLine("No blockchain state returned.");
    }
    else
    {
        using var sync = state.GetSync();
        using var peak = state.GetPeak();
        Console.WriteLine($"Synced      : {sync.GetSynced()} (mode={sync.GetSyncMode()})");
        Console.WriteLine($"Peak height : {peak.GetHeight()}");
        Console.WriteLine(
            $"Peak hash   : {Convert.ToHexString(peak.GetHeaderHash()).ToLowerInvariant()}"
        );
        Console.WriteLine($"Difficulty  : {state.GetDifficulty()}");
        Console.WriteLine($"Net space   : {state.GetSpace()}");
        Console.WriteLine($"Mempool size: {state.GetMempoolSize()}");
    }
}

// Clean up derived keys
foreach (var key in disposableKeys)
    key.Dispose();

return 0;

static string? GetArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
