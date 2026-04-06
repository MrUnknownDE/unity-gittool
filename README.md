# 🛠️ Unity Git Control Tool (VRChat Ready)

A lightweight, integrated Source Control Panel built directly into Unity. Designed to eliminate the constant context-switching between the Unity Editor and external command-line tools. Perfectly tailored for VRChat World Creators and developers who want to maintain clean version control without the bloat.

## ✨ Features
- **One-Click Init:** Initializes a new repository and automatically generates a clean Unity `.gitignore` file.
- **VS Code Style Interface:** Compact overview of modified, added, deleted, and untracked files.
- **Auto-Timestamp Commits:** If you don't provide a custom commit message, the tool gracefully falls back to a clean timestamp format.
- **Interactive File Explorer:** - `Single Click` on a file -> Pings and focuses the asset in the Unity Project View.
  - `Double Click` on a file -> Opens the built-in Code Diff Viewer right inside the Editor.
- **History View:** Browse your latest commits. Click any commit to open it directly in your remote web view (Gitea, GitHub, GitLab).

## 🚀 Installation via VRChat Creator Companion (VCC)

You can add this tool as a custom package directly into your VCC.

1. Open the VRChat Creator Companion.
2. Navigate to **Settings** -> **Packages**.
3. Click on **Add Repository**.
4. Enter your custom repo URL: `https://vcc.mrunk.de/index.json`
5. In your project views, under "Manage Project", the **VRChat Git Control Tool** will now appear. Simply click the plus icon to add it.

## 🛠️ Manual Installation
1. Download the latest version as a `.zip` archive.
2. Extract the folder.
3. Place the folder directly into your Unity project's `Packages` directory.
   *Alternative:* Copy the `.cs` files from the `Editor` folder into any `Editor` folder inside your `Assets` directory.

## 🕹️ Usage
Once installed, open the tool via the top menu bar in Unity:
`Tools` -> `Git-Tool`

A floating window will appear. You can easily dock this window into your custom layout (e.g., right next to the Inspector).

## ⚠️ Prerequisites
- **Git** must be installed on your system and added to your global environment variables (`PATH`).
- For automatic pushes to Gitea/GitHub to work seamlessly, you should have **SSH keys** or cached credentials configured. Unity cannot intercept terminal password prompts.