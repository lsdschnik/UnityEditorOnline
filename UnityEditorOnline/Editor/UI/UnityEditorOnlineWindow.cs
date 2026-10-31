using System.Collections.Generic;
using UnityEditor;
using UnityEditorOnline.Editor.Network;
using UnityEditorOnline.Editor.Sync.Selection;
using UnityEngine;

namespace UnityEditorOnline.Editor.UI
{
    public class UnityEditorOnlineWindow : EditorWindow
    {
        private const string NickKey = "UnityEditorOnline.Nickname";

        [SerializeField] private bool _isHost;
        [SerializeField] private string _ipAddress = "127.0.0.1";
        [SerializeField] private int _port = 7777;
        [SerializeField] private Color _color;
        [SerializeField] private string _nickname = string.Empty;

        private readonly NetworkSession _session = new NetworkSession();

        [MenuItem("UnityEditorOnline/Create session")]
        public static void ShowHostWindow()
        {
            var window = GetWindow<UnityEditorOnlineWindow>("UnityEditorOnline");
            if (!window._session.IsRunning) 
                window._isHost = true;
        }

        [MenuItem("UnityEditorOnline/Connect to session")]
        public static void ShowClientWindow()
        {
            var window = GetWindow<UnityEditorOnlineWindow>("UnityEditorOnline");
            if (!window._session.IsRunning) 
                window._isHost = false;
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_nickname))
                _nickname = EditorPrefs.GetString(NickKey, string.Empty); 

            if (_color.a <= 0f) 
                _color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f); 
        }

        private void OnDisable() => _session.Stop();
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            GUILayout.Label("TCP connection via Radmin VPN", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (_session.IsRunning) 
                DrawRunning();
            else 
                DrawSetup();
        }
        private void DrawSetup()
        {
            if (_isHost == false)
                _ipAddress = EditorGUILayout.TextField("Radmin IP:", _ipAddress);

            _port = EditorGUILayout.IntField("Port:", _port);
            GUILayout.Space(10);

            _nickname = EditorGUILayout.TextField("Nickname:", _nickname);
            _color = EditorGUILayout.ColorField(new GUIContent("Color:"), _color, true, false, false);
            GUILayout.Space(10);

            if (_isHost == true)
            {
                if (GUILayout.Button("Create session", GUILayout.Height(30)))
                {
                    ApplyIdentity();
                    _session.StartHost(_port);
                }
            }
            else
            {
                if (GUILayout.Button("Connect", GUILayout.Height(30)))
                {
                    bool ok = EditorUtility.DisplayDialog("UnityEditorOnline", "Connecting will replace synchronized objects (SyncId).\n\nSave or hide them to prevent deletion. Continue?", "Connect", "Cancel");

                    if (ok)
                    {
                        ApplyIdentity();
                        _session.StartClient(_ipAddress, _port);
                    }
                }
            }
        }

        private void ApplyIdentity()
        {
            string nick = _nickname.Trim();
            if (nick.Length == 0) 
                nick = "Mapper-" + UnityEngine.Random.Range(1000, 10000);

            _nickname = nick;
            _color.a = 1f;
            EditorPrefs.SetString(NickKey, nick);

            SelectionSync.LocalUserId = nick;
            SelectionSync.LocalColor = _color;
        }

        private void DrawRunning()
        {
            string status = _session.IsHost
                            ? $"You are Host (port {_session.Port}). Connected mappers: {_session.PeerCount}"
                            : _session.PeerCount > 0
                                ? $"Connected to host {_session.RemoteAddress}:{_session.Port}"
                                : $"Connecting to {_session.RemoteAddress}:{_session.Port}...";
            EditorGUILayout.HelpBox(status, UnityEditor.MessageType.Info);

            GUILayout.Space(10);

            if (_session.IsHost) DrawHostAddresses();

            if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
                _session.Stop();
        }
        private void DrawHostAddresses()
        {
            GUILayout.Label($"Address for mappers (port {_session.Port}):", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_session.HostAddresses))
            {
                EditorGUILayout.HelpBox(
                    "Radmin VPN address not found. Check that Radmin VPN is running and connected to the network.",
                    UnityEditor.MessageType.Warning);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(_session.HostAddresses, GUILayout.Height(18));
                    if (GUILayout.Button("Copy", GUILayout.Width(90)))
                        EditorGUIUtility.systemCopyBuffer = _session.HostAddresses;
                }
            }

            GUILayout.Space(10);
        }
    }
}