# Local icon assets

The build reads the icon from your installed Codex desktop app and generates
`chatgpt.ico` and `launcher.ico` here. These files are ignored by Git and are
not distributed as repository assets.

To use an icon you supply yourself, run:

```powershell
.\scripts\make-icon.ps1 -IconPath 'C:\path\to\your-icon.ico'
.\scripts\build.ps1
```

OpenAI names, logos and icons belong to OpenAI. Their use is subject to
[OpenAI's brand guidelines](https://openai.com/brand/), separately from the
source-code license.
