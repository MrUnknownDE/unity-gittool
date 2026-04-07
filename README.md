# 🛠️ MrUnknownDE VRChat Unity Tools

Welcome to the **MrUnknownDE VRChat Unity Tools** repository. This is a collection of customized, lightweight, and high-performance Unity Editor tools designed specifically to improve the workflow of VRChat World Creators. 

Instead of dealing with standalone applications or command-line interfaces, these tools bring essential DevOps and social features directly into your Unity Editor.

---

## 📦 Current Tools in this Package

### 1. Git Version Control System
A fully integrated Source Control Panel built directly into Unity. No more context-switching between the editor and external Git clients.
- **Smart Initialization:** Enter a Gitea/GitHub remote URL, and the tool will automatically handle the `init`, branch setup, and pull/merge existing server data before pushing your local project.
- **VS Code Style Interface:** Compact overview of modified, added, deleted, and untracked files.
- **Auto-Save Hook:** Pressing `CTRL+S` in Unity or changing focus automatically refreshes the Git status.
- **Timestamp Commits:** If you don't provide a custom commit message, the tool gracefully falls back to a clean timestamp format.
- **Interactive File Explorer:** Double-click any file to open the built-in Code Diff Viewer right inside the Editor.
- **Revert (Panic Button):** Easily discard all uncommitted changes if an experiment goes wrong.

### 2. Discord Rich Presence (RPC)
Let your community know what you are working on without saying a word.
- **Live Status:** Shows your current active Unity scene directly on your Discord profile.
- **Privacy Mode:** Hide the scene name if you are working on an unannounced or secret project.
- **Custom Status:** Add custom text (e.g., "Baking Lightmaps..." or "Writing Udon Scripts") to your Discord activity.

---

## 🚀 Installation

This tool is installed manually directly into your Unity project.

### Method 1: Unity Package Manager (Recommended)
1. Go to the [Releases page](../../releases/latest) of this repository.
2. Download the latest `de.mrunknownde.gittool-vX.X.X.zip` file.
3. Extract the ZIP file into a folder on your PC.
4. Open your Unity Project.
5. Go to `Window` -> `Package Manager`.
6. Click the **+** icon in the top left corner and select **Add package from disk...**.
7. Navigate to the extracted folder, select the `package.json` file, and click Open.

### Method 2: Direct Folder Drop
1. Download the latest `.zip` release.
2. Extract the archive.
3. Drag and drop the extracted folder directly into the `Packages` directory inside your Unity project's root folder (using Windows Explorer / File Explorer, not inside the Unity Editor window). Unity will automatically compile the tools.

---

## 🕹️ Usage

Once installed, you can access the tools via the top menu bar in Unity:

`Tools` -> `MrUnknownDE` -> `GIT Version Control`
`Tools` -> `MrUnknownDE` -> `Discord RPC`

The tools will open as floating windows. You can easily dock them into your custom Unity layout (e.g., right next to the Inspector or Console).

---

## ⚠️ Prerequisites & Troubleshooting

- **Git Installation:** You must have Git installed on your Windows machine. If the tool does not detect Git, it will provide a download link within the Unity UI.
- **Environment Variables:** If you just installed Git, **you must completely restart Unity Hub and the Unity Editor** so Windows can load the new `PATH` variables.
- **Authentication:** For automatic pushes to remote servers (Gitea/GitHub) to work seamlessly, ensure you have SSH keys or cached Git credentials configured on your system. Unity cannot intercept terminal password prompts.

---

*Built with ❤️ for the VRChat Community.*