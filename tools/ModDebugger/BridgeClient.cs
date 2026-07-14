using System.Net.Sockets;
using System.Text;

namespace ModDebugger;

/// <summary>
/// The live wire to the running game: the dev bridge's TCP socket. It pushes every line
/// the mod speaks (as "! spoken ..."), and it takes commands.
///
/// Everything here runs off the UI thread; callers get their events marshalled back via
/// the callbacks they hand in.
/// </summary>
internal sealed class BridgeClient : IDisposable
{
    private readonly Action<string> onSpoken;
    private readonly Action<string> onStatus;

    private TcpClient? client;
    private StreamReader? reader;
    private StreamWriter? writer;
    private Thread? readThread;
    private volatile bool running;

    /// <summary>Responses to a command arrive on the same stream as the events - this collects them.</summary>
    private readonly List<string> pendingResponse = new();
    private readonly AutoResetEvent responseComplete = new(false);

    public BridgeClient(Action<string> onSpoken, Action<string> onStatus)
    {
        this.onSpoken = onSpoken;
        this.onStatus = onStatus;
    }

    public bool IsConnected => client?.Connected == true;

    /// <summary>
    /// The bridge writes its port to the game's UserData - the game folder is the only
    /// thing this tool needs to be told.
    /// </summary>
    public static int? ReadPort(string gamePath)
    {
        try
        {
            var portFile = Path.Combine(gamePath, "UserData", "DevBridge", "port.txt");
            if (!File.Exists(portFile)) return null;
            return int.TryParse(File.ReadAllText(portFile).Trim(), out var port) ? port : null;
        }
        catch
        {
            return null;
        }
    }

    public bool Connect(string gamePath)
    {
        Disconnect();

        var port = ReadPort(gamePath);
        if (port == null)
        {
            onStatus("Bridge port file not found - is the game running with the dev bridge installed?");
            return false;
        }

        try
        {
            client = new TcpClient();
            client.Connect("127.0.0.1", port.Value);
            var stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            running = true;
            readThread = new Thread(ReadLoop) { IsBackground = true };
            readThread.Start();

            onStatus($"Connected to the game on port {port}.");
            return true;
        }
        catch (Exception ex)
        {
            onStatus($"Could not connect: {ex.Message}");
            client = null;
            return false;
        }
    }

    private void ReadLoop()
    {
        try
        {
            while (running && reader != null)
            {
                var line = reader.ReadLine();
                if (line == null) break;

                if (line.StartsWith("! spoken ", StringComparison.Ordinal))
                {
                    onSpoken(line["! spoken ".Length..]);
                }
                else if (line.StartsWith("! ", StringComparison.Ordinal))
                {
                    // scene changes, dialogue start/end - context, not speech
                    onStatus(line[2..]);
                }
                else if (line == "<<END>>")
                {
                    responseComplete.Set();
                }
                else
                {
                    lock (pendingResponse) pendingResponse.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
            if (running) onStatus($"Lost the connection: {ex.Message}");
        }
        finally
        {
            running = false;
        }
    }

    /// <summary>Sends a bridge command and waits briefly for its response.</summary>
    public string SendCommand(string command)
    {
        if (writer == null || !IsConnected) return "Not connected.";

        lock (pendingResponse) pendingResponse.Clear();
        responseComplete.Reset();

        try
        {
            writer.WriteLine(command);
        }
        catch (Exception ex)
        {
            return $"Send failed: {ex.Message}";
        }

        responseComplete.WaitOne(TimeSpan.FromSeconds(5));
        lock (pendingResponse)
        {
            return pendingResponse.Count == 0 ? "(no response)" : string.Join(Environment.NewLine, pendingResponse);
        }
    }

    public void Disconnect()
    {
        running = false;
        try { client?.Close(); } catch { /* best-effort */ }
        client = null;
        reader = null;
        writer = null;
    }

    public void Dispose() => Disconnect();
}
