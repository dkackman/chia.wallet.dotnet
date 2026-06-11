using ChiaWalletSdk;
using DotNetEnv;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

Env.Load(".env", Env.NoClobber());

return await ProgramMain(args);

static async Task<int> ProgramMain(string[] args)
{
    var peerArg = GetArg(args, "--peer") ?? Environment.GetEnvironmentVariable("CHIA_PEER_HOST");
    var mnemonicPhrase = GetArg(args, "--mnemonic") ?? Environment.GetEnvironmentVariable("CHIA_MNEMONIC");
    var secretKeyHex = GetArg(args, "--secret-key") ?? Environment.GetEnvironmentVariable("CHIA_SECRET_KEY");
    var publicKeyHex = GetArg(args, "--public-key") ?? Environment.GetEnvironmentVariable("CHIA_PUBLIC_KEY");
    var networkId = GetArg(args, "--network") ?? Environment.GetEnvironmentVariable("CHIA_NETWORK_ID") ?? "mainnet";
    var genesisChallenge = GetArg(args, "--genesis-challenge") ?? Environment.GetEnvironmentVariable("CHIA_GENESIS_CHALLENGE") ?? "ccd5bb71183532bff220ba46c268991a3ff07eb358e8255a65c30a2dce0e5fbb";

    var hasKey = !string.IsNullOrWhiteSpace(mnemonicPhrase) || !string.IsNullOrWhiteSpace(secretKeyHex) || !string.IsNullOrWhiteSpace(publicKeyHex);
    if (!hasKey)
    {
        Console.Error.WriteLine("Error: Provide one of:");
        Console.Error.WriteLine("  --mnemonic \"<24 words>\"  (or CHIA_MNEMONIC)");
        Console.Error.WriteLine("  --secret-key <hex>       (or CHIA_SECRET_KEY)");
        Console.Error.WriteLine("  --public-key <hex>       (or CHIA_PUBLIC_KEY)");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine($"Fetching blockchain state ({networkId})...");
    using var rpcOptions = new RpcClientOptions(requestTimeoutMs: 15_000, connectTimeoutMs: 10_000);
    using var rpcBase = networkId.Equals("testnet11", StringComparison.OrdinalIgnoreCase) ? RpcClient.Testnet11() : RpcClient.Mainnet();
    using var rpc = rpcBase.WithOptions(rpcOptions);
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
            Console.WriteLine($"Peak hash   : {Convert.ToHexString(peak.GetHeaderHash()).ToLowerInvariant()}");
            Console.WriteLine($"Difficulty  : {state.GetDifficulty()}");
            Console.WriteLine($"Net space   : {state.GetSpace()}");
            Console.WriteLine($"Mempool size: {state.GetMempoolSize()}");
        }
    }

    // Derive wallet keys and puzzle hashes
    const int addressCount = 10;
    var (puzzleHashes, disposableKeys) = DerivePuzzleHashes(addressCount, mnemonicPhrase, secretKeyHex, publicKeyHex);

    Console.WriteLine();
    using var firstAddr = new Address(puzzleHashes[0], "xch");
    Console.WriteLine($"First addr  : {firstAddr.Encode()}");

    // Peer discovery
    var peerCandidates = new List<string>();
    if (!string.IsNullOrWhiteSpace(peerArg))
    {
        peerCandidates.Add(peerArg);
    }
    else
    {
        Console.WriteLine("No peer provided; attempting DNS introducer discovery...");
        var dnsPeers = await PeerDiscovery.DiscoverViaDnsAsync("dns-introducer.chia.net", 8444);
        peerCandidates.AddRange(dnsPeers);
        if (peerCandidates.Count == 0)
        {
            Console.WriteLine("DNS introducer returned no peers; attempting websocket introducer fallback...");
            var wsPeers = await PeerDiscovery.DiscoverViaWebSocketFallbackAsync(new Uri("wss://introducer.chia.net:8444/ws"));
            peerCandidates.AddRange(wsPeers);
        }
    }

    if (peerCandidates.Count == 0)
    {
        Console.Error.WriteLine("No peers discovered. Set CHIA_PEER_HOST or pass --peer to connect.");
        return 2;
    }

    using var cert = Certificate.Generate();
    using var connector = new Connector(cert);
    using var options = new PeerOptions()
        .SetConnectTimeoutMs(10_000)   // TLS connect + chia handshake budget; null = unbounded
        .SetRequestTimeoutMs(15_000);  // per request/response round-trip; null = unbounded

    Peer? connectedPeer = null;
    foreach (var candidate in peerCandidates)
    {
        try
        {
            Console.WriteLine($"Attempting connect -> {candidate}");
            connectedPeer = await Peer.Connect(networkId, candidate, connector, options);
            Console.WriteLine($"Connected to {candidate}");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connect failed for {candidate}: {ex.Message}");
        }
    }

    if (connectedPeer is null)
    {
        Console.Error.WriteLine("Failed to connect to any discovered peer.");
        return 3;
    }

    using var peer = connectedPeer;

    // Query puzzle state
    var filters = new CoinStateFilters(includeSpent: false, includeUnspent: true, includeHinted: true, minAmount: "0");
    var response = await peer.RequestPuzzleState([.. puzzleHashes], null, Convert.FromHexString(genesisChallenge), filters, false);

    var coinStates = response.GetCoinStates();
    long totalMojos = 0;
    foreach (var coinState in coinStates)
    {
        using var coin = coinState.GetCoin();
        totalMojos += long.Parse(coin.GetAmount());
    }

    Console.WriteLine($"Coin Count  : {coinStates.Length}");
    Console.WriteLine($"Balance     : {totalMojos} mojos");
    Console.WriteLine($"Balance     : {totalMojos / 1_000_000_000_000.0:F12} XCH");

    foreach (var key in disposableKeys) key.Dispose();
    return 0;
}

static (List<byte[]> puzzleHashes, List<PublicKey> disposableKeys) DerivePuzzleHashes(int addressCount, string? mnemonicPhrase, string? secretKeyHex, string? publicKeyHex)
{
    PublicKey walletPk;
    SecretKey? walletSk = null;
    SecretKey? walletSkHardened = null;

    if (!string.IsNullOrWhiteSpace(publicKeyHex))
    {
        using var masterPk = PublicKey.FromBytes(Convert.FromHexString(publicKeyHex));
        using var pk1 = masterPk.DeriveUnhardened(12381);
        using var pk2 = pk1.DeriveUnhardened(8444);
        walletPk = pk2.DeriveUnhardened(2);
    }
    else if (!string.IsNullOrWhiteSpace(secretKeyHex))
    {
        using var sk = SecretKey.FromBytes(Convert.FromHexString(secretKeyHex));
        using var sk1 = sk.DeriveUnhardened(12381);
        using var sk2 = sk1.DeriveUnhardened(8444);
        walletSk = sk2.DeriveUnhardened(2);
        walletPk = walletSk.PublicKey();

        using var hsk1 = sk.DeriveHardened(12381);
        using var hsk2 = hsk1.DeriveHardened(8444);
        walletSkHardened = hsk2.DeriveHardened(2);
    }
    else
    {
        using var mnemonic = new Mnemonic(mnemonicPhrase!);
        var seed = mnemonic.ToSeed("");
        using var masterSk = SecretKey.FromSeed(seed);
        using var sk1 = masterSk.DeriveUnhardened(12381);
        using var sk2 = sk1.DeriveUnhardened(8444);
        walletSk = sk2.DeriveUnhardened(2);
        walletPk = walletSk.PublicKey();
        using var hsk1 = masterSk.DeriveHardened(12381);
        using var hsk2 = hsk1.DeriveHardened(8444);
        walletSkHardened = hsk2.DeriveHardened(2);
    }

    using var _ = walletPk;
    using var _walletSk = walletSk;
    using var _walletSkHardened = walletSkHardened;

    var puzzleHashes = new List<byte[]>();
    var disposableKeys = new List<PublicKey>();
    for (int i = 0; i < addressCount; i++)
    {
        var childPk = walletPk.DeriveUnhardened((uint)i);
        var syntheticPk = childPk.DeriveSynthetic();
        childPk.Dispose();
        puzzleHashes.Add(ChiaWalletSdkMethods.StandardPuzzleHash(syntheticPk));
        disposableKeys.Add(syntheticPk);

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

    return (puzzleHashes, disposableKeys);
}

static string? GetArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

static class PeerDiscovery
{
    public static async Task<List<string>> DiscoverViaDnsAsync(string host, int port)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var addrs = await Dns.GetHostAddressesAsync(host, cts.Token);
            var list = addrs
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork || a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                .Select(a => a.ToString())
                .Distinct()
                .Select(ip => ip + ":" + port)
                .ToList();
            Console.WriteLine($"DNS introducer returned {list.Count} addresses.");
            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DNS discovery failed: {ex.Message}");
            return [];
        }
    }

    public static async Task<List<string>> DiscoverViaWebSocketFallbackAsync(Uri uri)
    {
        var peers = new List<string>();
        try
        {
            using var ws = new ClientWebSocket();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ws.ConnectAsync(uri, cts.Token);
            var buffer = new ArraySegment<byte>(new byte[16 * 1024]);
            var result = await ws.ReceiveAsync(buffer, cts.Token);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var msg = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                try
                {
                    // Try parse as JSON array of strings
                    var arr = JsonSerializer.Deserialize<string[]>(msg);
                    if (arr is not null)
                    {
                        peers.AddRange(arr);
                        Console.WriteLine($"Websocket introducer returned {arr.Length} peers (JSON array).");
                        return peers.Distinct().ToList();
                    }
                }
                catch { }

                // Fallback: split by newlines and look for host:port patterns
                foreach (var line in msg.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains(":")) peers.Add(trimmed);
                }
                Console.WriteLine($"Websocket introducer returned {peers.Count} peers (text fallback).");
            }
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Websocket introducer fallback failed: {ex.Message}");
        }
        return peers.Distinct().ToList();
    }
}
