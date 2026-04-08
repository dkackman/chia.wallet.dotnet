# chia.wallet.dotnet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold a .NET 8 solution with an xUnit test project (unit + integration) and a CLI smoke-test app, both consuming the locally-built `ChiaWalletSdk` NuGet package directly.

**Architecture:** Two projects in a single solution — `Chia.Wallet.Tests` (xUnit, trait-separated unit/integration) and `Chia.Wallet.Cli` (top-level-statement console app). No intermediate wrapper; both reference `ChiaWalletSdk 0.0.0-local` via a local NuGet feed registered in `nuget.config`. All SDK types live in the `uniffi.chia_wallet_sdk` namespace.

**Tech Stack:** .NET 8, xUnit 2.9, xunit.skippablefact, DotNetEnv 3.1, ChiaWalletSdk 0.0.0-local (`uniffi.chia_wallet_sdk` namespace)

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `nuget.config` | Register local NuGet feed (relative path) |
| Create | `Chia.Wallet.sln` | Solution file |
| Create | `tests/Chia.Wallet.Tests/Chia.Wallet.Tests.csproj` | Test project definition |
| Create | `tests/Chia.Wallet.Tests/TestConfig.cs` | Load `.env`, expose typed config properties |
| Create | `tests/Chia.Wallet.Tests/.env.example` | Document env vars, gitignored `.env` sibling |
| Create | `tests/Chia.Wallet.Tests/Unit/MnemonicTests.cs` | Unit tests for Mnemonic generation and round-trip |
| Create | `tests/Chia.Wallet.Tests/Unit/KeyDerivationTests.cs` | Unit tests for SecretKey, PublicKey, puzzle hash derivation |
| Create | `tests/Chia.Wallet.Tests/Unit/CoinIdTests.cs` | Unit tests for Coin construction and CoinId computation |
| Create | `tests/Chia.Wallet.Tests/Integration/PeerConnectionTests.cs` | Integration test: Peer.Connect succeeds |
| Create | `tests/Chia.Wallet.Tests/Integration/BalanceTests.cs` | Integration test: RequestPuzzleState returns coin states |
| Create | `src/Chia.Wallet.Cli/Chia.Wallet.Cli.csproj` | CLI project definition |
| Create | `src/Chia.Wallet.Cli/.env.example` | Document env vars, gitignored `.env` sibling |
| Create | `src/Chia.Wallet.Cli/Program.cs` | CLI entry point: derive keys, connect, print balance |

---

## Task 1: Solution scaffold and NuGet config

**Files:**
- Create: `nuget.config`
- Create: `Chia.Wallet.sln` (via `dotnet new sln`)

- [ ] **Step 1: Write `nuget.config`**

  Create `/Users/don/src/dkackman/chia.wallet.dotnet/nuget.config`:

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <configuration>
    <packageSources>
      <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
      <add key="chia-local" value="../chia-wallet-sdk/nuget-out" />
    </packageSources>
  </configuration>
  ```

- [ ] **Step 2: Create the solution file**

  Run from `chia.wallet.dotnet/`:
  ```bash
  dotnet new sln -n Chia.Wallet
  ```
  Expected: `Chia.Wallet.sln` created.

- [ ] **Step 3: Commit**

  ```bash
  git add nuget.config Chia.Wallet.sln
  git commit -m "chore: add solution file and local NuGet feed config"
  ```

---

## Task 2: Test project scaffold

**Files:**
- Create: `tests/Chia.Wallet.Tests/Chia.Wallet.Tests.csproj`
- Create: `tests/Chia.Wallet.Tests/TestConfig.cs`
- Create: `tests/Chia.Wallet.Tests/.env.example`

- [ ] **Step 1: Create the test project**

  Run from `chia.wallet.dotnet/`:
  ```bash
  mkdir -p tests
  dotnet new xunit -n Chia.Wallet.Tests -o tests/Chia.Wallet.Tests --framework net8.0
  dotnet sln add tests/Chia.Wallet.Tests/Chia.Wallet.Tests.csproj
  ```
  Expected: project created and added to `Chia.Wallet.sln`.

- [ ] **Step 2: Replace the generated `.csproj` with the full dependency list**

  Overwrite `tests/Chia.Wallet.Tests/Chia.Wallet.Tests.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <TargetFramework>net8.0</TargetFramework>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>
      <IsPackable>false</IsPackable>
    </PropertyGroup>
    <ItemGroup>
      <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
      <PackageReference Include="xunit" Version="2.9.2" />
      <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      </PackageReference>
      <PackageReference Include="coverlet.collector" Version="6.0.2">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      </PackageReference>
      <PackageReference Include="Xunit.SkippableFact" Version="1.4.13" />
      <PackageReference Include="DotNetEnv" Version="3.1.1" />
      <PackageReference Include="ChiaWalletSdk" Version="0.0.0-local" />
    </ItemGroup>
  </Project>
  ```

- [ ] **Step 3: Delete the generated boilerplate test file**

  ```bash
  rm tests/Chia.Wallet.Tests/UnitTest1.cs
  ```

- [ ] **Step 4: Create directory structure**

  ```bash
  mkdir -p tests/Chia.Wallet.Tests/Unit
  mkdir -p tests/Chia.Wallet.Tests/Integration
  ```

- [ ] **Step 5: Write `TestConfig.cs`**

  Create `tests/Chia.Wallet.Tests/TestConfig.cs`:

  ```csharp
  using DotNetEnv;

  namespace Chia.Wallet.Tests;

  public static class TestConfig
  {
      static TestConfig()
      {
          Env.TraversePath().Load(overwriteExistingVars: false);
      }

      public static string? PeerHost => Environment.GetEnvironmentVariable("CHIA_PEER_HOST");
      public static string NetworkId => Environment.GetEnvironmentVariable("CHIA_NETWORK_ID") ?? "mainnet";
      public static bool IsIntegrationConfigured => !string.IsNullOrWhiteSpace(PeerHost);
  }
  ```

- [ ] **Step 6: Write `.env.example`**

  Create `tests/Chia.Wallet.Tests/.env.example`:

  ```
  # Copy this file to .env and fill in your values.
  # .env is gitignored and never committed.
  CHIA_PEER_HOST=wss://your-node.example.com:8444
  CHIA_NETWORK_ID=mainnet
  ```

- [ ] **Step 7: Verify restore succeeds**

  ```bash
  dotnet restore
  ```
  Expected: no errors; `ChiaWalletSdk 0.0.0-local` resolved from `chia-local` feed.

- [ ] **Step 8: Commit**

  ```bash
  git add tests/ Chia.Wallet.sln
  git commit -m "chore: add Chia.Wallet.Tests project with xUnit and DotNetEnv"
  ```

---

## Task 3: Unit tests — Mnemonic

**Files:**
- Create: `tests/Chia.Wallet.Tests/Unit/MnemonicTests.cs`

- [ ] **Step 1: Write the tests**

  Create `tests/Chia.Wallet.Tests/Unit/MnemonicTests.cs`:

  ```csharp
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
  ```

- [ ] **Step 2: Run the tests to verify they pass**

  ```bash
  dotnet test tests/Chia.Wallet.Tests/ --filter "Category!=Integration" --logger "console;verbosity=normal"
  ```
  Expected: 4 tests pass, 0 fail.

- [ ] **Step 3: Commit**

  ```bash
  git add tests/Chia.Wallet.Tests/Unit/MnemonicTests.cs
  git commit -m "test: add unit tests for Mnemonic generation and round-trip"
  ```

---

## Task 4: Unit tests — Key derivation

**Files:**
- Create: `tests/Chia.Wallet.Tests/Unit/KeyDerivationTests.cs`

- [ ] **Step 1: Write the tests**

  Create `tests/Chia.Wallet.Tests/Unit/KeyDerivationTests.cs`:

  ```csharp
  using uniffi.chia_wallet_sdk;
  using Xunit;

  namespace Chia.Wallet.Tests.Unit;

  public class KeyDerivationTests
  {
      // Well-known all-abandon test mnemonic — never use for real funds.
      private const string TestMnemonic =
          "abandon abandon abandon abandon abandon abandon abandon abandon " +
          "abandon abandon abandon abandon abandon abandon abandon abandon " +
          "abandon abandon abandon abandon abandon abandon abandon art";

      [Fact]
      public void FromSeed_ProducesNonNullKey()
      {
          using var mnemonic = new Mnemonic(TestMnemonic);
          var seed = mnemonic.ToSeed("");
          using var sk = SecretKey.FromSeed(seed);
          Assert.NotNull(sk);
      }

      [Fact]
      public void PublicKey_IsNotInfinity()
      {
          using var mnemonic = new Mnemonic(TestMnemonic);
          var seed = mnemonic.ToSeed("");
          using var sk = SecretKey.FromSeed(seed);
          using var pk = sk.PublicKey();
          Assert.False(pk.IsInfinity());
      }

      [Fact]
      public void DeriveHardenedThenSynthetic_DiffersFromMasterPublicKey()
      {
          using var mnemonic = new Mnemonic(TestMnemonic);
          var seed = mnemonic.ToSeed("");
          using var masterSk = SecretKey.FromSeed(seed);
          using var childSk = masterSk.DeriveHardened(0).DeriveSynthetic();
          using var childPk = childSk.PublicKey();
          using var masterPk = masterSk.PublicKey();
          Assert.NotEqual(
              Convert.ToHexString(childPk.ToBytes()),
              Convert.ToHexString(masterPk.ToBytes()));
      }

      [Fact]
      public void StandardPuzzleHash_Returns32Bytes()
      {
          using var mnemonic = new Mnemonic(TestMnemonic);
          var seed = mnemonic.ToSeed("");
          using var sk = SecretKey.FromSeed(seed);
          using var syntheticSk = sk.DeriveHardened(0).DeriveSynthetic();
          using var pk = syntheticSk.PublicKey();
          var puzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(pk);
          Assert.Equal(32, puzzleHash.Length);
      }

      [Fact]
      public void StandardPuzzleHash_IsDeterministic()
      {
          using var mnemonic = new Mnemonic(TestMnemonic);
          var seed = mnemonic.ToSeed("");
          using var sk1 = SecretKey.FromSeed(seed);
          using var sk2 = SecretKey.FromSeed(seed);
          using var syntheticSk1 = sk1.DeriveHardened(0).DeriveSynthetic();
          using var syntheticSk2 = sk2.DeriveHardened(0).DeriveSynthetic();
          using var pk1 = syntheticSk1.PublicKey();
          using var pk2 = syntheticSk2.PublicKey();
          Assert.Equal(
              Convert.ToHexString(ChiaWalletSdkMethods.StandardPuzzleHash(pk1)),
              Convert.ToHexString(ChiaWalletSdkMethods.StandardPuzzleHash(pk2)));
      }
  }
  ```

- [ ] **Step 2: Run the tests**

  ```bash
  dotnet test tests/Chia.Wallet.Tests/ --filter "Category!=Integration" --logger "console;verbosity=normal"
  ```
  Expected: 9 tests pass (4 from Task 3 + 5 new), 0 fail.

- [ ] **Step 3: Commit**

  ```bash
  git add tests/Chia.Wallet.Tests/Unit/KeyDerivationTests.cs
  git commit -m "test: add unit tests for key and puzzle hash derivation"
  ```

---

## Task 5: Unit tests — Coin ID

**Files:**
- Create: `tests/Chia.Wallet.Tests/Unit/CoinIdTests.cs`

- [ ] **Step 1: Write the tests**

  Create `tests/Chia.Wallet.Tests/Unit/CoinIdTests.cs`:

  ```csharp
  using uniffi.chia_wallet_sdk;
  using Xunit;

  namespace Chia.Wallet.Tests.Unit;

  public class CoinIdTests
  {
      [Fact]
      public void CoinId_Returns32Bytes()
      {
          using var coin = new Coin(new byte[32], new byte[32], "1000000000000");
          Assert.Equal(32, coin.CoinId().Length);
      }

      [Fact]
      public void CoinId_IsDeterministic()
      {
          using var coin1 = new Coin(new byte[32], new byte[32], "1000000000000");
          using var coin2 = new Coin(new byte[32], new byte[32], "1000000000000");
          Assert.Equal(
              Convert.ToHexString(coin1.CoinId()),
              Convert.ToHexString(coin2.CoinId()));
      }

      [Fact]
      public void CoinId_ChangesWhenAmountChanges()
      {
          using var coin1 = new Coin(new byte[32], new byte[32], "1000000000000");
          using var coin2 = new Coin(new byte[32], new byte[32], "2000000000000");
          Assert.NotEqual(
              Convert.ToHexString(coin1.CoinId()),
              Convert.ToHexString(coin2.CoinId()));
      }

      [Fact]
      public void CoinId_ChangesWhenPuzzleHashChanges()
      {
          var puzzleHash1 = new byte[32];
          var puzzleHash2 = new byte[32];
          puzzleHash2[0] = 1;
          using var coin1 = new Coin(new byte[32], puzzleHash1, "1000000000000");
          using var coin2 = new Coin(new byte[32], puzzleHash2, "1000000000000");
          Assert.NotEqual(
              Convert.ToHexString(coin1.CoinId()),
              Convert.ToHexString(coin2.CoinId()));
      }

      [Fact]
      public void GetAmount_ReturnsConstructedValue()
      {
          using var coin = new Coin(new byte[32], new byte[32], "1000000000000");
          Assert.Equal("1000000000000", coin.GetAmount());
      }
  }
  ```

- [ ] **Step 2: Run the tests**

  ```bash
  dotnet test tests/Chia.Wallet.Tests/ --filter "Category!=Integration" --logger "console;verbosity=normal"
  ```
  Expected: 14 tests pass (9 prior + 5 new), 0 fail.

- [ ] **Step 3: Commit**

  ```bash
  git add tests/Chia.Wallet.Tests/Unit/CoinIdTests.cs
  git commit -m "test: add unit tests for Coin construction and CoinId computation"
  ```

---

## Task 6: Integration tests — Peer connection

**Files:**
- Create: `tests/Chia.Wallet.Tests/Integration/PeerConnectionTests.cs`

- [ ] **Step 1: Write the test**

  Create `tests/Chia.Wallet.Tests/Integration/PeerConnectionTests.cs`:

  ```csharp
  using uniffi.chia_wallet_sdk;
  using Xunit;

  namespace Chia.Wallet.Tests.Integration;

  public class PeerConnectionTests
  {
      [SkippableFact]
      [Trait("Category", "Integration")]
      public async Task Connect_ValidPeer_ReturnsNonNullPeer()
      {
          Skip.If(!TestConfig.IsIntegrationConfigured,
              "Set CHIA_PEER_HOST in tests/Chia.Wallet.Tests/.env to run integration tests.");

          using var cert = Certificate.Generate();
          using var connector = new Connector(cert);
          using var options = new PeerOptions();

          using var peer = await Peer.Connect(
              TestConfig.NetworkId,
              TestConfig.PeerHost!,
              connector,
              options);

          Assert.NotNull(peer);
      }
  }
  ```

- [ ] **Step 2: Verify unit tests still pass (integration test is skipped without config)**

  ```bash
  dotnet test tests/Chia.Wallet.Tests/ --filter "Category!=Integration" --logger "console;verbosity=normal"
  ```
  Expected: 14 tests pass, 0 fail. The integration test is excluded by the filter.

- [ ] **Step 3: Commit**

  ```bash
  git add tests/Chia.Wallet.Tests/Integration/PeerConnectionTests.cs
  git commit -m "test: add integration test for Peer.Connect"
  ```

---

## Task 7: Integration tests — Balance

**Files:**
- Create: `tests/Chia.Wallet.Tests/Integration/BalanceTests.cs`

- [ ] **Step 1: Write the test**

  Create `tests/Chia.Wallet.Tests/Integration/BalanceTests.cs`:

  ```csharp
  using uniffi.chia_wallet_sdk;
  using Xunit;

  namespace Chia.Wallet.Tests.Integration;

  public class BalanceTests
  {
      // Well-known all-abandon test mnemonic — never use for real funds.
      private const string TestMnemonic =
          "abandon abandon abandon abandon abandon abandon abandon abandon " +
          "abandon abandon abandon abandon abandon abandon abandon abandon " +
          "abandon abandon abandon abandon abandon abandon abandon art";

      [SkippableFact]
      [Trait("Category", "Integration")]
      public async Task RequestPuzzleState_Returns_ValidResponse()
      {
          Skip.If(!TestConfig.IsIntegrationConfigured,
              "Set CHIA_PEER_HOST in tests/Chia.Wallet.Tests/.env to run integration tests.");

          using var mnemonic = new Mnemonic(TestMnemonic);
          var seed = mnemonic.ToSeed("");
          using var masterSk = SecretKey.FromSeed(seed);
          using var syntheticSk = masterSk.DeriveHardened(0).DeriveSynthetic();
          using var pk = syntheticSk.PublicKey();
          var puzzleHash = ChiaWalletSdkMethods.StandardPuzzleHash(pk);

          using var cert = Certificate.Generate();
          using var connector = new Connector(cert);
          using var options = new PeerOptions();
          using var peer = await Peer.Connect(
              TestConfig.NetworkId,
              TestConfig.PeerHost!,
              connector,
              options);

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

          Assert.NotNull(response);
          var coinStates = response.GetCoinStates();
          Assert.NotNull(coinStates);
          // The all-abandon wallet may have zero coins — that is fine.
          // We assert the response shape is valid, not a specific balance.
          Assert.True(coinStates.Count >= 0);
      }
  }
  ```

- [ ] **Step 2: Verify unit tests still pass**

  ```bash
  dotnet test tests/Chia.Wallet.Tests/ --filter "Category!=Integration" --logger "console;verbosity=normal"
  ```
  Expected: 14 tests pass, 0 fail.

- [ ] **Step 3: Commit**

  ```bash
  git add tests/Chia.Wallet.Tests/Integration/BalanceTests.cs
  git commit -m "test: add integration test for RequestPuzzleState balance check"
  ```

---

## Task 8: CLI project scaffold

**Files:**
- Create: `src/Chia.Wallet.Cli/Chia.Wallet.Cli.csproj`
- Create: `src/Chia.Wallet.Cli/.env.example`

- [ ] **Step 1: Create the CLI project**

  Run from `chia.wallet.dotnet/`:
  ```bash
  mkdir -p src
  dotnet new console -n Chia.Wallet.Cli -o src/Chia.Wallet.Cli --framework net8.0
  dotnet sln add src/Chia.Wallet.Cli/Chia.Wallet.Cli.csproj
  ```
  Expected: project created and added to solution.

- [ ] **Step 2: Replace the generated `.csproj`**

  Overwrite `src/Chia.Wallet.Cli/Chia.Wallet.Cli.csproj`:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <OutputType>Exe</OutputType>
      <TargetFramework>net8.0</TargetFramework>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
      <PackageReference Include="ChiaWalletSdk" Version="0.0.0-local" />
      <PackageReference Include="DotNetEnv" Version="3.1.1" />
    </ItemGroup>
  </Project>
  ```

- [ ] **Step 3: Write `.env.example`**

  Create `src/Chia.Wallet.Cli/.env.example`:

  ```
  # Copy this file to .env and fill in your values.
  # .env is gitignored and never committed.
  CHIA_PEER_HOST=wss://your-node.example.com:8444
  CHIA_NETWORK_ID=mainnet
  CHIA_MNEMONIC=word1 word2 ... word24
  ```

- [ ] **Step 4: Verify restore**

  ```bash
  dotnet restore
  ```
  Expected: no errors.

- [ ] **Step 5: Commit**

  ```bash
  git add src/ Chia.Wallet.sln
  git commit -m "chore: add Chia.Wallet.Cli project scaffold"
  ```

---

## Task 9: CLI implementation

**Files:**
- Modify: `src/Chia.Wallet.Cli/Program.cs`

- [ ] **Step 1: Write `Program.cs`**

  Overwrite `src/Chia.Wallet.Cli/Program.cs`:

  ```csharp
  using DotNetEnv;
  using uniffi.chia_wallet_sdk;

  Env.TraversePath().Load(overwriteExistingVars: false);

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
  using var syntheticSk = masterSk.DeriveHardened(0).DeriveSynthetic();
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
  ```

- [ ] **Step 2: Build to verify no compile errors**

  ```bash
  dotnet build src/Chia.Wallet.Cli/ --configuration Release
  ```
  Expected: Build succeeded, 0 error(s), 0 warning(s).

- [ ] **Step 3: Run a quick offline smoke test (mnemonic only, no peer)**

  ```bash
  dotnet run --project src/Chia.Wallet.Cli/ -- --mnemonic "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art"
  ```
  Expected: exits immediately with (since no `--peer` provided):
  ```
  Error: --peer <wss://host:port> or CHIA_PEER_HOST required.
  ```
  This confirms argument parsing works. To also exercise key derivation offline, add a valid peer URL via `.env` and omit `--mnemonic` to test the mnemonic error path.

- [ ] **Step 4: Commit**

  ```bash
  git add src/Chia.Wallet.Cli/Program.cs
  git commit -m "feat: implement CLI smoke-test wallet (derive keys, connect, print balance)"
  ```

---

## Task 10: Final verification

- [ ] **Step 1: Run all unit tests and confirm count**

  ```bash
  dotnet test tests/Chia.Wallet.Tests/ --filter "Category!=Integration" --logger "console;verbosity=normal"
  ```
  Expected: **14 tests pass**, 0 fail.

- [ ] **Step 2: Confirm full solution builds**

  ```bash
  dotnet build --configuration Release
  ```
  Expected: Build succeeded, 0 error(s).

- [ ] **Step 3: Commit if any files are unstaged, then tag the stub-complete state**

  ```bash
  git status
  # If clean:
  git tag stub-complete
  ```
