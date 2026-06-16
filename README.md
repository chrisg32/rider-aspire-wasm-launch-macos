# Rider .NET Aspire fails to launch a *hosted* Blazor WebAssembly web project on macOS (Process ID 0)

## Title (for YouTrack)
> Rider .NET Aspire: hosted Blazor WebAssembly web project orchestrated by an Aspire AppHost never starts on macOS — resource shows "Running (Unhealthy)" / Process ID 0, while the identical AppHost launched from the CLI works.

## Summary
On macOS, launching an Aspire AppHost **from Rider** does not spawn the dotnet process for an orchestrated **hosted Blazor WebAssembly** web project (a web project that references a `.Client` WASM project). The Aspire dashboard shows the resource as **Running (Unhealthy)** with **Process ID: 0**, its HTTP health check fails with `System.Threading.Tasks.TaskCanceledException`, and the endpoint times out — there is a DCP proxy listening but no application process behind it. Launching the **same** AppHost from the command line (`dotnet run --project <AppHost>`) starts the resource and serves correctly, and an otherwise-identical **Blazor Server** (non-WASM) project launches fine from Rider. This isolates the failure to Rider's IDE Aspire launch path for the hosted-WASM project shape.

---

## Environment (verified on this machine)

| Item | Value |
|---|---|
| OS | macOS 26.5.1 (build 25F80), Apple Silicon (arm64) |
| .NET SDK | 9.0.305 |
| Target framework | net9.0 |
| Aspire AppHost SDK / Hosting | **13.4.4** (`Aspire.AppHost.Sdk/13.4.4`, `Aspire.Hosting.AppHost` 13.4.4) — confirmed at runtime: `Aspire AppHost version: 13.4.4+ccc566c5ab3285c9beb8f38ede34734bb477c029` |
| Aspire CLI (tool) | 9.5.0 |
| DCP orchestration | `aspire.hosting.orchestration.osx-arm64` 13.4.4 |
| Rider | **2026.1.3** (build 261.25134.178) |
| Rider .NET Aspire plugin | **2.5.1** |

> Previously also observed on Rider stable 2026.1.2 (Aspire plugin 2.5.3) and EAP 2026.2 (plugin 2.7.2) against the same Aspire 13.4.4 stack. Confirm/adjust the plugin version on your machine in Rider → Settings → Plugins.

---

## What's in this package — the two-solution contrast

Two minimal Aspire solutions, built to be **as identical as possible except for the one meaningful difference**.

```
rider-aspire-wasm-launch-macos/
├── Working-AspireBlazorServer/        <-- launches fine from Rider
│   ├── Working-AspireBlazorServer.sln
│   ├── ServerApp.AppHost/             (Aspire AppHost, SDK 13.4.4)
│   └── ServerApp.Web/                 (Blazor Web App, interactivity = Server, NO WASM client)
│
└── Broken-AspireBlazorWasm/           <-- fails to launch from Rider (Process ID 0)
    ├── Broken-AspireBlazorWasm.sln
    ├── WasmApp.AppHost/               (Aspire AppHost, SDK 13.4.4 — identical to above)
    ├── WasmApp.Web/                   (Blazor Web App, interactivity = Auto, references the .Client)
    └── WasmApp.Web.Client/            (the Blazor WebAssembly client — the hosted-WASM piece)
```

**Identical between the two:** .NET 9 / net9.0; `Aspire.AppHost.Sdk/13.4.4` + `Aspire.Hosting.AppHost` 13.4.4; the AppHost adds a single project resource named `webfrontend` and attaches an HTTP health check on `/`; both web projects are stock `dotnet new blazor` output; no containers, no databases, no extra dependencies.

**The one difference (the trigger):**

| | Working (`ServerApp.Web`) | Broken (`WasmApp.Web`) |
|---|---|---|
| Blazor interactivity | Server | Auto (Server + WebAssembly) |
| References a `.Client` WASM project | **No** | **Yes** (`WasmApp.Web.Client`) |
| Extra package | — | `Microsoft.AspNetCore.Components.WebAssembly.Server` |

So the entire delta is: **the broken web project is a *hosted* WASM app (it references the Blazor WebAssembly client project); the working one is not.** That single reference is what flips Rider onto its WASM-aware launch path.

---

## Repro steps (Rider GUI — MANUAL, to be performed by the reporter)

These steps must be run in the Rider GUI; they cannot be automated from the CLI. The CLI-only portion is verified below and proves the apps themselves are fine.

1. Open `Broken-AspireBlazorWasm/Broken-AspireBlazorWasm.sln` in Rider on macOS.
2. Let Rider restore and index. Select the `WasmApp.AppHost` run configuration (the Aspire host).
3. Click **Run** (not Debug).
4. The Aspire dashboard opens. Watch the `webfrontend` resource.

**Expected:** `webfrontend` reaches **Running (Healthy)**, gets a real Process ID, logs Kestrel `Now listening on …`, and the endpoint serves the Blazor page (HTTP 200).

**Actual:** `webfrontend` shows **Running (Unhealthy)** with **Process ID: 0**. Its console shows only Aspire's dependency-wait gate ("Waiting for resource … / Finished waiting …") and then **no application startup logs** (no Kestrel "Now listening"). The health check fails with `System.Threading.Tasks.TaskCanceledException` thrown from `HealthChecks.Uris.UriHealthCheck`. Navigating to the endpoint times out — DCP's proxy is listening but nothing is upstream because no process was ever spawned.

**Control (to confirm it's the WASM shape, not your machine):** open `Working-AspireBlazorServer/Working-AspireBlazorServer.sln` and Run the `ServerApp.AppHost` configuration the same way. The `webfrontend` resource starts normally and serves. The only thing that changed between the two is the hosted-WASM `.Client` reference.

---

## Evidence

### Symptom (Rider, manual — for the reporter to capture)
- Dashboard: `webfrontend` = **Running (Unhealthy)**, **Process ID: 0**.
- Health check exception: `System.Threading.Tasks.TaskCanceledException` from `HealthChecks.Uris.UriHealthCheck` (the endpoint has no process behind the DCP proxy, so the probe never completes).
- Resource console: only the Aspire dependency-wait gate lines, then **no Kestrel `Now listening on …`** — i.e. the application never started.
- Browser: navigating to the resource endpoint times out.

Please attach: a screenshot of the dashboard showing Process ID 0 / Running (Unhealthy), and the `webfrontend` resource console log.

### CLI contrast (VERIFIED on this machine — both samples)
Both AppHosts were run from the terminal with `dotnet run --project <AppHost>` (DCP's `--no-launch-profile` launcher) and both orchestrated web resources served correctly.

**Working (Blazor Server):** AppHost reported `Aspire AppHost version: 13.4.4`. The child `ServerApp.Web` process spawned and Kestrel served:
```
HTTP/2 200
content-type: text/html; charset=utf-8
server: Kestrel
...<title>Home</title>...<a class="navbar-brand">ServerApp.Web</a>
```

**Broken (hosted Blazor WASM) — works from CLI:** AppHost reported `Aspire AppHost version: 13.4.4`. The child `WasmApp.Web` process spawned and Kestrel served HTTP 200, including the WASM bootstrap markers that confirm it is a genuine hosted-WASM app:
```
HTTP 200
...<title>Home</title>...<a class="navbar-brand">WasmApp.Web</a>
<script src="_framework/blazor.web.js"></script>
<!--Blazor-WebAssembly-Component-State: ... -->
```

**Conclusion:** the hosted-WASM app builds and serves identically to the Server app when the AppHost is launched from the CLI. The process-never-spawns failure appears only on Rider's IDE Aspire launch path for the hosted-WASM project on macOS.

---

## Suspected mechanism
When the orchestrated web project references a Blazor WebAssembly `.Client` (a *hosted* WASM setup), Rider's Aspire plugin appears to switch from its normal project-session launcher to a **WASM-aware launcher** (class name observed in earlier diagnostics: `WasmHostProjectSessionProcessLauncher`). On macOS that path silently fails to spawn the project's dotnet process: DCP still creates the resource and its proxy endpoint, so the dashboard shows the resource as present but unhealthy with **Process ID 0**, and the URI health check cancels because nothing is listening upstream. The CLI launch (which does not use Rider's launcher) is unaffected, which is why `dotnet run` works.

---

## Related public issues (researched; none match this exact bug)
- microsoft/aspire #1418 — "Can't debug Blazor WebAssembly project launched via AppHost." **Closed**, 2023, VS2022. About **debugging** WASM under AppHost, not the process failing to **launch** on Rider/macOS. https://github.com/dotnet/aspire/issues/1418
- microsoft/aspire #17795 — "Blazor WebAssembly Debugging Support in Aspire." **Open** feature request (one-click WASM debugging from the dashboard). Debugging feature, not a launch failure. https://github.com/microsoft/aspire/issues/17795
- dotnet/aspnetcore #64891 — "Enable debugging for Blazor WebAssembly apps launched from Aspire dashboard." Debugging feature request, not this bug. https://github.com/dotnet/aspnetcore/issues/64891
- JetBrains/aspire-plugin #129 — "Blazor WASM debugging and hot reload doesn't work." **Closed** (PR #249). The app still **runs**; only debugging/hot-reload fail. Different from "process never starts." https://github.com/JetBrains/aspire-plugin/issues/129
- JetBrains YouTrack RIDER-32948 — "Cannot start Blazor webassembly app from Rider." Older, **standalone** WASM (not Aspire-orchestrated hosted WASM). https://youtrack.jetbrains.com/issue/RIDER-32948
- JetBrains YouTrack RIDER-116833 — "Unable to launch Blazor WebAssembly App (BrowserRefreshHost error)." A `Microsoft.Extensions.Logging.Abstractions` assembly-version mismatch on **Linux**, not Aspire-orchestrated, not macOS. https://youtrack.jetbrains.com/issue/RIDER-116833

**Verdict:** the existing public items are all about **debugging/hot-reload** of WASM, or about non-Aspire / non-macOS launch failures with different root causes. None describe an Aspire-orchestrated **hosted** Blazor WASM web project whose **process is never spawned (Process ID 0)** specifically on **Rider for macOS** while the CLI works. This appears to be **not yet filed** — please submit.

---

## How to run each sample

### From the CLI (works for BOTH — baseline)
```bash
# Working (Blazor Server)
cd Working-AspireBlazorServer/ServerApp.AppHost
ASPIRE_ALLOW_UNSECURED_TRANSPORT=true dotnet run --project ServerApp.AppHost.csproj

# Broken (hosted Blazor WASM) — also serves fine from the CLI
cd Broken-AspireBlazorWasm/WasmApp.AppHost
ASPIRE_ALLOW_UNSECURED_TRANSPORT=true dotnet run --project WasmApp.AppHost.csproj
```
Open the dashboard URL printed in the console, then open the `webfrontend` resource's endpoint — it serves HTTP 200 in both cases.

### From Rider (the reproduction — GUI, manual)
1. Open the `.sln` (`Broken-AspireBlazorWasm.sln` or `Working-AspireBlazorServer.sln`).
2. Select the AppHost run configuration (`WasmApp.AppHost` or `ServerApp.AppHost`).
3. Click **Run**.
4. Working → `webfrontend` Running (Healthy), serves. Broken → `webfrontend` Running (Unhealthy), Process ID 0, never serves.

### Build only
```bash
dotnet build Working-AspireBlazorServer/Working-AspireBlazorServer.sln
dotnet build Broken-AspireBlazorWasm/Broken-AspireBlazorWasm.sln
```
Both build clean (0 warnings, 0 errors) — verified.
