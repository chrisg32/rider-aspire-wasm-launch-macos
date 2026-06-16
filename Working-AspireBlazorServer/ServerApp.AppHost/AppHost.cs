var builder = DistributedApplication.CreateBuilder(args);

// WORKING case: a Blazor *Server* (non-WASM) web project orchestrated by Aspire.
// This launches cleanly from Rider's IDE Aspire run path on macOS.
builder.AddProject<Projects.ServerApp_Web>("webfrontend")
    .WithHttpHealthCheck("/");

builder.Build().Run();
