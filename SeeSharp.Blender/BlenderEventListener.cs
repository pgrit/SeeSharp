using System.Net.Sockets;
using System.Net;
using System.Text.Json;
namespace SeeSharp.Blender
{
    public class BlenderEventListener
{
    private TcpListener? _listener;
    private BlenderEventDispatcher _dispatcher;

    public BlenderEventListener(BlenderEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void RegisterDispatcher(BlenderEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Loopback, 5052);
        _listener.Start();
        Console.WriteLine("📡 Listening for Blender events on 5052...");

        // while (true)
        // {
        //     var client = await _listener.AcceptTcpClientAsync();
        //     _ = HandleClientAsync(client);
        // }
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("🔌 Blender connected to Blazor Event Listener");

                _ = HandleClientAsync(client);
            }
        });
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var reader = new StreamReader(client.GetStream());

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            Console.WriteLine("📨 Received event: " + line);
            try
            {
                using var doc = JsonDocument.Parse(line);
                _dispatcher.Dispatch(doc.RootElement);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Parse error: " + ex.Message);
            }
        }
    }
}

}