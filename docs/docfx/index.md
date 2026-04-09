---
_layout: landing
---

# Chia Wallet SDK for .NET

.NET bindings for the [chia-wallet-sdk](https://github.com/dkackman/chia-wallet-sdk), providing a C# interface to the Chia blockchain.

## Getting Started

The SDK is distributed as the `ChiaWalletSdk` NuGet package. All types live in the `uniffi.chia_wallet_sdk` namespace and implement `IDisposable` for deterministic native resource cleanup.

```csharp
using uniffi.chia_wallet_sdk;

using var mnemonic = new Mnemonic("your twenty four word mnemonic ...");
var seed = mnemonic.ToSeed("");
using var masterSk = SecretKey.FromSeed(seed);
```

## API Reference

Browse the full [API documentation](api/).
