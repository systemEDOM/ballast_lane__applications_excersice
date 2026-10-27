using System.Text;
using BallastLane.Api.Infrastructure;
using BallastLane.Application.Abstractions;
using BallastLane.Application.Services;
using BallastLane.Infrastructure.Persistence;
using BallastLane.Infrastructure.Security;
using BallastLane.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or too short. Provide a signing key of at least 32 characters, " +
        "for example through the Jwt__Key environment variable.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ballast-lane";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ballast-lane-client";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ballast Lane Tasks API",
        Version = "v1",
        Description = "Task board API: authentication, public metadata and per-user tasks."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token. The 'Bearer ' prefix is added automatically."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IDbConnectionFactory>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=app.db";

    return new SqliteConnectionFactory(connectionString);
});
builder.Services.AddScoped<SchemaMigrator>();
builder.Services.AddScoped<IAppInitializer, DatabaseInitializer>();
builder.Services.AddScoped<IUserRepository, SqliteUserRepository>();
builder.Services.AddScoped<ITaskRepository, SqliteTaskRepository>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ITokenIssuer>(services => new JwtTokenIssuer(
    jwtKey,
    jwtIssuer,
    jwtAudience,
    TimeSpan.FromHours(8),
    services.GetRequiredService<IClock>()));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TaskService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ballast Lane Tasks API v1");
});

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IAppInitializer>();
    await initializer.InitializeAsync(CancellationToken.None);
}

await app.RunAsync();

public partial class Program
{
}
