using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using SimpleImageIO;

namespace SeeSharp.Image {
    struct CreateImagePacket {
        private const byte Type = 4;

        public bool GrabFocus;
        public string ImageName;
        public int Width, Height;
        public int NumChannels;
        public string[] ChannelNames;

        public byte[] IpcPacket {
            get {
                var bytes = new List<byte>();

                bytes.Add(Type);
                bytes.Add(GrabFocus ? (byte)1 : (byte)0);
                bytes.AddRange(Encoding.ASCII.GetBytes(ImageName));
                bytes.Add(0); // string should be zero terminated
                bytes.AddRange(BitConverter.GetBytes(Width));
                bytes.AddRange(BitConverter.GetBytes(Height));
                bytes.AddRange(BitConverter.GetBytes(NumChannels));
                foreach (var n in ChannelNames) {
                    bytes.AddRange(Encoding.ASCII.GetBytes(n));
                    bytes.Add(0); // string should be zero terminated
                }

                // Compute the size and write as bytes
                int size = bytes.Count + 4;
                bytes.InsertRange(0, BitConverter.GetBytes(size));

                return bytes.ToArray();
            }
        }
    };

    struct UpdateImagePacket {
        private const byte Type = 3;

        public bool GrabFocus;
        public string ImageName;
        public string ChannelName;
        public int Left, Top;
        public int Width, Height;
        public float[] Data;

        public byte[] IpcPacket {
            get {
                var bytes = new List<byte>(Width * Height * 4 + 100);

                bytes.Add(Type);
                bytes.Add(GrabFocus ? (byte)1 : (byte)0);
                bytes.AddRange(Encoding.ASCII.GetBytes(ImageName));
                bytes.Add(0); // string should be zero terminated
                bytes.AddRange(Encoding.ASCII.GetBytes(ChannelName));
                bytes.Add(0); // string should be zero terminated
                bytes.AddRange(BitConverter.GetBytes(Left));
                bytes.AddRange(BitConverter.GetBytes(Top));
                bytes.AddRange(BitConverter.GetBytes(Width));
                bytes.AddRange(BitConverter.GetBytes(Height));

                var byteArray = new byte[Data.Length * 4];
                Buffer.BlockCopy(Data, 0, byteArray, 0, byteArray.Length);
                bytes.AddRange(byteArray);

                // Compute the size and write as bytes
                int size = bytes.Count + 4;
                bytes.InsertRange(0, BitConverter.GetBytes(size));

                return bytes.ToArray();
            }
        }
    }

    struct CloseImagePacket {
        private const byte Type = 2;

        public string ImageName;

        public byte[] IpcPacket {
            get {
                var bytes = new List<byte>(ImageName.Length + 10);

                bytes.Add(Type);
                bytes.AddRange(Encoding.ASCII.GetBytes(ImageName));
                bytes.Add(0); // string should be zero terminated

                // Compute the size and write as bytes
                int size = bytes.Count + 4;
                bytes.InsertRange(0, BitConverter.GetBytes(size));

                return bytes.ToArray();
            }
        }
    }

    struct OpenImagePacket {
        private const byte Type = 0;

        public bool GrabFocus;
        public string ImageName;

        public byte[] IpcPacket {
            get {
                var bytes = new List<byte>(ImageName.Length + 10);

                bytes.Add(Type);
                bytes.Add(GrabFocus ? (byte)1 : (byte)0);
                bytes.AddRange(Encoding.ASCII.GetBytes(ImageName));
                bytes.Add(0); // string should be zero terminated

                // Compute the size and write as bytes
                int size = bytes.Count + 4;
                bytes.InsertRange(0, BitConverter.GetBytes(size));

                return bytes.ToArray();
            }
        }
    }

    struct ReloadImagePacket {
        private const byte Type = 1;

        public bool GrabFocus;
        public string ImageName;

        public byte[] IpcPacket {
            get {
                var bytes = new List<byte>(ImageName.Length + 10);

                bytes.Add(Type);
                bytes.Add(GrabFocus ? (byte)1 : (byte)0);
                bytes.AddRange(Encoding.ASCII.GetBytes(ImageName));
                bytes.Add(0); // string should be zero terminated

                // Compute the size and write as bytes
                int size = bytes.Count + 4;
                bytes.InsertRange(0, BitConverter.GetBytes(size));

                return bytes.ToArray();
            }
        }
    }

    public class TevIpc {
        TcpClient client;
        NetworkStream stream;
        Dictionary<string, (string name, ImageBase image)[]> syncedImages = new();

        public TevIpc(string ip = "127.0.0.1", int port = 14158) {
            try {
                client = new TcpClient("127.0.0.1", 14158);
                stream = client.GetStream();
            } catch(Exception) {
                System.Console.WriteLine("Warning: Could not connect to tev.");
                client = null;
            }
        }

        public void CreateImageSync(string name, int width, int height, params (string, ImageBase)[] layers) {
            if (client == null) return;

            Debug.Assert(!syncedImages.ContainsKey(name));
            syncedImages[name] = layers;

            // Count channels and generate layer names
            int numChannels = 0;
            List<string> channelNames = new();
            foreach (var (layerName, image) in layers) {
                Debug.Assert(image.NumChannels == 1 || image.NumChannels == 3 || image.NumChannels == 4);
                Debug.Assert(image.Width == width && image.Height == height);

                numChannels += image.NumChannels;
                if (image.NumChannels == 1) {
                    channelNames.Add(layerName + ".Y");
                } else {
                    channelNames.Add(layerName + ".R");
                    channelNames.Add(layerName + ".G");
                    channelNames.Add(layerName + ".B");
                }
                if (image.NumChannels == 4)
                    channelNames.Add(layerName + ".A");
            }

            var packet = new CreateImagePacket {
                ImageName = name,
                GrabFocus = false,
                Width = width,
                Height = height,
                NumChannels = numChannels,
                ChannelNames = channelNames.ToArray()
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void CloseImage(string name) {
            if (client == null) return;

            if (syncedImages.ContainsKey(name))
                syncedImages.Remove(name);

            var packet = new CloseImagePacket {
                ImageName = name
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void OpenImage(string filename) {
            if (client == null) return;

            var packet = new OpenImagePacket {
                GrabFocus = false,
                ImageName = filename
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void ReloadImage(string filename) {
            if (client == null) return;

            var packet = new ReloadImagePacket {
                GrabFocus = false,
                ImageName = filename
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void UpdateImage(string name) {
            if (client == null) return;
            var layers = syncedImages[name];

            // How many rows to transmit at once. Set to be large enough, yet below tev's buffer size.
            int stride = 200000 / layers[0].image.Width;

            var updatePacket = new UpdateImagePacket {
                ImageName = name,
                GrabFocus = false,
                Width = layers[0].image.Width,
                Data = new float[layers[0].image.Width * stride]
            };

            for (int rowStart = 0; rowStart < layers[0].image.Height; rowStart += stride) {
                updatePacket.Left = 0;
                updatePacket.Top = rowStart;
                updatePacket.Height = Math.Min(layers[0].image.Height - rowStart, stride);

                void SendPacket(ImageBase image, int channel) {
                    for (int row = rowStart; row < image.Height && row < rowStart + stride; row++) {
                        for (int col = 0; col < image.Width; col++) {
                            updatePacket.Data[(row - rowStart) * image.Width + col] =
                                image.GetPixelChannel(col, row, channel);
                        }
                    }
                    var bytes = updatePacket.IpcPacket;
                    stream.Write(bytes, 0, bytes.Length);
                }

                foreach (var layer in layers) {
                    if (layer.image.NumChannels == 1) {
                        updatePacket.ChannelName = layer.name + ".Y";
                        SendPacket(layer.image, 0);
                    } else {
                        updatePacket.ChannelName = layer.name + ".R";
                        SendPacket(layer.image, 0);
                        updatePacket.ChannelName = layer.name + ".G";
                        SendPacket(layer.image, 1);
                        updatePacket.ChannelName = layer.name + ".B";
                        SendPacket(layer.image, 2);
                    }
                    if (layer.image.NumChannels == 4) {
                        updatePacket.ChannelName = layer.name + ".A";
                        SendPacket(layer.image, 3);
                    }
                }
            }
        }
    }
}