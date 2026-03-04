using CnssProxy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register the CNSS HttpClient with fixed headers and SSL bypass
builder
    .Services.AddHttpClient(
        "cnss",
        client =>
        {
            client.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
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

app.UseRouting();
app.MapControllers();

app.Run();
