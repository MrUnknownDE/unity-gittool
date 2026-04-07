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
    
    private string currentBranchName = "unknown";
    private string[] availableBranches = new string[0]; 
    private int selectedBranchIndex = 0; 

    private string[] changedFiles = new string[0];
    private Vector2 scrollPositionChanges;
    private Vector2 scrollPositionHistory;

    private int selectedTab = 0;
    private string[] tabNames = { "Changes", "History" };
    
    // NEU: Settings & Override
    private bool showSettings = false;
    private string webUrlOverride = "";
    private string prefsKey = "";
    
    private struct CommitInfo { public string hash; public string date; public string message; }
    private List<CommitInfo> commitHistory = new List<CommitInfo>();

    [MenuItem("Tools/MrUnknownDE/GIT Version Control")]
    public static void ShowWindow()
    {
        GitPanel window = GetWindow<GitPanel>("GIT Version Control System");
        window.minSize = new Vector2(350, 550);
    }

    private void OnEnable() 
    { 
        // Generiert einen einzigartigen Key für dieses spezifische Unity-Projekt
        prefsKey = $"GitTool_WebUrl_{Application.dataPath.GetHashCode()}";
        webUrlOverride = EditorPrefs.GetString(prefsKey, "");
        
        RefreshData(); 
    }
    
    private void OnFocus() { RefreshData(); }

    public void RefreshData()
    {
        CheckGitInstallation();
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

    private void SetDefaultCommitMessage() { commitMessage = $"Auto-Save: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}"; }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("GIT Version Control System", EditorStyles.boldLabel);
        if (hasRepo) GUILayout.Label($"Active Branch: {currentBranchName}", EditorStyles.miniLabel);
        GUILayout.Space(5);

        if (!isGitInstalled) { RenderGitMissingUI(); return; }
        if (!hasRepo) { RenderInitUI(); return; }

        // --- NEU: SETTINGS FOLDOUT ---
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
            EditorGUILayout.HelpBox("e.g. https://git.mrunk.de/mrunknownde/my-repo\nLeaves SSH untouched but fixes browser links.", MessageType.None);
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
        EditorGUILayout.HelpBox("CRITICAL: Git not found. Please install Git and restart Unity.", MessageType.Error);
        if (GUILayout.Button("Download Git for Windows", GUILayout.Height(30))) Application.OpenURL("https://git-scm.com/download/win");
    }

    private void RenderInitUI()
    {
        EditorGUILayout.HelpBox("No local Git repository found.", MessageType.Warning);
        remoteUrlInput = EditorGUILayout.TextField("Remote URL:", remoteUrlInput);
        if (GUILayout.Button("Initialize Repository", GUILayout.Height(30)))
        {
            RunGitCommand("init");
            RunGitCommand("branch -M main");
            if (!string.IsNullOrWhiteSpace(remoteUrlInput)) {
                RunGitCommand($"remote add origin \"{remoteUrlInput.Trim()}\"");
                RunGitCommand("pull origin main --allow-unrelated-histories --no-edit");
            }
            GenerateUnityGitIgnore();
            AssetDatabase.Refresh(); 
            RunGitCommand("add .gitignore");
            RunGitCommand("commit -m \"Initial commit (GitIgnore)\"");
            if (!string.IsNullOrWhiteSpace(remoteUrlInput)) RunGitCommand("push -u origin main");
            RefreshData();
        }
    }

    private void RenderGitUI()
    {
        // --- BRANCH MANAGEMENT ---
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Branch Management", EditorStyles.boldLabel);
        
        if (availableBranches.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Switch Branch:", selectedBranchIndex, availableBranches);
            if (EditorGUI.EndChangeCheck() && newIndex != selectedBranchIndex)
            {
                RunGitCommand($"checkout \"{availableBranches[newIndex]}\"");
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
                RunGitCommand($"checkout -b \"{newBranchName.Trim()}\"");
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

        // --- COMMIT BEREICH ---
        commitMessage = EditorGUILayout.TextField(commitMessage, GUILayout.Height(25));

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f); 
        if (GUILayout.Button("✓ Commit & Push", GUILayout.Height(30)))
        {
            if (string.IsNullOrWhiteSpace(commitMessage)) SetDefaultCommitMessage();
            RunGitCommand("add .");
            RunGitCommand($"commit -m \"{commitMessage}\"");
            RunGitCommand("push -u origin HEAD");
            commitMessage = ""; 
            RefreshData();
        }
        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f); 
        if (GUILayout.Button("⎌ Revert All", GUILayout.Width(80), GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Revert Changes?", "Discard ALL uncommitted changes?", "Yes", "Cancel")) {
                RunGitCommand("reset --hard HEAD"); RunGitCommand("clean -fd"); RefreshData();
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
        // NEU: Override Logik greift zuerst!
        if (!string.IsNullOrWhiteSpace(webUrlOverride))
        {
            string url = webUrlOverride;
            if (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);
            if (url.EndsWith(".git")) url = url.Substring(0, url.Length - 4);
            Application.OpenURL($"{url}/commit/{hash}");
            return;
        }

        // Standard Fallback Logik (wenn kein Override gesetzt ist)
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

    public static string RunGitCommand(string args) {
        try {
            ProcessStartInfo si = new ProcessStartInfo("git", args) { WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")), UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (Process p = Process.Start(si)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return o; }
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