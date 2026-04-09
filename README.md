# chia.wallet.dotnet

.NET bindings for the [chia-wallet-sdk](https://github.com/dkackman/chia-wallet-sdk), providing a C# interface to the Chia blockchain via UniFFI-generated interop.

## Overview

This solution wraps the Rust-based `chia-wallet-sdk` using [uniffi-bindgen-cs](https://github.com/NordSecurity/uniffi-bindgen-cs), exposing a comprehensive set of types for interacting with the Chia network from .NET. It supports key derivation, peer connections, coin state queries, and the full range of Chia primitives including CATs, NFTs, DIDs, offers, vaults, and more.

## Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- The [`ChiaWalletSdk`](https://www.nuget.org/packages/ChiaWalletSdk/) NuGet package

## Solution Structure

```
Chia.Wallet.sln
├── src/
│   └── Chia.Wallet.Cli/        # Example CLI app: derives keys, connects to a peer, prints balance
└── tests/
    └── Chia.Wallet.Tests/       # xUnit test suite
        ├── Unit/                # Mnemonic, key derivation, coin ID tests
        └── Integration/         # Peer connection, balance query tests
```

## Setup

The `ChiaWalletSdk` package is available on [nuget.org](https://www.nuget.org/packages/ChiaWalletSdk/).

```bash
dotnet restore
dotnet build
```

## CLI Example

The `Chia.Wallet.Cli` project is a minimal wallet that derives keys from a mnemonic, connects to a full node peer, and prints the XCH balance for the first wallet address.

```bash
dotnet run --project src/Chia.Wallet.Cli -- \
  --peer wss://localhost:8444 \
  --mnemonic "your twenty four word mnemonic phrase ..." \
  --network mainnet
```

Or set environment variables (or a `.env` file):

```
CHIA_PEER_HOST=wss://localhost:8444
CHIA_MNEMONIC=your twenty four word mnemonic phrase ...
CHIA_NETWORK_ID=mainnet
```

### Sample Output

```
Puzzle Hash : 3f2a...
Connecting to wss://localhost:8444 (mainnet)...
Coin Count  : 5
Balance     : 1500000000000 mojos
Balance     : 1.500000000000 XCH
```

## Running Tests

Unit tests run without any external dependencies:

```bash
dotnet test --filter "FullyQualifiedName~Unit"
```

Integration tests require a running Chia node. Configure connection details via a `.env` file in the test project directory or environment variables, then:

```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

Run all tests:

```bash
dotnet test
```

## Key Types

The SDK exposes the Chia domain model as disposable C# classes under the `uniffi.chia_wallet_sdk` namespace. Some highlights:

| Type | Description |
|------|-------------|
| `Mnemonic` | BIP-39 mnemonic generation and seed derivation |
| `SecretKey` / `PublicKey` | BLS key derivation (hardened, synthetic) |
| `Peer` | Async connection to a Chia full node |
| `Coin` / `CoinState` | On-chain coin representation and state |
| `Cat` / `CatInfo` | Chia Asset Token support |
| `Nft` / `NftInfo` / `NftMetadata` | NFT primitives |
| `Did` / `DidInfo` | Decentralized identity |
| `SpendBundle` / `CoinSpend` | Transaction construction |
| `Vault` / `MedievalVault` | Vault primitives |
| `Clvm` / `Program` | CLVM program manipulation |
| `Simulator` | Local chain simulator for testing |

All object types implement `IDisposable` — use `using` statements or explicit disposal to release native resources.

## License

See the [chia-wallet-sdk](https://github.com/dkackman/chia-wallet-sdk) repository for license details.
