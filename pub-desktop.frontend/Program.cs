using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication.Cookies;
using pub_desktop.frontend.Components;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// This is the routes for the specified VNC servers.
// v1 -> Primary Linux Server.
var routes = new[]
{
    new RouteConfig
    {
        RouteId = "v1",
        ClusterId = "v1",
        AuthorizationPolicy = "RequiresAuthorizedUser",
        Match = new RouteMatch { Path = "/v1/{**catch-all}" },
    }.WithTransformPathRemovePrefix("/v1")
};

// This is the clusters for the specified VNC servers.
// v1 -> Primary Linux Server.
var clusters = new[]
{
    new ClusterConfig
    {
        ClusterId = "v1",
        Destinations =
            new Dictionary<string, DestinationConfig>
            {
                {
                    "v1", new DestinationConfig
                    {
                        Address = Environment.GetEnvironmentVariable("PRIMARYMACHINE_HTTP")!
                    }
                }
            }
    }
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(3);
})
.AddDiscord(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("CLIENT_ID")!;
    options.ClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET")!;
    
    options.Scope.Add("identify");
    options.SaveTokens = true;
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequiresAuthorizedUser", policy => policy.RequireAuthenticatedUser());
builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapReverseProxy();

await app.RunAsync();