# Design: chia.wallet.dotnet

**Date:** 2026-04-08  
**Status:** Approved

## Overview

Bootstrap the `chia.wallet.dotnet` repository as a .NET 8 solution that directly consumes the locally-built `ChiaWalletSdk` NuGet package from the sibling `chia-wallet-sdk` repo. No intermediate wrapper library — the test project and CLI app reference the SDK bindings directly.

## Solution Structure

```
chia.wallet.dotnet/
  Chia.Wallet.sln
  nuget.config                     ← relative path to chia-wallet-sdk/nuget-out
  docs/
    superpowers/specs/
      2026-04-08-chia-wallet-dotnet-design.md
  src/
    Chia.Wallet.Cli/
      Chia.Wallet.Cli.csproj
      Program.cs
      .env                         ← gitignored, dev secrets
  tests/
    Chia.Wallet.Tests/
      Chia.Wallet.Tests.csproj
      Unit/
        MnemonicTests.cs
        KeyDerivationTests.cs
        CoinIdTests.cs
      Integration/
        PeerConnectionTests.cs
        BalanceTests.cs
      TestConfig.cs
      .env                         ← gitignored, dev secrets
```

## NuGet Feed

A `nuget.config` at the solution root registers the local feed with a relative path:

```xml
<add key="chia-local" value="../chia-wallet-sdk/nuget-out" />
```

This makes the repo self-contained for collaborators. Users who have already run `dotnet nuget add source` globally will not be affected — NuGet merges feed sources.

Package reference: `ChiaWalletSdk` version `0.0.0-local`  
SDK namespace: `uniffi.chia_wallet_sdk`  
Target framework: `net8.0` (LTS, matches SDK)

## Test Project (`Chia.Wallet.Tests`)

### Dependencies
- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`
- `coverlet.collector`
- `DotNetEnv` — loads `.env` file at test startup

### Unit / Integration Separation

Integration tests are tagged with an xUnit trait:

```csharp
[Trait("Category", "Integration")]
```

CI (offline): `dotnet test --filter "Category!=Integration"`  
Full run: `dotnet test`

### Test Coverage Stubs

| Class | Category | Description |
|---|---|---|
| `MnemonicTests` | Unit | Generate mnemonic, round-trip from entropy, verify word count |
| `KeyDerivationTests` | Unit | `SecretKey.FromSeed`, derive `PublicKey`, compute puzzle hash |
| `CoinIdTests` | Unit | Coin ID computation from parent coin info, puzzle hash, amount |
| `PeerConnectionTests` | Integration | `Peer.Connect(...)` returns a non-null peer without throwing |
| `BalanceTests` | Integration | Fetch coin records for a known puzzle hash, assert response shape |

### Integration Test Configuration

Read from environment variables (populated via `.env` using `DotNetEnv`):

| Variable | Purpose |
|---|---|
| `CHIA_PEER_HOST` | Peer WebSocket address, e.g. `wss://node.example.com:8444` |
| `CHIA_NETWORK_ID` | Network identifier, e.g. `mainnet` or `testnet11` |

`TestConfig.cs` loads `.env` on first access and exposes typed properties. Tests that require these vars skip themselves (via `Skip`) if either is absent, so the suite never hard-fails in environments without a live node.

## CLI App (`Chia.Wallet.Cli`)

### Dependencies
- `ChiaWalletSdk 0.0.0-local` (direct, no wrapper)
- `DotNetEnv` — loads `.env` for dev convenience

### Invocation

```
dotnet run -- --peer <wss://host:port> --mnemonic "<24 words>" [--network mainnet|testnet11]
```

Falls back to environment variables / `.env`:

| Variable | CLI flag |
|---|---|
| `CHIA_PEER_HOST` | `--peer` |
| `CHIA_MNEMONIC` | `--mnemonic` |
| `CHIA_NETWORK_ID` | `--network` (default: `mainnet`) |

### Execution Sequence

1. Parse mnemonic → `SecretKey.FromSeed` → derive `PublicKey` → compute puzzle hash
2. `Peer.Connect(networkId, socketAddr, connector, options)`
3. Fetch coin records for the derived puzzle hash
4. Print to console:
   - Puzzle hash (hex)
   - Total balance in mojos
   - Total balance in XCH (mojos ÷ 10¹²)
   - Coin count

Output is plain console text — no formatting library.

## Constraints & Assumptions

- Both repos must remain siblings on disk (`/src/dkackman/chia-wallet-sdk` and `/src/dkackman/chia.wallet.dotnet`) for the relative nuget.config path to resolve.
- The local NuGet package must be built before `dotnet restore` (`./pack-local.sh` in the SDK repo).
- No wrapper/facade library at this stage — add one only if a clear pattern of re-use emerges across CLI and tests.
- `.env` files are gitignored in both projects; secrets never enter source control.
