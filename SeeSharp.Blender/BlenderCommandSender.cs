using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace SeeSharp.Blender
{
    public class BlenderCommandSender
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _connected = false;

        public async Task<bool> TryConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync("127.0.0.1", 5051);
                _stream = _client.GetStream();
                _connected = true;
                Console.WriteLine("✔ Connected to Blender cursor port");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Blender not listening (connection failed): " + ex.Message);
                _connected = false;
                return false;
            }
        }
        
        public async Task SendCursorAsync(float x, float y, float z)
        {
            // Ensure connection exists
            if (true)
            {
                bool ok = await TryConnectAsync();
                if (!ok)
                {
                    // Do NOT crash — just gracefully fail
                    Console.WriteLine("⚠ Could not send cursor update, Blender not running");
                    return;
                }
            }

            try
            {
                var data = new
                {
                    cursor_position = new[] { x, y, z }
                };

                string json = JsonSerializer.Serialize(data) + "\n";
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                await _stream.WriteAsync(bytes, 0, bytes.Length);
                await _stream.FlushAsync();

                Console.WriteLine("➡ Sent cursor update to Blender: " + json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error sending cursor data: " + ex.Message);

                // Force cleanup of dead connection
                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }

                _stream = null;
                _client = null;
                _connected = false;
            }
        }

        public async Task SendCommandAsync(object cmd)
        {
            if (!_connected || !IsSocketConnected())
            {
                _connected = false;

                Console.WriteLine("🔌 Lost connection — reconnecting…");
                bool ok = await TryConnectAsync();

                if (!ok)
                {
                    Console.WriteLine("⚠ Failed to reconnect");
                    return;
                }
            }
            try
            {
                string json = JsonSerializer.Serialize(cmd) + "\n";
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                await _stream.WriteAsync(bytes, 0, bytes.Length);
                await _stream.FlushAsync();

                Console.WriteLine("➡ Sent command: " + json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error sending command: " + ex.Message);
                _connected = false;
            }
        }
        private bool IsSocketConnected()
        {
            try
            {
                if (_client == null || !_client.Connected)
                    return false;

                var s = _client.Client;

                // Check if the socket has been closed
                bool readReady = s.Poll(0, SelectMode.SelectRead);
                bool noBytes = (s.Available == 0);

                if (readReady && noBytes)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}