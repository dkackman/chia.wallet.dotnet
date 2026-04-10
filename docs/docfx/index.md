---
_layout: landing
---

# Chia Wallet SDK for .NET

.NET bindings for the [chia-wallet-sdk](https://github.com/dkackman/chia-wallet-sdk), providing a C# interface to the Chia blockchain.

## Getting Started

The SDK is distributed as the `ChiaWalletSdk` NuGet package. All types live in the `ChiaWalletSdk` namespace and implement `IDisposable` for deterministic native resource cleanup.

```csharp
using ChiaWalletSdk;

var genesisChallenge ="ccd5bb71183532bff220ba46c268991a3ff07eb358e8255a65c30a2dce0e5fbb";

const int addressCount = 500;
using var masterPk = PublicKey.FromBytes(Convert.FromHexString("publicKeyHex"));
using var pk1 = masterPk.DeriveUnhardened(12381);
using var pk2 = pk1.DeriveUnhardened(8444);
PublicKey walletPk = pk2.DeriveUnhardened(2);

var puzzleHashes = new List<byte[]>();
var disposableKeys = new List<PublicKey>();
for (int i = 0; i < addressCount; i++)
{
    var childPk = walletPk.DeriveUnhardened((uint)i);
    var syntheticPk = childPk.DeriveSynthetic();
    childPk.Dispose();
    puzzleHashes.Add(ChiaWalletSdkMethods.StandardPuzzleHash(syntheticPk));
    disposableKeys.Add(syntheticPk);
}

using var firstAddr = new Address(puzzleHashes[0], "xch");
using var cert = Certificate.Generate();
using var connector = new Connector(cert);
using var options = new PeerOptions();
using var peer = await Peer.Connect("mainnet", "37.110.156.131:8444", connector, options);

var filters = new CoinStateFilters(
    includeSpent: false,
    includeUnspent: true,
    includeHinted: true,
    minAmount: "0");

var response = await peer.RequestPuzzleState(
    puzzleHashes,
    previousHeight: null,
    headerHash: Convert.FromHexString(genesisChallenge),
    filters,
    subscribe: false);

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

using var rpc = RpcClient.Mainnet();
using var stateResponse = await rpc.GetBlockchainState();
using var state = stateResponse.GetBlockchainState();
using var sync = state.GetSync();
using var peak = state.GetPeak();
Console.WriteLine($"Synced      : {sync.GetSynced()} (mode={sync.GetSyncMode()})");
Console.WriteLine($"Peak height : {peak.GetHeight()}");
Console.WriteLine($"Peak hash   : {Convert.ToHexString(peak.GetHeaderHash()).ToLowerInvariant()}");
Console.WriteLine($"Difficulty  : {state.GetDifficulty()}");
Console.WriteLine($"Net space   : {state.GetSpace()}");
Console.WriteLine($"Mempool size: {state.GetMempoolSize()}");

foreach (var key in disposableKeys) key.Dispose();
```

## API Reference

Browse the full [API documentation](api/index.md).
