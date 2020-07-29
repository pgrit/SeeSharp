using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using SeeSharp.Core.Shading;

namespace SeeSharp.Core.Image {
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

        public TevIpc(string ip = "127.0.0.1", int port = 14158) {
            try {
                client = new TcpClient("127.0.0.1", 14158);
                stream = client.GetStream();
            } catch(Exception) {
                System.Console.WriteLine("Warning: Could not connect to tev.");
                client = null;
            }
        }

        public void CreateImage(int width, int height, string name) {
            if (client == null) return;

            var packet = new CreateImagePacket {
                ImageName = name,
                GrabFocus = true,
                Width = width,
                Height = height,
                NumChannels = 3,
                ChannelNames = new string[] {"r", "g", "b"}
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void CloseImage(string name) {
            if (client == null) return;

            var packet = new CloseImagePacket {
                ImageName = name
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void OpenImage(string name) {
            if (client == null) return;

            var packet = new OpenImagePacket {
                GrabFocus = true,
                ImageName = name
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void ReloadImage(string name) {
            if (client == null) return;

            var packet = new ReloadImagePacket {
                GrabFocus = true,
                ImageName = name
            };
            var bytes = packet.IpcPacket;
            stream.Write(bytes, 0, bytes.Length);
        }

        public void UpdateImage(Image<ColorRGB> image, string name) {
            if (client == null) return;

            // How many rows to transmit at once. Set to be large enough, yet below tev's buffer size.
            int stride = 200000 / image.Width;

            var updatePacket = new UpdateImagePacket {
                ImageName = name,
                GrabFocus = true,
                Width = image.Width,
                ChannelName = "r",
                Data = new float[image.Width * stride]
            };

            for (int rowStart = 0; rowStart < image.Height; rowStart += stride) {
                updatePacket.Left = 0;
                updatePacket.Top = rowStart;
                updatePacket.Height = Math.Min(image.Height - rowStart, stride);

                updatePacket.ChannelName = "r";
                for (int row = rowStart; row < image.Height && row < rowStart + stride; row++) {
                    for (int col = 0; col < image.Width; col++) {
                        updatePacket.Data[(row - rowStart) * image.Width + col] = image[col, row].R;
                    }
                }
                var bytes = updatePacket.IpcPacket;
                stream.Write(bytes, 0, bytes.Length);

                updatePacket.ChannelName = "g";
                for (int row = rowStart; row < image.Height && row < rowStart + stride; row++) {
                    for (int col = 0; col < image.Width; col++) {
                        updatePacket.Data[(row - rowStart) * image.Width + col] = image[col, row].G;
                    }
                }
                bytes = updatePacket.IpcPacket;
                stream.Write(bytes, 0, bytes.Length);

                updatePacket.ChannelName = "b";
                for (int row = rowStart; row < image.Height && row < rowStart + stride; row++) {
                    for (int col = 0; col < image.Width; col++) {
                        updatePacket.Data[(row - rowStart) * image.Width + col] = image[col, row].B;
                    }
                }
                bytes = updatePacket.IpcPacket;
                stream.Write(bytes, 0, bytes.Length);
            }
        }
    }
}