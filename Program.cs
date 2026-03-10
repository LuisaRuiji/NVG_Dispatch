using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NVGInventory.BackgroundJobs;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Services;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Middleware;
using NVGInventory.Serialization;
using NVGInventory.Security;
using NVGInventory.Modules.Dispatching;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new ItemTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new AssetTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new AssetStatusJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new RequestTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new RequestStatusJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ApprovalStatusJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ApprovalDecisionJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ReturnConditionJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new LoanStatusJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new PurchaseOrderStatusJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new TripStatusJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new TripStopTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new TripDocumentTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new TripDocumentStateJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ShipmentRequestStatusJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ShipmentRequestDocumentTypeJsonConverter());
    });
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var firstError = context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        var message = string.IsNullOrWhiteSpace(firstError) ? "Validation failed." : firstError;
        return new BadRequestObjectResult(new ApiErrorResponse(
            "VALIDATION_ERROR",
            message,
            context.HttpContext.TraceIdentifier));
    };
});
builder.Services.AddDbContext<InventoryDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
    }
    options.UseSqlServer(connectionString);
});
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<DispatchingOptions>(builder.Configuration.GetSection(DispatchingOptions.SectionName));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthEventService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<RequestService>();
builder.Services.AddScoped<StockLedgerService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<RequestWorkflowService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<KitComponentService>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<LoanWorkflowService>();
builder.Services.AddScoped<RequestQueryService>();
builder.Services.AddScoped<LoanQueryService>();
builder.Services.AddScoped<ReportQueryService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<PurchaseOrderWorkflowService>();
builder.Services.AddScoped<PurchaseOrderQueryService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<InventoryAdjustmentWorkflowService>();
builder.Services.AddScoped<InventoryAdjustmentQueryService>();
builder.Services.AddScoped<ModuleSettingsService>();
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.DispatchTripService>();
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.ITripLifecycleService>(serviceProvider =>
    serviceProvider.GetRequiredService<NVGInventory.Modules.Dispatching.Services.DispatchTripService>());
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.DispatchDocumentWorkflowService>();
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.IDispatchDocumentWorkflowService>(serviceProvider =>
    serviceProvider.GetRequiredService<NVGInventory.Modules.Dispatching.Services.DispatchDocumentWorkflowService>());
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.IDispatchDocumentReadService>(serviceProvider =>
    serviceProvider.GetRequiredService<NVGInventory.Modules.Dispatching.Services.DispatchDocumentWorkflowService>());
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.DispatchTripQueryService>();
builder.Services.AddScoped<NVGInventory.Modules.ShipmentRequests.Services.IShipmentRequestTripDispatchGateway, NVGInventory.Modules.Dispatching.Services.DispatchShipmentRequestTripDispatchGateway>();
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.IDispatchShipmentReadService, NVGInventory.Modules.Dispatching.Services.DispatchShipmentReadService>();
builder.Services.AddScoped<NVGInventory.Modules.Dispatching.Services.DispatchCustomerService>();
builder.Services.AddScoped<NVGInventory.Modules.ShipmentRequests.Services.IShipmentRequestTripCreationService, NVGInventory.Modules.ShipmentRequests.Services.ShipmentRequestTripCreationService>();
builder.Services.AddScoped<NVGInventory.Modules.ShipmentRequests.Services.IPortalCustomerAccessService, NVGInventory.Modules.ShipmentRequests.Services.PortalCustomerAccessService>();
builder.Services.AddScoped<NVGInventory.Modules.ShipmentRequests.Services.ShipmentRequestService>();
builder.Services.AddScoped<NVGInventory.Modules.ShipmentRequests.Services.ShipmentRequestQueryService>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddScoped<PerformanceDataSeeder>();
builder.Services.Configure<IntegrityCheckJobOptions>(builder.Configuration.GetSection("BackgroundJobs:IntegrityCheck"));
builder.Services.AddHostedService<IntegrityCheckHostedService>();
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var frontendBaseUrl = builder.Configuration["FrontendBaseUrl"];
var allowedOrigins = corsOrigins
    .Concat(SplitOrigins(frontendBaseUrl))
    .Select(origin => origin.Trim())
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("CORS origins are not configured. Set FrontendBaseUrl or Cors:AllowedOrigins.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtOptions.Key ?? string.Empty);
if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException("JWT signing key must be configured and at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
            NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "NVGInventory API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT in the format: Bearer {token}"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    var permitLimit = builder.Configuration.GetValue("RateLimiting:Login:PermitLimit", 5);
    var windowMinutes = builder.Configuration.GetValue("RateLimiting:Login:WindowMinutes", 1);
    var queueLimit = builder.Configuration.GetValue("RateLimiting:Login:QueueLimit", 0);

    if (permitLimit <= 0)
    {
        permitLimit = 5;
    }

    if (windowMinutes <= 0)
    {
        windowMinutes = 1;
    }

    if (queueLimit < 0)
    {
        queueLimit = 0;
    }

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        httpContext.Response.ContentType = "application/json";
        var payload = new ApiErrorResponse(
            "RATE_LIMITED",
            "Too many requests. Please try again later.",
            httpContext.TraceIdentifier);
        await httpContext.Response.WriteAsJsonAsync(payload, cancellationToken);
    };

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(windowMinutes),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = queueLimit
            }));
});

var app = builder.Build();

var seedDemo = args.Any(arg =>
    string.Equals(arg, "seed-demo", StringComparison.OrdinalIgnoreCase)
    || string.Equals(arg, "--seed-demo", StringComparison.OrdinalIgnoreCase));

var seedPerf = args.Any(arg =>
    string.Equals(arg, "seed-perf", StringComparison.OrdinalIgnoreCase)
    || string.Equals(arg, "--seed-perf", StringComparison.OrdinalIgnoreCase));

if (seedPerf)
{
    var reset = args.Any(arg => string.Equals(arg, "--reset", StringComparison.OrdinalIgnoreCase));
    var totalRequests = ParseIntArg(args, "--requests", 1000);
    var pendingManager = ParseIntArg(args, "--pending-manager", 200);
    var approvedCount = ParseIntArg(args, "--approved", 100);

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<PerformanceDataSeeder>();
    await seeder.SeedAsync(
        new PerformanceSeedOptions(totalRequests, pendingManager, approvedCount),
        reset,
        CancellationToken.None);
    return;
}

if (seedDemo)
{
    var reset = args.Any(arg => string.Equals(arg, "--reset", StringComparison.OrdinalIgnoreCase));
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    await seeder.SeedAsync(reset, CancellationToken.None);
    return;
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.Migrate();
}

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
    }

    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");
    var originalBody = context.Response.Body;
    await using var countingBody = new CountingStream(originalBody);
    context.Response.Body = countingBody;
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();
        context.Response.Body = originalBody;
        var userId = context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                     ?? context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var path = context.Request.Path + context.Request.QueryString;
        var statusCode = context.Response.StatusCode;
        var sizeBytes = countingBody.BytesWritten;
        if (context.Response.ContentLength.HasValue && context.Response.ContentLength.Value > sizeBytes)
        {
            sizeBytes = context.Response.ContentLength.Value;
        }

        var logLevel = statusCode >= 500
            ? LogLevel.Error
            : statusCode >= 400
                ? LogLevel.Warning
                : LogLevel.Information;

        logger.Log(
            logLevel,
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms Size={SizeBytes}B UserId={UserId} CorrelationId={CorrelationId} TraceId={TraceId}",
            context.Request.Method,
            path,
            statusCode,
            stopwatch.ElapsedMilliseconds,
            sizeBytes,
            userId ?? "-",
            correlationId,
            context.TraceIdentifier);
    }
});

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1", out var remainder))
    {
        context.Request.Path = "/api" + remainder;
    }

    await next();
});

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Exception");
    try
    {
        await next();
    }
    catch (DomainException ex)
    {
        logger.LogWarning(
            ex,
            "Domain error for request {Path} TraceId={TraceId}",
            context.Request.Path,
            context.TraceIdentifier);
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/json";
        var details = ex is ConflictDomainException conflict ? conflict.Details : null;
        var payload = new ApiErrorResponse(ex.ErrorCode, ex.Message, context.TraceIdentifier, details);
        await context.Response.WriteAsJsonAsync(payload);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Unhandled error for request {Path} TraceId={TraceId}",
            context.Request.Path,
            context.TraceIdentifier);
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var message = app.Environment.IsDevelopment() ? ex.Message : "Unexpected error occurred.";
        var payload = new ApiErrorResponse("INTERNAL_ERROR", message, context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(payload);
    }
});

app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    if (response.HasStarted)
    {
        return;
    }

    if (response.Body is CountingStream counting && counting.BytesWritten > 0)
    {
        return;
    }

    string? errorCode = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
        StatusCodes.Status403Forbidden => "FORBIDDEN",
        StatusCodes.Status404NotFound => "NOT_FOUND",
        _ => null
    };

    if (errorCode is null)
    {
        return;
    }

    var message = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => "Unauthorized.",
        StatusCodes.Status403Forbidden => "Forbidden.",
        StatusCodes.Status404NotFound => "Not found.",
        _ => "Error."
    };

    response.ContentType = "application/json";
    var payload = new ApiErrorResponse(errorCode, message, statusContext.HttpContext.TraceIdentifier);
    await response.WriteAsJsonAsync(payload);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await DevSeedData.EnsureSeededAsync(app.Services, app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ModuleMaintenanceMiddleware>();

app.MapControllers();
app.MapGet("/health", async (InventoryDbContext dbContext, CancellationToken cancellationToken) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return Results.Json(new
            {
                status = "Unhealthy",
                database = "Disconnected",
                timestamp = DateTime.UtcNow
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

        return Results.Ok(new
        {
            status = "Healthy",
            database = "Connected",
            timestamp = DateTime.UtcNow
        });
    }
    catch
    {
        return Results.Json(new
        {
            status = "Unhealthy",
            database = "Error",
            timestamp = DateTime.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

static IEnumerable<string> SplitOrigins(string? origins)
{
    if (string.IsNullOrWhiteSpace(origins))
    {
        return Array.Empty<string>();
    }

    return origins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static int ParseIntArg(string[] args, string name, int defaultValue)
{
    foreach (var arg in args)
    {
        if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            var raw = arg[(name.Length + 1)..];
            if (int.TryParse(raw, out var value))
            {
                return value;
            }
        }
    }

    return defaultValue;
}

sealed class CountingStream : Stream
{
    private readonly Stream _inner;

    public CountingStream(Stream inner)
    {
        _inner = inner;
    }

    public long BytesWritten { get; private set; }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush()
    {
        _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _inner.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        BytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        BytesWritten += buffer.Length;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        BytesWritten += buffer.Length;
        return _inner.WriteAsync(buffer, cancellationToken);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        BytesWritten += count;
        return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }
}
