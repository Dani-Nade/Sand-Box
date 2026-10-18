# Sand House

A two-person indie game built in Unity.

## Play the roguelike prototype

Open `Assets/_Project/Scenes/RoguelikeRun.unity` and press Play (or **Esc > Start New Run** from the arena). Clear three combat levels, choosing one of three random gun upgrades after each clear. Kills no longer buy upgrades. [Run details and controls](docs/RoguelikeRun.md).

## Requirements

| Tool | Version | Notes |
|---|---|---|
| Unity | **6000.6.5f1** (exact) | Install via Unity Hub with *Windows Build Support*. Everyone must use the same version. |
| Git + Git LFS | any recent | Git LFS is bundled with Git for Windows and GitHub Desktop. |
| GitHub Desktop | latest | Or use git from the command line. |
| VS Code | latest | Install the **Unity** extension by Microsoft. |
| Blender | latest | For 3D assets. |

> ⚠️ Do **not** keep the project in a OneDrive/Dropbox/Google Drive synced folder. It slows Unity down and can corrupt the repo. Use something like `C:\Users\<you>\Projects\sand-house`.

## First-time setup

1. Clone the repo with GitHub Desktop (**File → Clone repository**).
2. Enable Git LFS (once per machine):
   ```bash
   git lfs install
   ```
3. Enable Unity Smart Merge for scenes and prefabs (once per clone, run inside the repo folder):
   ```bash
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   git config merge.unityyamlmerge.recursive binary
   ```
4. In Unity Hub: **Add → Add project from disk** and choose the Unity project folder in this repo.
5. In Unity: **Edit → Preferences → External Tools → External Script Editor** → choose **Visual Studio Code**.

## How we work

See [CONTRIBUTING.md](CONTRIBUTING.md).
