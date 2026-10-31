using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditorOnline.Editor.Sync;
using UnityEditorOnline.Editor.Sync.Camera;
using UnityEditorOnline.Editor.Sync.Receiving;
using UnityEditorOnline.Editor.Sync.Selection;
using UnityEngine;

namespace UnityEditorOnline.Editor.Network
{
    public class NetworkSession
    {
        private class Peer
        {
            public TcpClient Client;
            public NetworkStream Stream;
            public string Nickname;
            public Color Color;
            public readonly object WriteLock = new object();
        }

        public bool IsRunning => _running;
        public bool IsHost => _host;
        public int Port { get; private set; }
        public string RemoteAddress { get; private set; }
        public string HostAddresses { get; private set; }

        public int PeerCount
        {
            get
            {
                lock (_peers)
                {
                    return _peers.Count;
                }
            }
        }

        private volatile bool _running;
        private volatile bool _host;

        private TcpListener _server;
        private readonly List<Peer> _peers = new List<Peer>();

        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public void StartHost(int port)
        {
            if (_running)
                return;

            try
            {
                _server = new TcpListener(IPAddress.Any, port);
                _server.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityEditorOnline] Host starting error: {e.Message}");
                _server = null;
                return;
            }

            Port = port;
            HostAddresses = NetworkUtils.GetRadminAddress();
            _host = true;
            _running = true;

            EditorApplication.update += Pump;

            SyncTracker.Start(Send);

            TcpListener server = _server;
            new Thread(() => AcceptLoop(server)) { IsBackground = true, Name = "UnityEditorOnline Accept" }.Start();

            Debug.Log($"[UnityEditorOnline] Host started on the port {port}");
        }

        public void StartClient(string ip, int port)
        {
            if (_running)
                return;

            Port = port;
            RemoteAddress = ip;
            _host = false;
            _running = true;

            EditorApplication.update += Pump;

            new Thread(() => Connect(ip, port)) { IsBackground = true, Name = "UnityEditorOnline Connect" }.Start();
        }

        private void AcceptLoop(TcpListener server)
        {
            while (_running)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    client.NoDelay = true;
                    AddPeer(client);
                }
                catch (Exception e)
                {
                    if (_running)
                        Post(() => Debug.LogWarning($"[UnityEditorOnline] Accept connection error: {e.Message}"));
                    break;
                }
            }
        }

        private void Connect(string ip, int port)
        {
            try
            {
                var client = new TcpClient { NoDelay = true };
                NetworkUtils.ConnectWithTimeout(client, ip, port, 5000);
                AddPeer(client);

                Post(() =>
                {
                    Debug.Log($"[UnityEditorOnline] Connected to {ip}:{port}");
                    SyncTracker.Start(Send);
                });
            }
            catch (Exception e)
            {
                Post(() =>
                {
                    Debug.LogError($"[UnityEditorOnline] Failed to connect: {e}");
                    Stop();
                });
            }
        }

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            _host = false;

            EditorApplication.update -= Pump;

            SyncTracker.Stop();

            try { _server?.Stop(); } catch { }

            _server = null;

            Peer[] all;
            lock (_peers)
            {
                all = _peers.ToArray();
                _peers.Clear();
            }

            foreach (Peer peer in all)
                Close(peer);

            while (_queue.TryDequeue(out _)) { }
        }

        private void AddPeer(TcpClient client)
        {
            if (!_running)
            {
                client.Close();
                return;
            }

            var peer = new Peer { Client = client, Stream = client.GetStream() };
            lock (_peers) _peers.Add(peer);

            new Thread(() => ReadLoop(peer)) { IsBackground = true, Name = "UnityEditorOnline Read" }.Start();

            if (_host)
            {
                Post(() =>
                {
                    foreach (string json in SnapshotBuilder.BuildFullSnapshot())
                        SendTo(peer, json);

                    foreach (string json in SelectionSync.BuildSnapshotPackets())
                        SendTo(peer, json);

                    foreach (string json in CameraSync.BuildSnapshotPackets())
                        SendTo(peer, json);

                    SelectionSync.MarkDirty();
                    CameraSync.MarkDirty();
                });
            }
        }

        private void RemovePeer(Peer peer)
        {
            lock (_peers)
            {
                if (!_peers.Remove(peer)) 
                    return;
            }
            Close(peer);

            Post(() =>
            {
                if (_host)
                {
                    Debug.Log($"[UnityEditorOnline] Mapper {Tag(peer)} disconnected");

                    if (peer.Nickname != null)
                    {
                        SelectionSync.RemoveUser(peer.Nickname);
                        Send(SelectionSync.BuildLeavePacket(peer.Nickname));

                        CameraSync.RemoveUser(peer.Nickname);
                        Send(CameraSync.BuildLeavePacket(peer.Nickname));
                    }
                }
                else
                {
                    Debug.Log("[UnityEditorOnline] Connection to the host was closed");
                    Stop();
                }
            });
        }

        private static string Tag(Peer peer)
        {
            if (peer.Nickname == null)
                return "Mapper";

            string nick = peer.Nickname.Replace("<", "").Replace(">", "");
            if (nick.Length > 24) nick = nick.Substring(0, 24);

            return $"<color=#{ColorUtility.ToHtmlStringRGB(peer.Color)}>{nick}</color>";
        }
        private static void Close(Peer peer)
        {
            try { peer.Stream.Close(); } catch { }
            try { peer.Client.Close(); } catch { }
        }

        private void ReadLoop(Peer peer)
        {
            try
            {
                while (_running)
                {
                    byte[] payload = NetworkUtils.ReadFrame(peer.Stream);

                    if (_host)
                        Broadcast(payload, peer);

                    string message = Encoding.UTF8.GetString(payload);
                    Post(() =>
                    {
                        if (_host && peer.Nickname == null &&
                            SelectionSync.TryGetIdentity(message, out string id, out Color color))
                        {
                            peer.Nickname = id;
                            peer.Color = color;
                            Debug.Log($"[UnityEditorOnline] Mapper {Tag(peer)} connected");
                        }

                        SyncReceiver.Handle(message);
                    });
                }
            }
            catch (Exception e)
            {
                if (_running) 
                    Post(() => Debug.LogWarning($"[UnityEditorOnline] Disconnected from {Tag(peer)}: {e.Message}"));
            }
            finally
            {
                RemovePeer(peer);
            }
        }

        private void Send(string json)
        {
            if (_running)
                Broadcast(Encoding.UTF8.GetBytes(json), null);
        }

        private void SendTo(Peer peer, string json)
        {
            WriteFrame(peer, NetworkUtils.BuildFrame(Encoding.UTF8.GetBytes(json)));
        }

        private void Broadcast(byte[] payload, Peer except)
        {
            byte[] frame = NetworkUtils.BuildFrame(payload);

            Peer[] all;
            lock (_peers) all = _peers.ToArray();

            foreach (Peer peer in all)
            {
                if (peer != except)
                    WriteFrame(peer, frame);
            }
        }

        private void WriteFrame(Peer peer, byte[] frame)
        {
            try
            {
                lock (peer.WriteLock)
                    peer.Stream.Write(frame, 0, frame.Length);
            }
            catch (Exception e)
            {
                Post(() => Debug.LogWarning($"[UnityEditorOnline] Failed to send to {Tag(peer)}: {e.Message}"));
                RemovePeer(peer);
            }
        }

        private void Post(Action action)
        {
            if (_running)
                _queue.Enqueue(action);
        }

        private void Pump()
        {
            while (_queue.TryDequeue(out Action action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}