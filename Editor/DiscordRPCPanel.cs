using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

public class DiscordRPCPanel : EditorWindow
{
    // === HIER DEINE DISCORD APPLICATION ID EINTRAGEN ===
    private const string clientId = "1490767097096048780"; 
    
    private static bool isEnabled = false;
    private static bool hideSceneName = false;
    private static string customStatus = "Building a VRChat World/Avatar";
    
    private static NamedPipeClientStream pipe;
    private static long startTime;

    [MenuItem("Tools/MrUnknownDE/Discord RPC")]
    public static void ShowWindow()
    {
        DiscordRPCPanel window = GetWindow<DiscordRPCPanel>("Discord RPC");
        window.minSize = new Vector2(300, 250);
    }

    private void OnEnable()
    {
        // Lädt gespeicherte Einstellungen
        isEnabled = EditorPrefs.GetBool("DiscordRPC_Enabled", false);
        hideSceneName = EditorPrefs.GetBool("DiscordRPC_HideScene", false);
        customStatus = EditorPrefs.GetString("DiscordRPC_Status", "Building a VRChat World");

        if (isEnabled) ConnectToDiscord();

        // Hooks in Unity einhängen
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private void OnDisable()
    {
        if (pipe != null && pipe.IsConnected) pipe.Close();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("DISCORD RICH PRESENCE", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUI.BeginChangeCheck();
        
        // Status Toggle
        GUI.backgroundColor = isEnabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
        if (GUILayout.Button(isEnabled ? "Status: ONLINE" : "Status: OFFLINE", GUILayout.Height(30)))
        {
            isEnabled = !isEnabled;
            EditorPrefs.SetBool("DiscordRPC_Enabled", isEnabled);
            
            if (isEnabled) ConnectToDiscord();
            else DisconnectDiscord();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(15);
        GUILayout.Label("SETTINGS", EditorStyles.boldLabel);

        // Custom Status Input
        GUILayout.Label("Custom Status (Line 1):");
        customStatus = EditorGUILayout.TextField(customStatus);
        
        // Privacy Toggle
        hideSceneName = EditorGUILayout.Toggle("Hide Scene Name (Privacy)", hideSceneName);

        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool("DiscordRPC_HideScene", hideSceneName);
            EditorPrefs.SetString("DiscordRPC_Status", customStatus);
            if (isEnabled) UpdatePresence();
        }

        GUILayout.Space(15);
        EditorGUILayout.HelpBox("Wenn aktiv, sieht dein Discord-Server in deinem Profil, an welcher Szene du gerade baust.", MessageType.Info);
    }

    // --- UNITY EVENT HOOKS ---
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (isEnabled) UpdatePresence();
    }

    private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        if (isEnabled) UpdatePresence();
    }

    // --- DISCORD IPC LOGIK ---
    private async void ConnectToDiscord()
    {
        if (pipe != null && pipe.IsConnected) return;

        try
        {
            pipe = new NamedPipeClientStream(".", "discord-ipc-0", PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(2000);

            // Handshake (Opcode 0)
            string handshake = "{\"v\": 1, \"client_id\": \"" + clientId + "\"}";
            SendFrame(0, handshake);

            startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            UpdatePresence();
            
            UnityEngine.Debug.Log("Discord RPC Connected!");
        }
        catch (Exception)
        {
            UnityEngine.Debug.LogWarning("Discord RPC: Konnte keine Verbindung zu Discord herstellen. Läuft Discord?");
            isEnabled = false;
        }
    }

    private void DisconnectDiscord()
    {
        if (pipe != null && pipe.IsConnected)
        {
            pipe.Close();
            pipe.Dispose();
        }
        pipe = null;
        UnityEngine.Debug.Log("Discord RPC Disconnected.");
    }

    private void UpdatePresence()
    {
        if (pipe == null || !pipe.IsConnected) return;

        string sceneName = hideSceneName ? "Secret Map" : EditorSceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName)) sceneName = "Unsaved Scene";

        string stateText = EditorApplication.isPlaying ? "Testet im Playmode" : $"Editiert: {sceneName}.unity";
        
        // Flieht die Strings für sicheres JSON
        string safeStatus = customStatus.Replace("\"", "\\\"");
        string safeState = stateText.Replace("\"", "\\\"");

        // Das Activity Payload (Opcode 1)
        string json = $@"{{
            ""cmd"": ""SET_ACTIVITY"",
            ""args"": {{
                ""pid"": {Process.GetCurrentProcess().Id},
                ""activity"": {{
                    ""details"": ""{safeStatus}"",
                    ""state"": ""{safeState}"",
                    ""timestamps"": {{
                        ""start"": {startTime}
                    }},
                    ""instance"": false
                }}
            }},
            ""nonce"": ""1""
        }}";

        SendFrame(1, json);
    }

    private void SendFrame(int opcode, string payload)
    {
        if (pipe == null || !pipe.IsConnected) return;

        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        int length = payloadBytes.Length;
        byte[] buffer = new byte[8 + length];
        
        BitConverter.GetBytes(opcode).CopyTo(buffer, 0);
        BitConverter.GetBytes(length).CopyTo(buffer, 4);
        payloadBytes.CopyTo(buffer, 8);
        
        try
        {
            pipe.Write(buffer, 0, buffer.Length);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Discord RPC Error: " + e.Message);
            DisconnectDiscord();
        }
    }
}