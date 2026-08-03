using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Brokers.DateTimes;
using Backup.Web.Api.Server.Brokers.Loggings;
using Backup.Web.Api.Server.Brokers.RoleManagement;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Brokers.UserManagement;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Roles;
using Backup.Web.Api.Server.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

ApplyDockerMySqlConnectionString(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// App services and brokers
builder.Services.AddScoped<Backup.Web.Api.Server.Services.ITextExtractionService, Backup.Web.Api.Server.Services.TextExtractionService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.IPdfToTextService, Backup.Web.Api.Server.Services.PdfToTextService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Brokers.Storage.IStorageBroker, Backup.Web.Api.Server.Brokers.Storage.StorageBroker>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.IDocumentService, Backup.Web.Api.Server.Services.Documents.DocumentService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Ocr.IOcrTextExtractionService, Backup.Web.Api.Server.Services.Ocr.OcrTextExtractionService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.IDocumentComparisonService, Backup.Web.Api.Server.Services.Documents.DocumentComparisonService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.IDocumentParserService, Backup.Web.Api.Server.Services.Documents.DocumentParserService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.ISupplierDocumentProductEnsureService, Backup.Web.Api.Server.Services.Documents.SupplierDocumentProductEnsureService>();
// Parsing infrastructure
builder.Services.AddSingleton<Backup.Web.Api.Server.Services.Documents.Parsing.DocumentParserConfig>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.Parsing.LanguageDetector>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.Parsing.IDocumentParser, Backup.Web.Api.Server.Services.Documents.Parsing.Suppliers.KnaufInvoiceParser>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.Parsing.IDocumentParser, Backup.Web.Api.Server.Services.Documents.Parsing.Suppliers.KnaufFinalParser>();
// Generic/simple parsers first by specificity
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.Parsing.IDocumentParser, Backup.Web.Api.Server.Services.Documents.Parsing.SpanishDeliveryNoteParser>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.Parsing.IDocumentParser, Backup.Web.Api.Server.Services.Documents.Parsing.InvoiceParser>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.Parsing.IDocumentParser, Backup.Web.Api.Server.Services.Documents.Parsing.DeliveryNoteParser>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Documents.Parsing.DocumentProcessor>();
// Ollama
builder.Services.Configure<Backup.Web.Api.Server.Services.Documents.Ollama.OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddHttpClient<Backup.Web.Api.Server.Services.Documents.Ollama.IOllamaParsingService, Backup.Web.Api.Server.Services.Documents.Ollama.OllamaParsingService>();
// Python extractor (optional)
builder.Services.Configure<Backup.Web.Api.Server.Services.Documents.Python.PythonExtractorOptions>(builder.Configuration.GetSection("PythonExtractor"));
builder.Services.Configure<Backup.Web.Api.Server.Services.Pricing.ErpPricingOptions>(builder.Configuration.GetSection("ErpPricing"));
builder.Services.Configure<Backup.Web.Api.Server.Services.ErpSync.ErpSyncOptions>(
    builder.Configuration.GetSection(Backup.Web.Api.Server.Services.ErpSync.ErpSyncOptions.SectionName));
builder.Services.AddHttpClient<Backup.Web.Api.Server.Services.Documents.Python.IPythonExtractorClient, Backup.Web.Api.Server.Services.Documents.Python.PythonExtractorClient>();
builder.Services.AddHttpClient(nameof(Backup.Web.Api.Server.Controllers.PythonProxyController), client =>
{
    client.Timeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Stock.IStockService, Backup.Web.Api.Server.Services.Stock.StockService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Numbering.INumberingSequenceService, Backup.Web.Api.Server.Services.Numbering.NumberingSequenceService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.BusinessPdf.IBusinessDocumentPdfService, Backup.Web.Api.Server.Services.BusinessPdf.BusinessDocumentPdfService>();
builder.Services.AddHttpClient("ErpProductImages", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<Backup.Web.Api.Server.Services.Pricing.IErpPricingService, Backup.Web.Api.Server.Services.Pricing.ErpPricingService>();
builder.Services.AddHttpClient<Backup.Web.Api.Server.Services.ErpSync.IErpProductSyncService, Backup.Web.Api.Server.Services.ErpSync.ErpProductSyncService>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue("ErpSync:TimeoutSeconds", 30);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds));
});
builder.Services.AddScoped<Backup.Web.Api.Server.Services.ErpSync.IErpExcelImportService, Backup.Web.Api.Server.Services.ErpSync.ErpExcelImportService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.ErpSync.IErpCatalogSyncService, Backup.Web.Api.Server.Services.ErpSync.ErpCatalogSyncService>();
builder.Services.AddHostedService<Backup.Web.Api.Server.Services.ErpSync.ErpProductSyncBackgroundService>();
builder.Services.AddHostedService<Backup.Web.Api.Server.Services.Archiving.DocumentArchiveBackgroundService>();

builder.Services.Configure<Backup.Web.Api.Server.Services.StoreChat.StoreChatOptions>(
    builder.Configuration.GetSection(Backup.Web.Api.Server.Services.StoreChat.StoreChatOptions.SectionName));
builder.Services.Configure<Backup.Web.Api.Server.Services.StoreChat.AiSettingsOptions>(
    builder.Configuration.GetSection(Backup.Web.Api.Server.Services.StoreChat.AiSettingsOptions.SectionName));
builder.Services.Configure<Backup.Web.Api.Server.Services.StoreChat.StripeOptions>(
    builder.Configuration.GetSection(Backup.Web.Api.Server.Services.StoreChat.StripeOptions.SectionName));
builder.Services.AddSingleton<Backup.Web.Api.Server.Services.StoreChat.IStoreChatSessionStore, Backup.Web.Api.Server.Services.StoreChat.InMemoryStoreChatSessionStore>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.StoreChat.IStoreChatPdfService, Backup.Web.Api.Server.Services.StoreChat.StoreChatPdfService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.StoreChat.IStoreChatStripeService, Backup.Web.Api.Server.Services.StoreChat.StoreChatStripeService>();
builder.Services.AddSingleton<Backup.Web.Api.Server.Services.SalesAssistant.Guides.IProjectGuideRegistry>(
    Backup.Web.Api.Server.Services.SalesAssistant.Guides.ProjectGuideRegistry.Default);
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesContextDetector, Backup.Web.Api.Server.Services.SalesAssistant.SalesContextDetector>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesCatalogSearchTool, Backup.Web.Api.Server.Services.SalesAssistant.SalesCatalogSearchTool>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesCommerceTool, Backup.Web.Api.Server.Services.SalesAssistant.SalesCommerceTool>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesComplementTool, Backup.Web.Api.Server.Services.SalesAssistant.SalesComplementTool>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.StoreChat.IStoreChatService, Backup.Web.Api.Server.Services.StoreChat.StoreChatService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesProjectService, Backup.Web.Api.Server.Services.SalesAssistant.SalesProjectService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesGuidedIntentDetector, Backup.Web.Api.Server.Services.SalesAssistant.SalesGuidedIntentDetector>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesPackEngine, Backup.Web.Api.Server.Services.SalesAssistant.SalesPackEngine>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesRecommendationEngine, Backup.Web.Api.Server.Services.SalesAssistant.SalesRecommendationEngine>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesCompareEngine, Backup.Web.Api.Server.Services.SalesAssistant.SalesCompareEngine>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesJustificationService, Backup.Web.Api.Server.Services.SalesAssistant.SalesJustificationService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesConfidenceEngine, Backup.Web.Api.Server.Services.SalesAssistant.SalesConfidenceEngine>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesPromoService, Backup.Web.Api.Server.Services.SalesAssistant.SalesPromoService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesLogisticsEngine, Backup.Web.Api.Server.Services.SalesAssistant.SalesLogisticsEngine>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesPlanningEngine, Backup.Web.Api.Server.Services.SalesAssistant.SalesPlanningEngine>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesProjectResumeService, Backup.Web.Api.Server.Services.SalesAssistant.SalesProjectResumeService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesPhotoClassifier, Backup.Web.Api.Server.Services.SalesAssistant.SalesPhotoClassifier>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesWallSchemaParser, Backup.Web.Api.Server.Services.SalesAssistant.SalesWallSchemaParser>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesSemanticSearch, Backup.Web.Api.Server.Services.SalesAssistant.SalesBagOfWordsSearch>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesWorkflowGuard, Backup.Web.Api.Server.Services.SalesAssistant.SalesWorkflowGuard>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesTurnResponder, Backup.Web.Api.Server.Services.SalesAssistant.SalesTurnResponder>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnDispatcher, Backup.Web.Api.Server.Services.SalesAssistant.Turns.SalesGuidedTurnDispatcher>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.CartComplementsHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.ConfirmComplementsHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.DirectComplementHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.HesitationHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.StyleHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.WallSchemaHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.TipsHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.SavingsHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.PromosHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.LogisticsHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.PlanningHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.SemanticSearchHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.WhyProductHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.CompareHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.PackRequestHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.Turns.ISalesGuidedTurnHandler, Backup.Web.Api.Server.Services.SalesAssistant.Turns.MoreProductsHandler>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesReplyComposer, Backup.Web.Api.Server.Services.SalesAssistant.SalesReplyComposer>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesLlmIntentRouter, Backup.Web.Api.Server.Services.SalesAssistant.SalesLlmIntentRouter>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesDeterministicReply, Backup.Web.Api.Server.Services.SalesAssistant.SalesDeterministicReply>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.ISalesAssistantFacade, Backup.Web.Api.Server.Services.SalesAssistant.SalesAssistantFacade>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.SalesAssistant.IStoreChatTurnLogService, Backup.Web.Api.Server.Services.SalesAssistant.StoreChatTurnLogService>();
builder.Services.AddHttpClient<Backup.Web.Api.Server.Services.StoreChat.IStoreChatAiClient, Backup.Web.Api.Server.Services.StoreChat.StoreChatAiClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddDbContext<StorageBroker>();
builder.Services.AddScoped<IUserManagementBroker, UserManagementBroker>();
builder.Services.AddScoped<IRoleManagementBroker, RoleManagementBroker>();
builder.Services.AddTransient<IStorageBroker, StorageBroker>();
builder.Services.AddTransient<ILogger, Logger<LoggingBroker>>();
builder.Services.AddTransient<ILoggingBroker, LoggingBroker>();
builder.Services.AddTransient<IDateTimeBroker, DateTimeBroker>();
builder.Services.AddTransient<IRoleService, RoleService>();
builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TooGoodToGo API", Version = "v1" });
    //c.SchemaFilter<IgnoreNavigationPropertiesFilter>();
});



builder.Services.AddTransient<IJwtUtils, JwtUtils>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Tenancy.ICompanyContextService, Backup.Web.Api.Server.Services.Tenancy.CompanyContextService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Tenancy.TenancySeedService>();
builder.Services.AddScoped<Backup.Web.Api.Server.Services.Tenancy.SupplierSeedService>();
builder.Services.Configure<Backup.Web.Api.Server.Models.AppSettings.AppSettings>(
    builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<Backup.Web.Api.Server.Models.AppSettings.AuthSeedOptions>(
    builder.Configuration.GetSection(Backup.Web.Api.Server.Models.AppSettings.AuthSeedOptions.SectionName));
builder.Services.AddIdentityCore<User>(options =>
{
    options.User.RequireUniqueEmail = false;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

}).AddRoles<Role>()
        .AddEntityFrameworkStores<StorageBroker>()
        .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
    // SignalR WebSocket : token via ?access_token=
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider,
    Backup.Web.Api.Server.Services.Security.JwtUserIdProvider>();
builder.Services.AddSingleton<Backup.Web.Api.Server.Services.Security.IPermissionChangeNotifier,
    Backup.Web.Api.Server.Services.Security.PermissionChangeNotifier>();
builder.Services.AddAuthorization(options =>
{
    Backup.Web.Api.Server.Authorization.PermissionPolicyRegistration.RegisterPermissionPolicies(options);
});
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider,
    Backup.Web.Api.Server.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    Backup.Web.Api.Server.Authorization.PermissionAuthorizationHandler>();
// CORS for local dev (Angular :4200 → API :5243, SignalR inclus)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
builder.Services.AddRequestTimeouts();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    try
    {
        using var scope = app.Services.CreateScope();
        if (scope.ServiceProvider.GetService<StorageBroker>() is { } broker)
            broker.Database.Migrate();
    }
    catch (Exception ex)
    {
        var startupLogger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");
        startupLogger.LogError(
            ex,
            "Database migration failed on startup — app continues but DB features may be unavailable");
    }
}

try
{
    using var seedScope = app.Services.CreateScope();
    var seedLogger = seedScope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("AuthSeed");
    await Backup.Web.Api.Server.Services.Users.AuthSeedService.SeedAsync(
        seedScope.ServiceProvider,
        seedLogger);
}
catch (Exception ex)
{
    var startupLogger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("AuthSeed");
    startupLogger.LogError(ex, "Auth seed failed on startup");
}

try
{
    using var tenancyScope = app.Services.CreateScope();
    var tenancySeed = tenancyScope.ServiceProvider.GetRequiredService<Backup.Web.Api.Server.Services.Tenancy.TenancySeedService>();
    await tenancySeed.EnsureDefaultsAsync();

    var supplierSeed = tenancyScope.ServiceProvider.GetRequiredService<Backup.Web.Api.Server.Services.Tenancy.SupplierSeedService>();
    await supplierSeed.EnsurePulseSuppliersAsync();
}
catch (Exception ex)
{
    var startupLogger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("TenancySeed");
    startupLogger.LogError(ex, "Tenancy/supplier seed failed on startup");
}

if (builder.Configuration.GetValue("UseHttpsRedirection", true))
    app.UseHttpsRedirection();

app.UseCors();

app.UseRequestTimeouts();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapControllers();
app.MapHub<Backup.Web.Api.Server.Hubs.PermissionsHub>(Backup.Web.Api.Server.Hubs.PermissionsHub.HubPath);

app.MapFallbackToFile("/index.html");

app.Run();

static void ApplyDockerMySqlConnectionString(IConfiguration configuration)
{
    var mysqlUser = configuration["MYSQL_USER"];
    if (string.IsNullOrWhiteSpace(mysqlUser))
        return;

    var csb = new MySqlConnectionStringBuilder
    {
        Server = configuration["MYSQL_HOST"] ?? "mysql",
        Port = uint.TryParse(configuration["MYSQL_PORT"], out var port) ? port : 3306,
        Database = configuration["MYSQL_DATABASE"] ?? "backupcontent",
        UserID = mysqlUser,
        Password = configuration["MYSQL_PASSWORD"] ?? string.Empty,
        PersistSecurityInfo = false,
        ConnectionTimeout = 300
    };

    configuration["ConnectionStrings:DefaultConnection"] = csb.ConnectionString;
}

