using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using SeeSharp.Common;
namespace SeeSharp.Blender;
public class BlenderEventListener {
    private TcpListener? _listener;
    private BlenderEventDispatcher _dispatcher;

    public BlenderEventListener(BlenderEventDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }

    public void RegisterDispatcher(BlenderEventDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }

    public async Task StartAsync() {
        _listener = new TcpListener(IPAddress.Loopback, 5052);
        _listener.Start();
        Logger.Log("📡 Listening for Blender events on 5052...");

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                Logger.Log("🔌 Blender connected to Blazor Event Listener");

                _ = HandleClientAsync(client);
            }
        });
    }

    private async Task HandleClientAsync(TcpClient client) {
        using var reader = new StreamReader(client.GetStream());

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            Logger.Log("📨 Received event: " + line);
            try
            {
                using var doc = JsonDocument.Parse(line);
                _dispatcher.Dispatch(doc.RootElement);
            }
            catch (Exception ex)
            {
                Logger.Error("❌ Parse error: " + ex.Message);
            }
        }
    }
}