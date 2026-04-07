using UnityEditor;
using UnityEngine;
using System;
using System.Text;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;

public class LiveSyncPanel : EditorWindow
{
    private string wsUrl = "ws://localhost:8080";
    private static ClientWebSocket ws;
    private static bool isConnected = false;
    private static CancellationTokenSource cts;

    private Transform lastSelected;
    private bool isNetworkApplying = false;

    [Serializable]
    private class SyncMessage
    {
        public string type; 
        public string objectName;
        public Vector3 position;
        public Vector3 eulerAngles;
        public Vector3 localScale;
    }

    [MenuItem("Tools/MrUnknownDE/Live Sync Bridge")]
    public static void ShowWindow()
    {
        GetWindow<LiveSyncPanel>("Live Sync Bridge").minSize = new Vector2(320, 350);
    }

    private void OnEnable()
    {
        wsUrl = EditorPrefs.GetString("LiveSync_WS_URL", "ws://localhost:8080");
        EditorApplication.update += EditorUpdate;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnGUI()
    {
        GUILayout.Space(5);

        // --- 🚧 DICK & FETT: WIP WARNING 🚧 ---
        EditorGUILayout.HelpBox(
            "🚧 WORK IN PROGRESS 🚧\n\n" +
            "This feature is highly experimental and in active development!\n" +
            "Expect bugs, network desyncs, or unexpected behavior.\n\n" +
            "ALWAYS backup your project before starting a Live Session!", 
            MessageType.Warning);
        
        GUILayout.Space(10);
        GUILayout.Label("REAL-TIME MULTI-USER SYNC", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUI.BeginChangeCheck();
        wsUrl = EditorGUILayout.TextField("WebSocket Server:", wsUrl);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString("LiveSync_WS_URL", wsUrl);
        }

        GUILayout.Space(15);

        if (!isConnected)
        {
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
            if (GUILayout.Button("🔌 Connect Session", GUILayout.Height(40))) Connect();
        }
        else
        {
            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button("🛑 Disconnect", GUILayout.Height(40))) Disconnect();

            GUILayout.Space(15);
            EditorGUILayout.HelpBox("🟢 Connected!\nTransforms are tracked in real-time.\nListening for Git updates...", MessageType.Info);
        }
        GUI.backgroundColor = Color.white;
    }

    private async void Connect()
    {
        if (ws != null && ws.State == WebSocketState.Open) return;

        ws = new ClientWebSocket();
        cts = new CancellationTokenSource();

        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), cts.Token);
            isConnected = true;
            UnityEngine.Debug.Log("Live Sync: Connected to Server!");
            
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Live Sync: Connection failed. Is the server running? " + e.Message);
        }
    }

    private async void Disconnect()
    {
        if (ws != null)
        {
            cts?.Cancel();
            if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
            ws.Dispose();
        }
        isConnected = false;
        UnityEngine.Debug.Log("Live Sync: Disconnected.");
    }

    private void EditorUpdate()
    {
        if (!isConnected || isNetworkApplying) return;

        if (lastSelected != null && lastSelected.hasChanged)
        {
            SyncMessage msg = new SyncMessage
            {
                type = "TRANSFORM",
                objectName = lastSelected.name,
                position = lastSelected.position,
                eulerAngles = lastSelected.eulerAngles,
                localScale = lastSelected.localScale
            };

            SendMessage(JsonUtility.ToJson(msg));
            lastSelected.hasChanged = false; 
        }
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeTransform != null)
        {
            lastSelected = Selection.activeTransform;
            lastSelected.hasChanged = false; 
        }
    }

    public static void BroadcastGitUpdate()
    {
        if (!isConnected || ws == null || ws.State != WebSocketState.Open) return;

        SyncMessage msg = new SyncMessage { type = "GIT_PULL" };
        SendMessage(JsonUtility.ToJson(msg));
        UnityEngine.Debug.Log("Live Sync: Broadcasted Git Update signal to team.");
    }

    private static async void SendMessage(string json)
    {
        if (ws == null || ws.State != WebSocketState.Open) return;
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
    }

    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[2048];
        while (ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    EditorApplication.delayCall += () => ProcessIncoming(json);
                }
            }
            catch { break; }
        }
        isConnected = false;
    }

    private void ProcessIncoming(string json)
    {
        try
        {
            SyncMessage msg = JsonUtility.FromJson<SyncMessage>(json);

            if (msg.type == "TRANSFORM")
            {
                GameObject target = GameObject.Find(msg.objectName);
                if (target != null)
                {
                    isNetworkApplying = true; 
                    target.transform.position = msg.position;
                    target.transform.eulerAngles = msg.eulerAngles;
                    target.transform.localScale = msg.localScale;
                    target.transform.hasChanged = false;
                    isNetworkApplying = false;
                }
            }
            else if (msg.type == "GIT_PULL")
            {
                UnityEngine.Debug.LogWarning("Live Sync: Teammate pushed new files! Starting auto-pull...");
                GitPanel.RunGitCommand("pull --rebase origin HEAD");
                AssetDatabase.Refresh();
                UnityEngine.Debug.Log("Live Sync: Auto-pull complete. Files updated.");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Live Sync: Failed to parse incoming message. " + e.Message);
        }
    }
}