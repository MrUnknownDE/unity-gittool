using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

public class GitPanel : EditorWindow
{
    private string commitMessage = "";
    private string remoteUrlInput = ""; // NEU: Speicher für die Remote-URL
    private bool hasRepo = false;
    private string[] changedFiles = new string[0];
    private Vector2 scrollPositionChanges;
    private Vector2 scrollPositionHistory;

    private int selectedTab = 0;
    private string[] tabNames = { "Changes", "History" };
    
    private struct CommitInfo { public string hash; public string date; public string message; }
    private List<CommitInfo> commitHistory = new List<CommitInfo>();

    [MenuItem("Tools/MrUnknownDE/Git-Tool")]
    public static void ShowWindow()
    {
        GitPanel window = GetWindow<GitPanel>("Source Control");
        window.minSize = new Vector2(350, 500);
    }

    private void OnEnable()
    {
        RefreshData();
    }

    private void OnFocus()
    {
        RefreshData();
    }

    public void RefreshData()
    {
        CheckRepoStatus();
        
        if (string.IsNullOrWhiteSpace(commitMessage) || commitMessage.StartsWith("Auto-Save:"))
        {
            SetDefaultCommitMessage();
        }
        
        Repaint(); 
    }

    private void SetDefaultCommitMessage()
    {
        commitMessage = $"Auto-Save: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("SOURCE CONTROL", EditorStyles.boldLabel);
        GUILayout.Space(5);

        if (!hasRepo)
        {
            RenderInitUI();
            return;
        }

        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(25));
        GUILayout.Space(10);

        if (selectedTab == 0) RenderGitUI();
        else RenderHistoryUI();
    }

    private void RenderInitUI()
    {
        EditorGUILayout.HelpBox("No local Git repository found. Initialize current project folder?", MessageType.Warning);
        
        GUILayout.Space(10);
        GUILayout.Label("Remote Repository URL (Optional):", EditorStyles.boldLabel);
        remoteUrlInput = EditorGUILayout.TextField(remoteUrlInput, GUILayout.Height(25));
        EditorGUILayout.HelpBox("e.g., https://github.com/user/repo.git or git@gitea.domain.com:user/repo.git", MessageType.Info);
        GUILayout.Space(10);

        if (GUILayout.Button("Initialize Repository", GUILayout.Height(30)))
        {
            RunGitCommand("init");
            
            // Branch direkt sauber auf "main" setzen (verhindert Konflikte mit alten "master" Defaults)
            RunGitCommand("branch -M main");

            // Remote URL hinzufügen, falls angegeben
            if (!string.IsNullOrWhiteSpace(remoteUrlInput))
            {
                RunGitCommand($"remote add origin \"{remoteUrlInput.Trim()}\"");
            }

            GenerateUnityGitIgnore();
            RunGitCommand("add .gitignore");
            RunGitCommand("commit -m \"Initial commit (GitIgnore)\"");

            // Initialen Push ausführen, wenn ein Remote existiert
            if (!string.IsNullOrWhiteSpace(remoteUrlInput))
            {
                RunGitCommand("push -u origin main");
                UnityEngine.Debug.Log("Git-Tool: Repository initialized and pushed to remote!");
            }
            else
            {
                UnityEngine.Debug.Log("Git-Tool: Local repository initialized successfully.");
            }

            RefreshData();
        }
    }

    private void RenderGitUI()
    {
        commitMessage = EditorGUILayout.TextField(commitMessage, GUILayout.Height(25));

        GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f); 
        if (GUILayout.Button("✓ Commit & Push", GUILayout.Height(30)))
        {
            if (string.IsNullOrWhiteSpace(commitMessage)) commitMessage = $"Auto-Save: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            RunGitCommand("add .");
            RunGitCommand($"commit -m \"{commitMessage}\"");
            RunGitCommand("push");
            
            commitMessage = ""; 
            RefreshData();  
            UnityEngine.Debug.Log("Git-Tool: Changes successfully pushed!");
        }
        GUI.backgroundColor = Color.white; 

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Legend: [M] Modified | [A] Added | [D] Deleted | [??] Untracked", MessageType.Info);
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"CHANGES ({changedFiles.Length})", EditorStyles.boldLabel);
        if (GUILayout.Button("↻", GUILayout.Width(25))) 
        {
            RefreshData();
        }
        EditorGUILayout.EndHorizontal();

        scrollPositionChanges = EditorGUILayout.BeginScrollView(scrollPositionChanges, "box");
        
        if (changedFiles.Length == 0) GUILayout.Label("No unsaved changes.");
        else RenderFileList(changedFiles);
        
        EditorGUILayout.EndScrollView();
    }

    private void RenderHistoryUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("LAST COMMITS (Click to open in Browser)", EditorStyles.boldLabel);
        if (GUILayout.Button("↻", GUILayout.Width(25))) FetchHistory();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        if (commitHistory.Count == 0)
        {
            GUILayout.Label("No commits found.");
            return;
        }

        scrollPositionHistory = EditorGUILayout.BeginScrollView(scrollPositionHistory, "box");

        foreach (var commit in commitHistory)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 22);
            
            if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.1f));
            }
            
            GUIStyle textStyle = new GUIStyle(EditorStyles.label) { richText = true };
            GUI.Label(rect, $"<b>{commit.hash}</b> | {commit.date} | {commit.message}", textStyle);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                OpenCommitInBrowser(commit.hash);
                e.Use();
            }
            GUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RenderFileList(string[] files)
    {
        foreach (string line in files)
        {
            if (line.Length < 4) continue;

            string status = line.Substring(0, 1).Trim() == "" ? line.Substring(0, 2) : line.Substring(0, 1); 
            string path = line.Substring(line.IndexOf('\t') + 1 > 0 ? line.IndexOf('\t') + 1 : 3).Trim();
            if (path.StartsWith("\"") && path.EndsWith("\"")) path = path.Substring(1, path.Length - 2);

            Rect rect = EditorGUILayout.GetControlRect(false, 18);
            if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.1f));

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            GUI.Label(rect, $"[{status.Trim()}] {path}", labelStyle);

            Event e = Event.current;
            if (e.isMouse && e.button == 0 && rect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
            {
                if (e.clickCount == 1) PingAsset(path);
                else if (e.clickCount == 2) GitDiffViewer.ShowWindow(path, status);
                e.Use();
            }
        }
    }

    private void OpenCommitInBrowser(string hash)
    {
        string remoteUrl = RunGitCommand("config --get remote.origin.url").Trim();
        if (string.IsNullOrEmpty(remoteUrl))
        {
            UnityEngine.Debug.LogWarning("Git-Tool: No remote repository configured (origin missing).");
            return;
        }

        string webUrl = remoteUrl;
        
        if (webUrl.StartsWith("git@"))
        {
            webUrl = webUrl.Replace(":", "/").Replace("git@", "https://");
        }
        
        if (webUrl.EndsWith(".git"))
        {
            webUrl = webUrl.Substring(0, webUrl.Length - 4);
        }

        Application.OpenURL($"{webUrl}/commit/{hash}");
    }

    private void CheckRepoStatus()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        hasRepo = Directory.Exists(Path.Combine(projectPath, ".git"));

        if (hasRepo)
        {
            string output = RunGitCommand("status -s");
            changedFiles = string.IsNullOrWhiteSpace(output) ? new string[0] : output.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            FetchHistory();
        }
    }

    private void FetchHistory()
    {
        commitHistory.Clear();
        string output = RunGitCommand("log -n 25 --pretty=format:\"%h|%cd|%s\" --date=short");
        if (!string.IsNullOrWhiteSpace(output))
        {
            string[] lines = output.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    commitHistory.Add(new CommitInfo { hash = parts[0], date = parts[1], message = parts[2] });
                }
            }
        }
    }

    private void PingAsset(string relativePath)
    {
        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
        if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
    }

    private void GenerateUnityGitIgnore()
    {
        string path = Path.Combine(Application.dataPath, "../.gitignore");
        if (!File.Exists(path))
        {
            string content = @".idea
.vs
bin
obj
*.sln.DotSettings.user
/Library
/Temp
/UserSettings
/Configs
/*.csproj
/*.sln
/Logs
/Packages/*
!/Packages/manifest.json
!/Packages/packages-lock.json
!/Packages/vpm-manifest.json
~UnityDirMonSyncFile~*";
            File.WriteAllText(path, content);
            UnityEngine.Debug.Log(".gitignore generated successfully!");
        }
    }

    public static string RunGitCommand(string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            using (Process p = Process.Start(startInfo)) { p.WaitForExit(); return p.StandardOutput.ReadToEnd(); }
        }
        catch { return ""; }
    }
}

public class GitSaveListener : UnityEditor.AssetModificationProcessor
{
    public static string[] OnWillSaveAssets(string[] paths)
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorWindow.HasOpenInstances<GitPanel>())
            {
                EditorWindow.GetWindow<GitPanel>().RefreshData();
            }
        };
        
        return paths; 
    }
}