using Azure.Identity;
using ImageSharingWithCloud.DAL;
using ImageSharingWithCloud.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System;
using System.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

/*
 * Add services to the container.
 */
builder.Services.AddControllersWithViews();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();
/*
 * Configure cookie policy to allow ADA saved in a cookie.
 */
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // This lambda determines whether user consent for non-essential cookies is needed for a given request.
    options.CheckConsentNeeded = context => true;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});

/*
 * Configure logging to go the console (local testing only!), also Azure logging.
 */
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddAzureWebAppDiagnostics();

/*
 * If in production mode, add secret access keys that will be stored in Azure Key Vault.
 
if (builder.Environment.IsProduction())
{
    string vault = builder.Configuration[StorageConfig.KeyVaultUri];
    if (vault == null)
    {
        throw new ArgumentNullException("Missing key vault URI in configuration!");
    }
    else
    {
        Uri vaultUri = new Uri(vault);
        //builder.Configuration.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
    }
}*/

/*
 * Connection string for SQL database; append credentials if present (from Azure Vault).
 */
string dbConnectionString = builder.Configuration[StorageConfig.ApplicationDbConnString];
if (dbConnectionString == null)
{
    throw new ArgumentNullException("Missing SQL connection string in configuration: " + StorageConfig.ApplicationDbConnString);
}
string database = builder.Configuration[StorageConfig.ApplicationDbDatabase];
if (database == null)
{
    throw new ArgumentNullException("Missing database name in configuration: " + StorageConfig.ApplicationDbDatabase);
}
string dbUser = builder.Configuration[StorageConfig.ApplicationDbUser];
string dbPassword = builder.Configuration[StorageConfig.ApplicationDbPassword];
dbConnectionString = StorageConfig.getDatabaseConnectionString(dbConnectionString, database, dbUser, dbPassword);

// TODO Add database context & enable saving sensitive data in the log (not for production use!)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(dbConnectionString, sqlServerOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    });
    // Only add this if in development environment
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});
// For SQL Database, allow for db connection sometimes being lost

// Replacement for database error page
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// TODO add Identity service



/*
 * Best practice is to have a single instance of a Cosmos client for an application.
 * Use dependency injection to inject this single instance into ImageStorage repository.
 */
CosmosClient imageDbClient = ImageStorage.GetImageDbClient(builder.Environment, builder.Configuration);
builder.Services.AddSingleton<CosmosClient>(imageDbClient);

// Add our own service for managing access to logContext of image views
builder.Services.AddScoped<ILogContext, LogContext>();

// Add our own service for managing uploading of images to blob storage
builder.Services.AddScoped<IImageStorage, ImageStorage>();


WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCookiePolicy();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

/*
 * Everything is configurable.
 */
app.MapDefaultControllerRoute();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{Id?}");

/*
 * TODO Seed the database: We need to manually inject the dependencies of the initalizer.
 * EF services are scoped to a request, so we must create a temporary scope for its injection.
 * More on dependency injection: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection
 * More on DbContext lifetime: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
 */
if (app.Environment.IsDevelopment())
{
    using (var serviceScope = app.Services.CreateScope())
    {
        var serviceProvider = serviceScope.ServiceProvider;

        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbInitializer>>();
        var imageStorage = serviceProvider.GetRequiredService<IImageStorage>();
        var logContext = serviceProvider.GetRequiredService<ILogContext>();

        await new ApplicationDbInitializer(db, imageStorage, logContext, logger).SeedDatabase(serviceProvider);
    }
}


/*
 * Finally, run the application!
 */

app.Run();