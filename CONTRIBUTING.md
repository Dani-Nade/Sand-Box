# How we work

## Jira → branch → pull request

1. Pick a ticket on the Jira board and move it to **In Progress**.
2. Pull the latest `main`, then create a branch named after the ticket:
   - `GAME-12-player-jump`
   - `GAME-30-fix-camera-jitter`
3. Commit often. Put the ticket key in the commit message so it shows up in Jira:
   - `GAME-12 Add jump with coyote time`
4. Push and open a **Pull Request** into `main`.
5. The other person gives it a quick look (pull the branch and press Play if it's gameplay).
6. Merge, delete the branch, move the ticket to **Done**.

**Never commit directly to `main`.**

## Unity rules (avoid merge hell)

- **One person per scene at a time.** Say "I'm in `Level1`" in Discord before editing a scene.
- **Build things as prefabs.** Edit the prefab, not the copy in the scene. Prefabs rarely conflict.
- **Always commit `.meta` files** together with the asset they belong to. Moving or renaming assets must be done *inside Unity*, never in File Explorer.
- **Don't commit** the `Library/`, `Temp/`, `Logs/` or `UserSettings/` folders (already in `.gitignore`).
- Pull before you start working, push when you stop.

## Folder structure

```
Assets/
  _Project/
    Art/
      Models/       # .fbx exported from Blender
      Textures/
      Materials/
      Animations/
      Sprites/
    Audio/
      Music/
      SFX/
    Prefabs/
    Scenes/
    Scripts/
    UI/
  ThirdParty/       # Asset Store / downloaded packages
Source/
  Blender/          # .blend source files (outside Assets/ so Unity doesn't import them)
```

Keeping our stuff under `Assets/_Project/` separates it from imported packages.

## Art pipeline (Blender → Unity)

- Keep `.blend` source files in `Source/Blender/`.
- Export to `.fbx` into `Assets/_Project/Art/Models/`.
- Blender FBX export settings: **Apply Scalings: FBX All**, **Forward: -Z Forward**, **Up: Y Up**, tick **Apply Transform**.
- Apply scale and rotation in Blender before exporting (**Ctrl+A → All Transforms**).
- 1 Blender unit = 1 meter = 1 Unity unit.
- Name things clearly: `SM_Crate_01` (static mesh), `SK_Player` (skinned/rigged), `T_Crate_Albedo` (texture), `M_Crate` (material).

## Git LFS

Large files (`.blend`, `.fbx`, `.png`, `.wav`, ...) go through Git LFS automatically via `.gitattributes`. GitHub's free LFS quota is limited, so avoid committing huge test files and delete unused assets.
