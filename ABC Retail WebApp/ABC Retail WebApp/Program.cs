using System;
using ABC_Retail_WebApp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Register HttpClient-backed FunctionsService and configure the HttpClient BaseAddress
builder.Services.AddHttpClient<FunctionsService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("FunctionsHttpClient");

    // Priority order:
    // 1. Explicit override: AzureFunctions:BaseUrl
    // 2. Environment-specific values (production vs local)
    // 3. Fallback to any configured value
    var explicitBase = config["AzureFunctions:BaseUrl"];
    var baseLocal = config["AzureFunctions:BaseUrlLocal"];
    var baseProd = config["AzureFunctions:BaseUrlProduction"];

    string baseUrl;

    if (!string.IsNullOrWhiteSpace(explicitBase))
    {
        baseUrl = explicitBase;
        logger.LogInformation("AzureFunctions:BaseUrl override found and will be used.");
    }
    else if (env.IsProduction() && !string.IsNullOrWhiteSpace(baseProd))
    {
        baseUrl = baseProd;
        logger.LogInformation("Using AzureFunctions:BaseUrlProduction because environment is Production.");
    }
    else if (!env.IsProduction() && !string.IsNullOrWhiteSpace(baseLocal))
    {
        baseUrl = baseLocal;
        logger.LogInformation("Using AzureFunctions:BaseUrlLocal because environment is not Production.");
    }
    else
    {
        // final fallback to whichever is available
        baseUrl = !string.IsNullOrWhiteSpace(baseProd) ? baseProd : baseLocal;
        logger.LogWarning("No explicit AzureFunctions:BaseUrl found. Falling back to whichever BaseUrl is present (may be null).");
    }

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        // Fail early with an informative message so the app doesn't silently keep trying localhost
        throw new InvalidOperationException("Azure Functions base URL is not configured. Set AzureFunctions:BaseUrl, AzureFunctions:BaseUrlProduction or AzureFunctions:BaseUrlLocal in configuration or App Settings.");
    }

    // Normalize URL: trim trailing slashes
    baseUrl = baseUrl.TrimEnd('/');

    // Ensure BaseUrl contains the /api segment if your service calls are relative like "customers"
    // If you prefer to control /api in code paths, remove this block.
    if (!baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        // If the configured base looks like a function host (azurewebsites.net) and doesn't include /api,
        // append /api so client.GetAsync("customers") will request /api/customers.
        // This avoids accidentally hitting the host root.
        baseUrl = baseUrl + "/api";
        logger.LogInformation("Appended /api to Azure Functions BaseUrl to ensure relative calls hit the function routes.");
    }

    logger.LogInformation("Using Azure Functions BaseUrl: {BaseUrl} (Environment: {Env})", baseUrl, env.EnvironmentName);

    client.BaseAddress = new Uri(baseUrl);
});

// If you have other services to register, do so here (storage, logging, etc).

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();