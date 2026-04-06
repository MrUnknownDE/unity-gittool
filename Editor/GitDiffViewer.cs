using UnityEditor;
using UnityEngine;

public class GitDiffViewer : EditorWindow
{
    private string filePath;
    private string diffContent;
    private Vector2 scrollPos;

    private readonly Color colorAddBg = new Color(0.18f, 0.35f, 0.18f, 0.6f);
    private readonly Color colorRemoveBg = new Color(0.4f, 0.15f, 0.15f, 0.6f);
    private readonly Color colorHeaderBg = new Color(0.2f, 0.3f, 0.5f, 0.5f);

    public static void ShowWindow(string path, string status)
    {
        GitDiffViewer window = GetWindow<GitDiffViewer>("Diff Viewer");
        window.minSize = new Vector2(700, 500);
        window.LoadDiff(path, status);
    }

    private void LoadDiff(string path, string status)
    {
        filePath = path;

        if (status.Contains("??"))
        {
            diffContent = $"--- NEW FILE (Untracked) ---\n\n{System.IO.File.ReadAllText(path)}";
        }
        else
        {
            diffContent = GitPanel.RunGitCommand($"diff HEAD -- \"{path}\"");
            if (string.IsNullOrWhiteSpace(diffContent))
            {
                diffContent = "No text-based changes found (possibly a binary file or empty diff).";
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label($"  File Diff: {filePath}", EditorStyles.largeLabel);
        GUILayout.Space(10);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, "box");

        GUIStyle lineStyle = new GUIStyle(EditorStyles.label);
        lineStyle.richText = true;
        lineStyle.wordWrap = false;
        lineStyle.fontSize = 12;

        string[] lines = diffContent.Split(new[] { '\n' });
        
        foreach (string line in lines)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 18);
            
            if (line.StartsWith("+") && !line.StartsWith("+++"))
            {
                EditorGUI.DrawRect(rect, colorAddBg);
            }
            else if (line.StartsWith("-") && !line.StartsWith("---"))
            {
                EditorGUI.DrawRect(rect, colorRemoveBg);
            }
            else if (line.StartsWith("@@"))
            {
                EditorGUI.DrawRect(rect, colorHeaderBg);
            }

            string displayLine = line.Replace("\t", "    ");
            GUI.Label(rect, $"<color=#E0E0E0>{displayLine}</color>", lineStyle);
        }

        EditorGUILayout.EndScrollView();
    }
}