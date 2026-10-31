using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace UnityEditorOnline.Editor.Network
{
    public static class NetworkUtils
    {
        private const int MaxPacketSize = 1024 * 1024 * 64;

        public static string GetRadminAddress()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) 
                    continue;

                foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) 
                        continue;

                    string ip = ua.Address.ToString();
                    if (ip.StartsWith("26.")) 
                        return ip;
                }
            }

            return null;
        }

        public static byte[] BuildFrame(byte[] payload)
        {
            byte[] frame = new byte[4 + payload.Length];
            BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length)).CopyTo(frame, 0);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            return frame;
        }

        public static byte[] ReadFrame(Stream stream)
        {
            var header = new byte[4];
            ReadExact(stream, header, 4);

            int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
            if (length < 0 || length > MaxPacketSize)
                throw new IOException($"Wrong packet size: {length}");

            var payload = new byte[length];
            ReadExact(stream, payload, length);
            return payload;
        }

        public static void ReadExact(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);

                if (read == 0)
                    throw new IOException("Closed connection");

                offset += read;
            }
        }

        public static void ConnectWithTimeout(TcpClient client, string ip, int port, int timeoutMs)
        {
            var task = client.ConnectAsync(ip, port);
            if (!task.Wait(timeoutMs))
            {
                client.Close();
                throw new TimeoutException($"Connection timeout ({timeoutMs / 1000} s)");
            }
        }

    }
}