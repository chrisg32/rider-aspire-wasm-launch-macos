var builder = DistributedApplication.CreateBuilder(args);

// BROKEN case: a *hosted Blazor WebAssembly* web project (WasmApp.Web references the
// WasmApp.Web.Client WASM project). Identical orchestration to the Working sample, but
// Rider's IDE Aspire run path takes its WASM-aware launcher
// (WasmHostProjectSessionProcessLauncher) and silently fails to spawn the process on
// macOS: the resource shows "Running (Unhealthy)", Process ID 0, and this health check
// fails with TaskCanceledException because nothing is behind the DCP proxy endpoint.
//
// Launching the SAME AppHost from the CLI (`dotnet run --project WasmApp.AppHost`) works.
builder.AddProject<Projects.WasmApp_Web>("webfrontend")
    .WithHttpHealthCheck("/");

builder.Build().Run();
