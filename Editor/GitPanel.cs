using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

public class GitPanel : EditorWindow
{
    private string commitMessage = "";
    private string remoteUrlInput = ""; 
    private string newBranchName = ""; 
    
    private bool isGitInstalled = true;
    private bool hasRepo = false;
    
    private bool settingsCorrect = true;
    private string settingsWarning = "";

    private string currentBranchName = "unknown";
    private string[] availableBranches = new string[0]; 
    private int selectedBranchIndex = 0; 

    private string[] changedFiles = new string[0];
    private Vector2 scrollPositionChanges;
    private Vector2 scrollPositionHistory;
    
    // STATISCH: Damit RunGitCommand darauf zugreifen kann und das Log beim Neuladen erhalten bleibt!
    private static string gitLogOutput = "";
    private Vector2 scrollPositionLog;

    private int selectedTab = 0;
    private string[] tabNames = { "Changes", "History" };
    
    private bool showSettings = false;
    private string webUrlOverride = "";
    private string prefsKey = "";
    
    private struct CommitInfo { public string hash; public string date; public string message; }
    private List<CommitInfo> commitHistory = new List<CommitInfo>();

    [MenuItem("Tools/MrUnknownDE/GIT Version Control")]
    public static void ShowWindow()
    {
        GitPanel window = GetWindow<GitPanel>("GIT Version Control System");
        window.minSize = new Vector2(380, 650); 
    }

    private void OnEnable() 
    { 
        prefsKey = $"GitTool_WebUrl_{Application.dataPath.GetHashCode()}";
        webUrlOverride = EditorPrefs.GetString(prefsKey, "");
        RefreshData(); 
    }
    
    private void OnFocus() { RefreshData(); }

    public void RefreshData()
    {
        CheckGitInstallation();
        CheckUnitySettings(); 

        if (!isGitInstalled) return;
        CheckRepoStatus();
        
        if (hasRepo) 
        {
            currentBranchName = RunGitCommand("rev-parse --abbrev-ref HEAD").Trim();
            FetchBranches();
        }
        
        if (string.IsNullOrWhiteSpace(commitMessage) || commitMessage.StartsWith("Auto-Save:")) 
        {
            SetDefaultCommitMessage();
        }
        
        Repaint(); 
    }

    private void CheckGitInstallation()
    {
        try {
            ProcessStartInfo startInfo = new ProcessStartInfo("git", "--version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (Process p = Process.Start(startInfo)) { p.WaitForExit(); isGitInstalled = true; }
        } catch { isGitInstalled = false; }
    }

    // FIX: Nutzt jetzt die aktuelle Unity-API (VersionControlSettings.mode)
    private void CheckUnitySettings()
    {
        settingsCorrect = true;
        settingsWarning = "";

        if (VersionControlSettings.mode != "Visible Meta Files")
        {
            settingsCorrect = false;
            settingsWarning += "• Version Control Mode must be 'Visible Meta Files'\n";
        }

        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            settingsCorrect = false;
            settingsWarning += "• Asset Serialization must be 'Force Text'\n";
        }
    }

    private void FixUnitySettings()
    {
        VersionControlSettings.mode = "Visible Meta Files";
        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEngine.Debug.Log("Git-Tool: Unity Project Settings updated for Git compatibility.");
        RefreshData();
    }

    private void SetDefaultCommitMessage() { commitMessage = $"Auto-Save: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}"; }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("GIT Version Control System", EditorStyles.boldLabel);
        if (hasRepo) GUILayout.Label($"Active Branch: {currentBranchName}", EditorStyles.miniLabel);
        GUILayout.Space(5);

        if (!settingsCorrect)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox("INCOMPATIBLE PROJECT SETTINGS:\n" + settingsWarning, MessageType.Error);
            GUI.backgroundColor = new Color(1f, 0.5f, 0f);
            if (GUILayout.Button("Fix Project Settings Now"))
            {
                FixUnitySettings();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        if (!isGitInstalled) { RenderGitMissingUI(); return; }
        if (!hasRepo) { RenderInitUI(); return; }

        showSettings = EditorGUILayout.Foldout(showSettings, "⚙️ Repository Settings");
        if (showSettings)
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Web Override (For custom SSH instances)", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            webUrlOverride = EditorGUILayout.TextField("Web URL:", webUrlOverride);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(prefsKey, webUrlOverride.Trim());
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(25));
        GUILayout.Space(10);

        if (selectedTab == 0) RenderGitUI();
        else RenderHistoryUI();
    }

    private void RenderGitMissingUI()
    {
        EditorGUILayout.HelpBox("CRITICAL: Git not found.", MessageType.Error);
        if (GUILayout.Button("Download Git for Windows", GUILayout.Height(30))) Application.OpenURL("https://git-scm.com/download/win");
    }

    private void RenderInitUI()
    {
        EditorGUILayout.HelpBox("No local Git repository found.", MessageType.Warning);
        remoteUrlInput = EditorGUILayout.TextField("Remote URL:", remoteUrlInput);
        if (GUILayout.Button("Initialize Repository", GUILayout.Height(30)))
        {
            RunGitCommand("init", true);
            RunGitCommand("branch -M main", true);
            if (!string.IsNullOrWhiteSpace(remoteUrlInput)) {
                RunGitCommand($"remote add origin \"{remoteUrlInput.Trim()}\"", true);
                RunGitCommand("pull origin main --allow-unrelated-histories --no-edit", true);
            }
            GenerateUnityGitIgnore();
            AssetDatabase.Refresh(); 
            RunGitCommand("add .gitignore", true);
            RunGitCommand("commit -m \"Initial commit (GitIgnore)\"", true);
            if (!string.IsNullOrWhiteSpace(remoteUrlInput)) RunGitCommand("push -u origin main", true);
            RefreshData();
        }
    }

    private void RenderGitUI()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Branch Management", EditorStyles.boldLabel);
        
        if (availableBranches.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Switch Branch:", selectedBranchIndex, availableBranches);
            if (EditorGUI.EndChangeCheck() && newIndex != selectedBranchIndex)
            {
                RunGitCommand($"checkout \"{availableBranches[newIndex]}\"", true);
                AssetDatabase.Refresh(); 
                RefreshData();
                return; 
            }
        }

        EditorGUILayout.BeginHorizontal();
        newBranchName = EditorGUILayout.TextField("New Branch:", newBranchName);
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f); 
        if (GUILayout.Button("+ Create", GUILayout.Width(80)))
        {
            if (!string.IsNullOrWhiteSpace(newBranchName))
            {
                RunGitCommand($"checkout -b \"{newBranchName.Trim()}\"", true);
                newBranchName = "";
                RefreshData();
                GUI.FocusControl(null); 
                return;
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        commitMessage = EditorGUILayout.TextField(commitMessage, GUILayout.Height(25));

        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f); 
        if (GUILayout.Button("✓ Push", GUILayout.Height(30)))
        {
            UnityEngine.Debug.Log("Git-Tool: Saving Scenes and Assets before push...");
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            if (string.IsNullOrWhiteSpace(commitMessage)) SetDefaultCommitMessage();
            RunGitCommand("add .", true);
            RunGitCommand($"commit -m \"{commitMessage}\"", true);
            
            string pushResult = RunGitCommand("push -u origin HEAD", true);
            if (pushResult.Contains("rejected") || pushResult.Contains("fetch first"))
            {
                UnityEngine.Debug.LogError("Git-Tool: PUSH REJECTED! Jemand anderes hat Änderungen hochgeladen. Bitte klicke zuerst auf 'Pull'.");
            }
            else
            {
                UnityEngine.Debug.Log("Git-Tool: Changes successfully pushed!");
                commitMessage = ""; 
            }
            RefreshData();
        }

        GUI.backgroundColor = new Color(0.8f, 0.6f, 0.2f); 
        if (GUILayout.Button("⬇️ Pull", GUILayout.Width(80), GUILayout.Height(30)))
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            string pullResult = RunGitCommand("pull", true);
            AssetDatabase.Refresh(); 

            if (pullResult.Contains("CONFLICT"))
            {
                UnityEngine.Debug.LogError("Git-Tool: MERGE CONFLICT! Bitte in VS Code auflösen!");
                EditorUtility.DisplayDialog("Merge Conflict", "Es gibt Konflikte mit den Server-Daten!\n\nGit konnte die Änderungen nicht automatisch zusammenführen. Bitte öffne die roten Dateien in deinem Code-Editor und löse den Konflikt manuell auf.", "OK");
            }
            RefreshData();
        }

        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f); 
        if (GUILayout.Button("⎌ Revert", GUILayout.Width(80), GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Revert Changes?", "Discard ALL uncommitted changes?", "Yes", "Cancel")) {
                RunGitCommand("reset --hard HEAD", true); 
                RunGitCommand("clean -fd", true); 
                AssetDatabase.Refresh(); 
                RefreshData();
            }
        }
        GUI.backgroundColor = Color.white; 
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"CHANGES ({changedFiles.Length})", EditorStyles.boldLabel);
        if (GUILayout.Button("↻", GUILayout.Width(25))) RefreshData();
        EditorGUILayout.EndHorizontal();

        scrollPositionChanges = EditorGUILayout.BeginScrollView(scrollPositionChanges, "box");
        if (changedFiles.Length == 0) GUILayout.Label("No changes.");
        else RenderFileList(changedFiles);
        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("GIT CONSOLE", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) gitLogOutput = "";
        EditorGUILayout.EndHorizontal();

        scrollPositionLog = EditorGUILayout.BeginScrollView(scrollPositionLog, "box", GUILayout.Height(120));
        GUIStyle logStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 10 };
        GUILayout.Label(string.IsNullOrEmpty(gitLogOutput) ? "Ready." : gitLogOutput, logStyle);
        EditorGUILayout.EndScrollView();
    }

    private void RenderHistoryUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("LAST COMMITS", EditorStyles.boldLabel);
        if (GUILayout.Button("↻", GUILayout.Width(25))) FetchHistory();
        EditorGUILayout.EndHorizontal();

        scrollPositionHistory = EditorGUILayout.BeginScrollView(scrollPositionHistory, "box");
        foreach (var commit in commitHistory) {
            Rect rect = EditorGUILayout.GetControlRect(false, 22);
            if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.1f));
            GUI.Label(rect, $"<b>{commit.hash}</b> | {commit.date} | {commit.message}", new GUIStyle(EditorStyles.label){richText=true});
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) {
                OpenCommitInBrowser(commit.hash);
                Event.current.Use();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void RenderFileList(string[] files)
    {
        foreach (string line in files) {
            if (line.Length < 4) continue;
            string status = line.Substring(0, 2).Trim();
            string path = line.Substring(3).Trim().Replace("\"", "");
            Rect rect = EditorGUILayout.GetControlRect(false, 18);
            if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.1f));
            GUI.Label(rect, $"[{status}] {path}");
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) {
                if (Event.current.clickCount == 1) PingAsset(path);
                else if (Event.current.clickCount == 2) GitDiffViewer.ShowWindow(path, status);
                Event.current.Use();
            }
        }
    }

    private void OpenCommitInBrowser(string hash)
    {
        if (!string.IsNullOrWhiteSpace(webUrlOverride))
        {
            string url = webUrlOverride;
            if (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);
            if (url.EndsWith(".git")) url = url.Substring(0, url.Length - 4);
            Application.OpenURL($"{url}/commit/{hash}");
            return;
        }

        string remoteUrl = RunGitCommand("config --get remote.origin.url").Trim();
        if (string.IsNullOrEmpty(remoteUrl)) return;

        if (remoteUrl.StartsWith("git@") || remoteUrl.StartsWith("ssh://")) {
            remoteUrl = remoteUrl.Replace("ssh://", "");
            remoteUrl = remoteUrl.Replace("git@", "https://");
            int firstColon = remoteUrl.IndexOf(':', 8); 
            if (firstColon != -1) remoteUrl = remoteUrl.Remove(firstColon, 1).Insert(firstColon, "/");
        }
        if (remoteUrl.EndsWith(".git")) remoteUrl = remoteUrl.Substring(0, remoteUrl.Length - 4);
        Application.OpenURL($"{remoteUrl}/commit/{hash}");
    }

    private void CheckRepoStatus()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        hasRepo = Directory.Exists(Path.Combine(projectPath, ".git"));
        if (hasRepo) {
            string output = RunGitCommand("status -s");
            changedFiles = string.IsNullOrWhiteSpace(output) ? new string[0] : output.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            FetchHistory();
        }
    }

    private void FetchBranches()
    {
        string output = RunGitCommand("branch --format=\"%(refname:short)\"");
        if (!string.IsNullOrWhiteSpace(output))
        {
            availableBranches = output.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            selectedBranchIndex = System.Array.IndexOf(availableBranches, currentBranchName);
            if (selectedBranchIndex == -1) selectedBranchIndex = 0;
        }
    }

    private void FetchHistory()
    {
        commitHistory.Clear();
        string output = RunGitCommand("log -n 25 --pretty=format:\"%h|%cd|%s\" --date=short");
        if (!string.IsNullOrWhiteSpace(output)) {
            foreach (string line in output.Split('\n')) {
                string[] p = line.Split('|');
                if (p.Length >= 3) commitHistory.Add(new CommitInfo { hash = p[0], date = p[1], message = p[2] });
            }
        }
    }

    private void PingAsset(string path) {
        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (obj) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
    }

    private void GenerateUnityGitIgnore() {
        string path = Path.Combine(Application.dataPath, "../.gitignore");
        if (!File.Exists(path)) File.WriteAllText(path, ".idea\n.vs\nbin\nobj\n/Library\n/Temp\n/UserSettings\n/Configs\n/*.csproj\n/*.sln\n/Logs\n/Packages/*\n!/Packages/manifest.json\n!/Packages/packages-lock.json\n~UnityDirMonSyncFile~*");
    }

    // FIX: Methode ist wieder static!
    public static string RunGitCommand(string args, bool logAction = false) {
        try {
            ProcessStartInfo si = new ProcessStartInfo("git", args) { 
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")), 
                UseShellExecute = false, 
                RedirectStandardOutput = true, 
                RedirectStandardError = true, 
                CreateNoWindow = true 
            };
            
            using (Process p = Process.Start(si)) { 
                string o = p.StandardOutput.ReadToEnd(); 
                string e = p.StandardError.ReadToEnd(); 
                p.WaitForExit(); 
                string result = o + (string.IsNullOrWhiteSpace(e) ? "" : "\n" + e);
                
                if (logAction) {
                    string time = System.DateTime.Now.ToString("HH:mm:ss");
                    string entry = $"[{time}] > git {args}\n";
                    if (!string.IsNullOrWhiteSpace(result)) entry += result.Trim() + "\n\n";
                    
                    gitLogOutput = entry + gitLogOutput; 
                    if (gitLogOutput.Length > 10000) gitLogOutput = gitLogOutput.Substring(0, 10000); 
                }
                
                return result; 
            }
        } catch { return ""; }
    }
}

public class GitSaveListener : UnityEditor.AssetModificationProcessor
{
    public static string[] OnWillSaveAssets(string[] paths) {
        EditorApplication.delayCall += () => { if (EditorWindow.HasOpenInstances<GitPanel>()) EditorWindow.GetWindow<GitPanel>("GIT Version Control System").RefreshData(); };
        return paths;
    }
}