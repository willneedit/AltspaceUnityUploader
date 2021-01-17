# AltspaceUnityUploader
Fully rewritten Unity Uploader for AltspaceVR.

## Rationale

This is intended as an alternative for the official Unity uploader. For various reasons I won't list up here I decided to do a full, clean rewrite of the Uploader rather than messing with the code of the original one.

## Installation

While you can download prepackaged .unitypackages from Releases like https://github.com/willneedit/AltspaceUnityUploader/releases/tag/v0.1.0 (or any later release), you can also use GitHub's "Download .ZIP" feature to obtain a copy of the source and unpack it somewhere into the target project's "Asset" folder in its own directory. No compilation is needed.

Truly daring souls can directly use git to check out this repository into the Unity Project's "Asset" folder and stay abreast with the development of this tool. *A basic understanding on how git works is very much advised*.

## Usage

The Uploader integrates itself into Unity in two places:
 * The menu, with its own menu called "AUU" (shorthand for Altspace Unity Uploader)
 * The *context menu* of any in-scene item, with an additional menu item labeled **Convert to AltVR kit item...**
 
### The menu

The menu consists of two items:
 * Manage Login
 * Settings

In "Manage Login" you can log in into your Altspace account to manage your kits and templates, as in, load the list of items known to your account as well as create a new items.
When you selected or created one item, a short breakdown is listed in the window, like the presence and states of the assets uploaded for the specific item.

At the bottom you see a directory name into your Assets folder (for a kit) or a scene name, as well as maybe a remark **in boldface** on the current state of the directory and a suggestion on how to fix it. It could be....
 * Creating the kit directory under its suggested name
 * Renaming the scene

Only when certain conditions are met (directory for kit items exists, or scene is saved) you see a "Build" button, as well as when you're online and have an item selected, a "Build And Upload" button. Following the recommended steps (as mentioned above) will put you on the right track.

### Convert to AltVR kit item...

This reformats the in-scene item to the model/collider scheme needed for AltSpace and places it as a prefab into the directory designated for kit items, as defined in the "Manage Login" window. By default, this *removes* the item from the scene (same as the official uploader does), but this behavior can be switched off in the settings. When switched off, the converted item is actually *replaced* with an instance of the kit prefab item.

## Dials & Switches (the settings)

Most of the settings are straightforward and laced with tooltips for a short explanation of what they do.

 * The 'Normalize...' options for kit objects reset the position, rotation and scale values of the objects before they're converted. Try using these if you experience your kit object showing up with odd rotation or scaling when spawning it using the world editor or from an MRE.
 * 'Fix Environment Lighting' tries to set a reasonable default for your lighting based on the project settings, but it falls short when it tries to cope with a Skybox lighting setting and falls back to a uniform gray. In that case, try using a uniform color for your environment lighting or a gradient.
 
