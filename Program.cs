using CnssProxy.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

var builder = WebApplication.CreateBuilder(args);

// ── .env file ──────────────────────────────────────────────────────────────
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            continue;
        var idx = trimmed.IndexOf('=');
        if (idx < 1)
            continue;
        var key = trimmed[..idx].Trim();
        var value = trimmed[(idx + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, value);
    }
}

// ── HashiCorp Vault ────────────────────────────────────────────────────────
var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");
if (!string.IsNullOrEmpty(vaultToken))
{
    var vaultAddress = builder.Configuration["Vault:Address"] ?? "http://148.230.115.49:8200";
    var vaultMount = builder.Configuration["Vault:MountPoint"] ?? "Secrets";
    var vaultPath = builder.Configuration["Vault:SecretPath"] ?? "database/cnss-gateway";

    try
    {
        var vaultClient = new VaultClient(
            new VaultClientSettings(vaultAddress, new TokenAuthMethodInfo(vaultToken))
        );
        var secret = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
            path: vaultPath,
            mountPoint: vaultMount
        );
        var overrides = secret
            .Data.Data.Select(kv => new KeyValuePair<string, string?>(
                kv.Key.Replace("__", ":"),
                kv.Value?.ToString()
            ))
            .ToList();
        builder.Configuration.AddInMemoryCollection(overrides);
        Console.WriteLine(
            $"[Vault] Loaded {overrides.Count} secret(s) from {vaultMount}/{vaultPath}"
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Vault] Warning: could not load secrets — {ex.Message}");
    }
}
else
{
    Console.WriteLine("[Vault] VAULT_TOKEN not set — using appsettings values.");
}

builder.Services.AddControllers();

// ── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CNSS Gateway", Version = "v1" });

    // Allow JWT in Swagger UI
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste your Keycloak access token here.",
        }
    );
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                []
            },
        }
    );
});

// ── Authentication (Keycloak JWT) ──────────────────────────────────────────
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>(
            "Keycloak:RequireHttps",
            false
        );
    });

builder.Services.AddAuthorization();

// ── In-process cache ───────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// ── MongoDB & domain services ──────────────────────────────────────────────
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddSingleton<CnssAuthService>();
builder.Services.AddSingleton<CnssApiService>();

// ── CNSS HttpClient ────────────────────────────────────────────────────────
builder
    .Services.AddHttpClient(
        "cnss",
        client =>
        {
            client.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
            );
            client.DefaultRequestHeaders.Add("Accept", "application/json, */*");
            client.DefaultRequestHeaders.Add("Accept-Language", "fr-FR,fr;q=0.9,en;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(120);
        }
    )
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        }
    );

var app = builder.Build();

// Global exception handler
app.Use(
    async (ctx, next) =>
    {
        try
        {
            await next(ctx);
        }
        catch (OtpRequiredException ex)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { message = ex.Message, otpRequired = true });
        }
        catch (KeyNotFoundException ex)
        {
            ctx.Response.StatusCode = 404;
            await ctx.Response.WriteAsJsonAsync(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            ctx.Response.StatusCode = 502;
            await ctx.Response.WriteAsJsonAsync(
                new { message = "CNSS upstream error.", error = ex.Message }
            );
        }
    }
);

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CNSS Gateway v1"));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
