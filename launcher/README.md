# ClipVault launcher

A tiny native `ClipVault.exe` that sits at the root of the distributed bundle and starts the real
app under `app\`. It exists only to give the extracted folder one obvious entry point instead of a
wall of DLLs:

```text
ClipVault/
├─ ClipVault.exe      # this launcher
├─ README.txt
└─ app/
   └─ ClipVault.App.exe  # the actual application
```

It resolves its own directory, runs `app\ClipVault.App.exe` (forwarding any arguments, with `app\`
as the working directory), and exits. The app is a tray-resident single instance, so the launcher
does not wait on it.

## Why a separate native exe

It is outside the .NET solution (`ClipVault.slnx`) on purpose: it carries no managed runtime, stays
~50 KB, and never participates in `dotnet restore/build`. Self-contained .NET keeps its host
(`coreclr.dll`, `hostfxr.dll`, …) next to the app exe, so the app itself cannot be relocated under
`app\` without a stub at the root — this is that stub.

## Build

```powershell
pwsh launcher/build.ps1 -OutputPath artifacts/launcher/ClipVault.exe -Version 1.2.3
```

Requires the MSVC "Desktop development with C++" workload (located via `vswhere`). CI builds it on
every release; `just publish` builds it when MSVC is present and skips it otherwise. The version
stamps the exe's version resource; the icon and a side-by-side manifest are embedded from
`ClipVault.Launcher.rc`.
