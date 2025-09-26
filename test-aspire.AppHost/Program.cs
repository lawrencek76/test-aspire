using System.Diagnostics;

var appConsole = AppDomain.CurrentDomain.BaseDirectory.Replace("AppHost", "AppConsole");

using var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = Path.Combine(appConsole, "test-aspire.AppConsole.exe"),
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = false,
        Verb = "runas"
    }
};

process.Start();
process.BeginErrorReadLine();
process.BeginOutputReadLine();
process.WaitForExit();

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.test_aspire_ApiService>("apiservice")
    .WithUrlForEndpoint("https", config => config.Url = "https://api.site.test/weatherforecast");

builder.AddProject<Projects.test_aspire_Web>("webfrontend")
    .WithUrlForEndpoint("https", config => config.Url = "https://www.site.test")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
