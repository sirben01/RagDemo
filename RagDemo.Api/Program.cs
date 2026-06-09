using RagDemo.Core.Interfaces;
using RagDemo.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// CORS for React dev server
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173")
     .AllowAnyHeader()
     .AllowAnyMethod()));

// Config sections
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));

// Named HTTP clients
builder.Services.AddHttpClient("openai", (sp, c) =>
{
    var key = builder.Configuration["OpenAI:ApiKey"] ?? "";
    c.BaseAddress = new Uri("https://api.openai.com");
    c.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
});

builder.Services.AddHttpClient("anthropic", (sp, c) =>
{
    var key = builder.Configuration["Anthropic:ApiKey"] ?? "";
    c.BaseAddress = new Uri("https://api.anthropic.com");
    c.DefaultRequestHeaders.Add("x-api-key", key);
    c.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});

builder.Services.AddHttpClient("crawler", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.Add("User-Agent", "RagDemo/1.0 (web crawler)");
});

// Core services
builder.Services.AddScoped<IWebCrawler, WebCrawler>();
builder.Services.AddSingleton<ITextChunker, TextChunker>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();
builder.Services.AddScoped<IAnthropicService, AnthropicService>();
builder.Services.AddScoped<IRagPipelineService, RagPipelineService>();

var app = builder.Build();

app.UseCors();
app.MapControllers();

app.Run();
