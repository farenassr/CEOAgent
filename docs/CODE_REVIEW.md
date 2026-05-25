# 📋 Revisión Técnica Enterprise — CeoAgent

> **Revisor:** Principal Software Engineer / Senior Solutions Architect
> **Fecha:** 2026-05-25
> **Alcance:** Solución completa `CeoAgent.slnx` (.NET 11, Aspire 13.3.5, PostgreSQL, Azure Storage, Microsoft Agent Framework)
> **Arquitectura objetivo:** Modular Monolith + Clean Architecture + Vertical Slice + Ports & Adapters
> **Estado del MVP:** Funcional pero con riesgos de seguridad bloqueantes para producción.

---

## 1. 🧭 Conclusiones Generales

CeoAgent es un proyecto **estructuralmente excelente** para una base joven. La elección tecnológica es moderna y coherente: .NET 11, Aspire para orquestación, FastEndpoints para el slice HTTP, Mediator (martinothamar) con source generator, Mapperly compile‑time, Refit + Polly para adaptadores externos, ZLogger + OpenTelemetry + Langfuse para observabilidad, EF Core 10 con Npgsql y `jsonb` real, TUnit + Testcontainers para integración. La calidad estática está reforzada con `TreatWarningsAsErrors`, `Meziantou.Analyzer`, `Roslynator.Analyzers`, `BannedSymbols.txt` (prohibiendo `DateTime.UtcNow`) y `Directory.Build.props` centralizado.

La **legibilidad es alta**: nombres descriptivos, XML‑docs en entidades, separación por slices (`Modules/Companies/Endpoints|Commands|Mappers`), validators FluentValidation embebidos por endpoint, y entidades expresivas con factory methods. Las reglas del proyecto (`README.md` y `AGENTS.md` referenciado) están **internamente consistentes** y se cumplen en el código existente (TimeProvider, `Guid.CreateVersion7()`, Mapperly por módulo, query filters por `company_id`).

La **mantenibilidad** es buena. El acoplamiento entre proyectos respeta una direccionalidad clara: `Application → Infrastructure → ApiService/Worker`, con `Integrations` como puertos puros y `Adapters` como implementaciones. Sin embargo hay **fricciones arquitectónicas reales**: la `ApiService` referencia directamente `CeoAgent.Infrastructure` (incluyendo `CeoAgentDbContext` concreto) desde los endpoints, lo que mezcla el rol de "Application" con detalles de persistencia y rompe la inversión de dependencias prometida.

La **coherencia entre módulos** es buena pero el repo está en **estado pre‑MVP**: los proyectos `CeoAgent.Adapters`, `CeoAgent.Integrations`, `CeoAgent.Tools`, `CeoAgent.Application` son prácticamente carcasas vacías (un solo archivo `*Assembly.cs` cada uno). El `Worker` solo loguea cada minuto — no consume cola, no llama al agente, no ejecuta herramientas. Lo que está construido está bien, pero el flujo principal documentado en el README (1‑10) **no está implementado**.

**Riesgos críticos detectados (bloqueantes para producción):**

1. 🔴 **Endpoints admin sin autenticación** — `UseFastEndpoints` los marca `AllowAnonymous()` globalmente, contradiciendo el README que dice "endpoints admin usan API key estática".
2. 🔴 **IDOR (Insecure Direct Object Reference) en el tenant resolver** — el `companyId` se toma de un header HTTP arbitrario (`X-Company-Id`) sin firma, JWT ni vínculo a una identidad autenticada. Cualquier cliente puede asumir cualquier tenant.
3. 🔴 **Singleton `CompanyContextAccessor` con `AsyncLocal` + `DbContextPool`** — combinación peligrosa: el pool reutiliza instancias del `DbContext`, los `HasQueryFilter` capturan al `ICompanyContext` singleton, y aunque `AsyncLocal` mitiga la fuga entre requests, también **rompe el aislamiento cuando código background (Worker) ejecuta queries fuera del scope HTTP** o cuando el filtro se compila la primera vez con un valor diferente.
4. 🟠 **El Worker no implementa nada del flujo MVP** (queue consumer, agent runner, tool handlers, adapters). El README promete capacidades que no existen aún.
5. 🟠 **CORS configurable a "AllowAnyHeader + AllowAnyMethod"** en cuanto haya ≥1 origen permitido, con `AllowCredentials` no controlado.

### 🎯 Calificación cualitativa

> **Estado: 🟠 Aceptable como base de MVP, REQUIERE refactorización urgente antes de exponerse a producción.**

La base es de nivel senior. Sin embargo, dos hallazgos de seguridad (auth ausente + IDOR via header) y un fallo de concurrencia/DI (singleton ambient context + DbContextPool) **deben corregirse antes** de cualquier despliegue público.

---

## 2. 🔐 Seguridad

### 🔴 Hallazgo S‑01 — Autenticación ausente en endpoints administrativos

**🚨 Descripción del problema:**
En `CeoAgent.ApiService/Program.cs:95` se configura globalmente:

```csharp
app.UseFastEndpoints(options => options.Endpoints.Configurator = endpoint => endpoint.AllowAnonymous());
```

Esto fuerza `AllowAnonymous()` en **todos los endpoints**, incluyendo `/v1/admin/companies`, `/v1/admin/companies/{id}/channels`, `/v1/admin/companies/{id}/agent-profile`, `/v1/admin/companies/{id}/tools` y `/v1/admin/companies/{id}/integration-credentials`. El test `AdminEndpointAccessTests.AdminEndpoint_WithoutAuthentication_AllowsRequest` (Api.Tests) **documenta este comportamiento como esperado**, lo que indica que la decisión es consciente pero contradice el `README.md` ("Los endpoints admin usan API key estática en el MVP") y el principio de defensa en profundidad. Viola **OWASP API1:2023 — Broken Object Level Authorization** y **OWASP API2:2023 — Broken Authentication**.

**🔥 Impacto potencial:**
En producción, cualquier atacante con conocimiento del esquema (publicado en `/scalar` en desarrollo, y posiblemente filtrable por sondeo) puede:

- Crear empresas tenants nuevas con `POST /v1/admin/companies`.
- Registrar canales WhatsApp Cloud apuntando a sus propios `phone_number_id` y robar conversaciones legítimas.
- Configurar credenciales arbitrarias (`kv://...`) o cambiar el perfil del agente para inyectar prompts maliciosos.
- Habilitar tools con configuración hostil que serán ejecutadas por el Worker cuando el flujo MVP se complete.
- Escalar el daño combinándolo con el hallazgo S‑02 (IDOR) para tomar control completo de cualquier tenant existente.

**🛠️ Propuesta de Refactorización:**

```csharp
// CeoAgent.ApiService/Infrastructure/Auth/AdminApiKeyAuthenticationHandler.cs
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CeoAgent.ApiService.Infrastructure.Auth;

public sealed class AdminApiKeyOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "AdminApiKey";
    public const string HeaderName = "X-Admin-Api-Key";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class AdminApiKeyAuthenticationHandler(
    IOptionsMonitor<AdminApiKeyOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder)
    : AuthenticationHandler<AdminApiKeyOptions>(options, loggerFactory, urlEncoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AdminApiKeyOptions.HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presented = values.ToString();
        var expected = Options.ApiKey;

        // Comparación timing-safe para evitar ataques side-channel.
        if (expected.Length == 0
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(presented),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API key."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "admin")],
            AdminApiKeyOptions.SchemeName);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            AdminApiKeyOptions.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

En `ApiRegistrations.AddApi`:

```csharp
services
    .AddAuthentication(AdminApiKeyOptions.SchemeName)
    .AddScheme<AdminApiKeyOptions, AdminApiKeyAuthenticationHandler>(
        AdminApiKeyOptions.SchemeName,
        configureOptions: null);

services.AddOptions<AdminApiKeyOptions>()
    .BindConfiguration("Api:AdminAuth")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "Api:AdminAuth:ApiKey must be configured (use Key Vault, not appsettings).")
    .ValidateOnStart();

services.AddAuthorization(options =>
{
    options.AddPolicy("admin", policy => policy
        .AddAuthenticationSchemes(AdminApiKeyOptions.SchemeName)
        .RequireRole("admin"));
});
```

En `Program.cs` reemplazar el bloque global:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(options =>
{
    options.Endpoints.Configurator = endpoint =>
    {
        if (endpoint.Routes is { Length: > 0 } routes
            && routes.Any(route => route.StartsWith("/v1/admin", StringComparison.Ordinal)))
        {
            endpoint.Policies("admin");
        }
        else if (endpoint.Routes?.Any(r => r.StartsWith("/v1/webhooks", StringComparison.Ordinal)) == true)
        {
            endpoint.AllowAnonymous(); // Los webhooks se autorizan por firma HMAC.
        }
        else
        {
            endpoint.AllowAnonymous();
        }
    };
});
```

**✅ Recomendación adicional:**
La API key admin **nunca** debe vivir en `appsettings.json`. Debe inyectarse desde Azure Key Vault (ya hay infraestructura con `AddAzureKeyVault` en `LangfuseEnvironmentExtensions`). Añadir un test que valide explícitamente que `/v1/admin/*` devuelve 401 sin header — el test actual `AdminEndpoint_WithoutAuthentication_AllowsRequest` debe **invertirse** y renombrarse. Documentar el contrato en `docs/security.md` y agregar al menos un rate‑limit más estricto para `/v1/admin/*` (key partition por API key, no por IP).

---

### 🔴 Hallazgo S‑02 — IDOR en `CompanyContextMiddleware` (tenant spoofing por header)

**🚨 Descripción del problema:**
`CompanyContextMiddleware` (`Infrastructure/Company/CompanyContextMiddleware.cs:11‑27`) lee `X-Company-Id` directamente del request sin ninguna validación criptográfica ni autorizativa:

```csharp
if (context.Request.Headers.TryGetValue(HeaderName, out var values)
    && Guid.TryParse(values.FirstOrDefault(), out var companyId))
{
    companyContextAccessor.SetCompany(companyId);
}
```

Luego, `EnsureCompanyIsAccessibleAsync` en cada endpoint solamente verifica `companyContext.CompanyId != companyId` contra el route param, lo cual es **redundante**: ambos provienen del cliente. Cualquier atacante manda `X-Company-Id: <victim-guid>` y `POST /v1/admin/companies/<victim-guid>/...` y pasa el guard porque ambos coinciden. Esto viola **OWASP API1:2023** y rompe el principio de multi‑tenancy enunciado en el README ("La empresa se resuelve por canal, nunca por teléfono del cliente").

**🔥 Impacto potencial:**
En producción significa **takeover completo de cualquier tenant** simplemente conociendo (o adivinando — Guid v7 incluye timestamp visible, lo que reduce entropía) su `companyId`. El atacante puede listar conversaciones, sobreescribir el perfil del agente con un prompt malicioso, registrar canales para hijack de números WhatsApp, o asociar tools a credenciales propias. Combinado con S‑01, todo lo anterior es accesible sin ningún token.

**🛠️ Propuesta de Refactorización:**
El `companyId` debe derivar **siempre** de un claim firmado, no de un header inocente. Para endpoints admin (humanos con API key) el flujo debe ser:

1. La API key se asocia a un **conjunto cerrado de companies permitidas** (tabla `admin_api_key → company_id[]`).
2. El middleware obtiene la lista permitida y exige que el route param `companyId` pertenezca a ella.

```csharp
// CeoAgent.ApiService/Infrastructure/Company/CompanyContextMiddleware.cs
using CeoAgent.Application.Company;
using CeoAgent.Application.Errors;

namespace CeoAgent.ApiService.Infrastructure.Company;

public sealed class CompanyContextMiddleware(
    RequestDelegate next,
    ICompanyContextAccessor companyContextAccessor,
    ILogger<CompanyContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Endpoints públicos (health, webhooks firmados) no requieren company context.
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity is not { IsAuthenticated: true } identity)
        {
            await next(context);
            return;
        }

        var routeCompanyValue = context.Request.RouteValues["companyId"]?.ToString();
        if (!Guid.TryParse(routeCompanyValue, out var routeCompanyId))
        {
            // Endpoints sin route companyId (ej. POST /admin/companies) no fijan contexto.
            await next(context);
            return;
        }

        var allowedCompanies = context.User
            .FindAll("company_id")
            .Select(claim => Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        if (!allowedCompanies.Contains(routeCompanyId))
        {
            logger.LogWarning(
                "Forbidden cross-tenant access attempt. Principal={Principal} RequestedCompanyId={CompanyId}",
                identity.Name, routeCompanyId);
            throw new NotFoundException("company", routeCompanyId); // 404 para no filtrar existencia.
        }

        companyContextAccessor.SetCompany(routeCompanyId);
        try { await next(context); }
        finally { companyContextAccessor.Clear(); }
    }
}
```

Y el handler de auth incluye los `company_id` claim desde la base:

```csharp
var allowedCompanies = await dbContext.AdminApiKeyCompanyGrants
    .AsNoTracking()
    .Where(grant => grant.AdminApiKeyId == apiKeyId)
    .Select(grant => grant.CompanyId)
    .ToArrayAsync();

var claims = new List<Claim> { new(ClaimTypes.Role, "admin") };
claims.AddRange(allowedCompanies.Select(id => new Claim("company_id", id.ToString("D"))));
```

**✅ Recomendación adicional:**
Cambiar `Guid.CreateVersion7()` por `Guid.NewGuid()` para identificadores externos visibles (Guid v7 expone timestamp y reduce entropía contra enumeración). Mantener v7 solo donde la ordenación temporal en índice b‑tree aporte valor (e.g. `Message.Id`, `ToolExecution.Id`). Crear test que confirme: usuario A con grant `[companyA]` recibe `404` al pedir `companyB`. Auditar logs (sink Langfuse/OTel) de cada intento `Forbidden cross-tenant access attempt` con métrica dedicada para detección de abuso.

---

### 🟠 Hallazgo S‑03 — Webhook signature validation no implementada

**🚨 Descripción del problema:**
El README declara "Los webhooks se autorizan por firma HMAC del proveedor" pero **no existe ningún endpoint webhook ni middleware de validación HMAC** en el código. El flujo principal (1) `WhatsApp envía un webhook al API` y (2) `El API valida firma y payload` no está implementado. Esto es una promesa de seguridad documentada pero ausente, lo que tipifica como riesgo de **drift de seguridad** (documentación vs implementación).

**🔥 Impacto potencial:**
Cuando se conecte el webhook, sin validación HMAC `X-Hub-Signature-256` cualquiera puede inyectar mensajes falsos imitando WhatsApp Cloud, contaminar el storage, gatillar invocaciones LLM (costo financiero directo en Azure OpenAI), provocar respuestas automáticas a víctimas, o hacer flooding del Storage Queue para denegar servicio al worker.

**🛠️ Propuesta de Refactorización:**

```csharp
// CeoAgent.ApiService/Modules/Webhooks/WhatsAppSignatureValidator.cs
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace CeoAgent.ApiService.Modules.Webhooks;

internal static class WhatsAppSignatureValidator
{
    private const string SignaturePrefix = "sha256=";

    public static bool IsValid(
        ReadOnlySpan<byte> rawBody,
        ReadOnlySpan<char> presentedSignature,
        ReadOnlySpan<byte> appSecret)
    {
        if (!presentedSignature.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var hexSpan = presentedSignature[SignaturePrefix.Length..];
        Span<byte> expectedHash = stackalloc byte[32];
        Span<byte> presentedHash = stackalloc byte[32];

        if (!Convert.TryFromHexString(hexSpan, presentedHash, out var bytesWritten) || bytesWritten != 32)
        {
            return false;
        }

        HMACSHA256.HashData(appSecret, rawBody, expectedHash);
        return CryptographicOperations.FixedTimeEquals(expectedHash, presentedHash);
    }
}
```

Usado en un endpoint dedicado que lee el body crudo (cuidado: FastEndpoints debe estar configurado para preservar el stream, no leerlo dos veces).

**✅ Recomendación adicional:**
El `appSecret` (Meta App Secret) debe inyectarse desde Key Vault, no `IConfiguration` directo. Loggear (en debug only) `X-Hub-Signature-256`, nunca el body. Añadir test con vectores conocidos del provider. Limitar tamaño de body (`MaxRequestBodySize` < 4 MB) y rechazar `Content-Length` faltante.

---

### 🟠 Hallazgo S‑04 — `GlobalExceptionHandler` puede filtrar detalles internos

**🚨 Descripción del problema:**
`GlobalExceptionHandler.cs:60` adjunta `exception.Message` al `ProblemDetails.Detail` solo si la excepción es `BusinessRuleException` o `NotFoundException`. Es seguro **siempre que** las implementaciones de estos tipos no leaken estados internos. Sin embargo, `NotFoundException` formatea `$"{resource} {key} not found"`, lo que en producción expone `companyId` GUIDs reales en respuestas. Adicionalmente, el handler `activity.AddException(exception)` adjunta stack trace al span de tracing, que si va a Langfuse/OTel sin filtrado puede exponer paths internos del binario y nombres de assemblies en consoles externas.

**🔥 Impacto potencial:**
Atacantes pueden mapear nombres internos de recursos (`integration_credential_reference`, `company`) y enumerar GUIDs válidos comparando respuestas 404 con detalle vs 404 vacíos. El stack trace en spans expuestos a Langfuse en cuenta cloud externa puede exponer paths como `C:\Users\siemp\source\repos\CeoAgent\...` que filtran rutas de desarrollo y el sistema operativo.

**🛠️ Propuesta de Refactorización:**

```csharp
// Discriminar entornos
var includeDetail = exception switch
{
    BusinessRuleException => true,   // Mensajes diseñados para clientes.
    NotFoundException => false,      // No filtrar tipo de recurso ni clave.
    _ => false,
};

problemDetails.Detail = includeDetail ? exception.Message : null;

// Para tracing: agregar exception solo en dev/staging, redactar stack en prod.
if (Activity.Current is { } activity)
{
    activity.SetStatus(ActivityStatusCode.Error, title);
    activity.SetTag("error.type", type);
    if (env.IsDevelopment() || env.IsStaging())
    {
        activity.AddException(exception);
    }
    else
    {
        activity.SetTag("exception.type", exception.GetType().FullName);
        // No adjuntar stack en producción.
    }
}
```

Y `NotFoundException` debe usar un mensaje genérico:

```csharp
public sealed class NotFoundException(string resource, object key)
    : Exception($"Resource not found.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}
```

**✅ Recomendación adicional:**
Auditar el Langfuse OTLP exporter: por defecto se envía `Authorization` Basic via `options.Headers`. Verificar que ese header **nunca** sea loggeado por OpenTelemetry self‑diagnostics. Documentar en `docs/observability.md` qué campos contienen PII y cómo redactarlos (prompt content, message text, transcript de audio).

---

### 🟡 Hallazgo S‑05 — CORS configurable a un policy peligroso

**🚨 Descripción del problema:**
`ConfigureCorsOptions` aplica `.AllowAnyHeader().AllowAnyMethod()` siempre que haya ≥1 origen permitido. No restringe credenciales, no limita verbos a `GET/POST` (los webhooks solo necesitan `POST`, admin solo `POST/PATCH/DELETE`), y permite cualquier header (incluyendo `Authorization`). Si `AllowedOrigins` se mal configura con `"*"` literal o con `https://*.attacker.com`, la API queda abierta al CORS world.

**🔥 Impacto potencial:**
Con la combinación con S‑01 (no auth), un sitio malicioso embebido por una víctima puede invocar `POST /v1/admin/...` directamente desde el browser sin restricción, incluso si en el futuro se añade autenticación basada en cookies SameSite.

**🛠️ Propuesta de Refactorización:**

```csharp
public void Configure(CorsOptions options)
{
    options.AddPolicy(ApiRegistrations.CorsPolicyName, policy =>
    {
        var allowedOrigins = apiOptions.Value.Cors.AllowedOrigins;

        if (allowedOrigins.Length == 0)
        {
            // Sin orígenes => política restrictiva por defecto.
            policy.DisallowCredentials();
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .WithHeaders("Content-Type", "X-Correlation-Id", "X-Admin-Api-Key")
            .WithMethods("GET", "POST", "PATCH", "DELETE")
            .DisallowCredentials() // Las API keys no son credenciales del browser.
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
}
```

**✅ Recomendación adicional:**
Validar en `ApiOptions.IsValid` que `AllowedOrigins` no contenga `"*"` ni wildcards `"*.dominio"`. Añadir test que confirme respuestas CORS sólo para hosts explícitos.

---

### 🟡 Hallazgo S‑06 — `Refit` sin `AuthorizationMessageHandler` ni rotación

**🚨 Descripción del problema:**
`ProviderRefitClientRegistrations` configura resilience pero **no inyecta credenciales** (tokens WhatsApp Cloud, Google Calendar OAuth). Cuando se conecten adaptadores reales, será fácil olvidar adjuntar el bearer y dejar los clientes Refit sin auth, o peor, hardcodearla. La inyección debe estar centralizada.

**🛠️ Propuesta de Refactorización:**

```csharp
public static IHttpClientBuilder AddWhatsAppCloudRefitClient<TClient>(
    this IServiceCollection services)
    where TClient : class
{
    services.AddTransient<WhatsAppCloudAuthHandler>();

    var builder = services.AddRefitClient<TClient>();
    builder.RemoveAllResilienceHandlers();
    builder.AddHttpMessageHandler<WhatsAppCloudAuthHandler>(); // inyecta Bearer desde credential store
    builder.AddResilienceHandler("whatsapp-cloud", pipeline => { /* ... */ });
    return builder;
}

internal sealed class WhatsAppCloudAuthHandler(
    ICredentialResolver credentials,
    ICompanyContext companyContext)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await credentials.GetWhatsAppCloudTokenAsync(
            companyContext.CompanyId ?? throw new InvalidOperationException("Company context required."),
            cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
```

**✅ Recomendación adicional:**
El `ICredentialResolver` debe leer de Key Vault con caché L1 in‑memory + invalidación TTL. Rotación obligatoria cada 90 días con dual‑write en el periodo de gracia.

---

## 3. ⚡ Performance (Rendimiento)

### 🟠 Hallazgo P‑01 — `ChangeTracker.Entries().ToArray()` en cada `SaveChangesAsync`

**🚨 Descripción del problema:**
`CeoAgentDbContext.StampAuditableEntities` (línea 67) crea un **array nuevo en cada save**: `foreach (var entry in ChangeTracker.Entries().ToArray())`. El `.ToArray()` no es necesario para iteración (el `ChangeTracker` no se modifica durante la enumeración del stamping) y aloca un array `EntityEntry[]` proporcional al número de entradas trackeadas. Bajo carga sostenida (alta concurrencia) esto presiona al Gen0 GC.

**🔥 Impacto potencial:**
En SaaS multi‑tenant con miles de RPS, cada request HTTP/Worker job suele tocar 3‑7 entidades (Conversation + Message + ToolExecution + audit). Eso son N arrays de 3‑7 elementos por segundo. En 10k RPS, son 10k arrays/s, cada uno con `Object[]` reference overhead, presionando el GC Gen0 y aumentando el costo de Azure App Service por escalado.

**🛠️ Propuesta de Refactorización:**

```csharp
private void StampAuditableEntities()
{
    var now = timeProvider.GetUtcNow().UtcDateTime;
    var ambientCompany = companyContext.CompanyId;

    foreach (var entry in ChangeTracker.Entries())
    {
        switch (entry.State)
        {
            case EntityState.Added:
            case EntityState.Modified:
                StampEntry(entry, now, ambientCompany);
                break;
        }
    }
}

private static void StampEntry(EntityEntry entry, DateTime now, Guid? ambientCompany)
{
    if (entry.Entity is Conversation
        && entry.State == EntityState.Modified
        && entry.Property(nameof(Conversation.AgentProfileId)).IsModified)
    {
        throw new InvalidOperationException("Conversation.AgentProfileId is immutable after conversation creation.");
    }

    if (entry.Entity is AuditableCompanyOwnedEntity companyOwned)
    {
        if (companyOwned.CompanyId == Guid.Empty && ambientCompany is { } companyId)
        {
            companyOwned.CompanyId = companyId;
        }
        companyOwned.UpdatedAt = now;
        if (entry.State == EntityState.Added)
        {
            companyOwned.CreatedAt = now;
        }
    }
    else if (entry.Entity is Company company)
    {
        company.UpdatedAt = now;
        if (entry.State == EntityState.Added)
        {
            company.CreatedAt = now;
        }
    }
}
```

**✅ Recomendación adicional:**
Benchmark con BenchmarkDotNet antes/después; meta: 0 allocations en stamping para entradas comunes. Considerar interfaz marker `IAuditable` con `Stamp(DateTime now)` para evitar `is` chain.

---

### 🟠 Hallazgo P‑02 — `HasJsonbConversion` reserializa para `ValueComparer`

**🚨 Descripción del problema:**
`JsonPropertyBuilderExtensions.HasJsonbConversion` define el comparer así:

```csharp
var comparer = new ValueComparer<TProperty>(
    (left, right) => AreEqual(left, right),           // Serialize ambos a JSON
    value => GetJsonHashCode(value),                  // Serialize a JSON, hash del string
    value => Clone(value)!);                          // Serialize + Deserialize
```

Cada operación de comparison o snapshot **serializa el documento completo a JSON con `JsonSerializerOptions.Web`**, sin caching. EF Core invoca `Snapshot()` (clone) en cada `Attach`/`Add`/`SaveChanges` para entidades modificadas, y `Equals` para detectar cambios. Para `WorkingHours`, `ChannelMetadata`, `MessagePayload`, `ToolExecutionRequest`, etc., esto significa **doble serialización por entidad por save**. La `Web` instance se crea con `Converters = { new JsonStringEnumConverter() }`, lo que adicionalmente alocea el `JsonSerializerOptions` interno la primera vez.

**🔥 Impacto potencial:**
Workers consumiendo Storage Queue procesan ~100 mensajes/s pico. Cada mensaje insertado serializa `MessagePayload` 2 veces. Si el payload es 4 KB JSON, son 800 KB/s solo en clone/equals operations. Con `JsonSerializerDefaults.Web` y `JsonStringEnumConverter` nuevos cada vez (note: la `JsonOptions` field es estática, eso está bien), pero serializar a `string` aloca StringBuilder + char arrays — todo presiona Gen1.

**🛠️ Propuesta de Refactorización:**
Usar el soporte nativo de Npgsql para `jsonb` con `JsonDocument`/`OwnedJsonProperty` o, mejor aún, EF Core 10 ya soporta complex properties con `.ToJson()` (ya usado en `CompanyChannelConfiguration`). Para el resto, usar `JsonValueReaderWriter` (.NET 8+) que evita ida/vuelta string:

```csharp
public static PropertyBuilder<TProperty> HasJsonbConversion<TProperty>(
    this PropertyBuilder<TProperty> builder,
    string columnName) where TProperty : class
{
    var converter = new ValueConverter<TProperty, string?>(
        value => Serialize(value),
        value => Deserialize<TProperty>(value)!);

    // Comparer estructural sin reserialización: usar reflection-cached equality o deep-clone vía MemoryPack.
    var comparer = new ValueComparer<TProperty>(
        equalsExpression: (left, right) => ReferenceEquals(left, right) || JsonStructuralEquals(left, right),
        hashCodeExpression: value => value == null ? 0 : RuntimeHelpers.GetHashCode(value),
        snapshotExpression: value => value); // Inmutable por convención: el handler nunca muta el doc en sitio.

    return builder
        .HasConversion(converter, comparer)
        .HasColumnName(columnName)
        .HasColumnType("jsonb");
}
```

Y enforzar por convención (analyzer custom) que los `JsonDocument` payload classes **son inmutables** (records o setters `init`).

**✅ Recomendación adicional:**
Migrar `WorkingHours`, `MessagePayload`, etc. a `record` con `init` y constructor primario para garantizar inmutabilidad estructural. Considerar `MemoryPack` o `Utf8Json` si el payload pesa más en throughput que en correctness ergonomy.

---

### 🟡 Hallazgo P‑03 — `WithDefaultTracking()` siempre evalúa branch

**🚨 Descripción del problema:**
`QueryTrackingExtensions.WithDefaultTracking` impone un branch por query y agrega un nivel de indirection. El parámetro `trackChanges` con default `false` es razonable, pero hay 14+ usos en endpoints que repiten la decisión. Esto es más un punto de **mantenibilidad** que de performance, pero combinado con el siguiente issue (sin `AsNoTracking()` por default) en `DbContextPool` puede llevar a memory bloat del change tracker cuando se olvida.

**🛠️ Propuesta de Refactorización:**

```csharp
// En InfrastructureRegistrations.AddInfrastructure, fijar NoTracking global:
services.AddDbContextPool<CeoAgentDbContext>((provider, options) =>
{
    /* ... existing ... */
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
});
```

Y `WithDefaultTracking(trackChanges: true)` queda explícito solo cuando se necesita.

**✅ Recomendación adicional:**
Benchmark Gen0 allocations en endpoints típicos con y sin NoTracking default.

---

### 🟡 Hallazgo P‑04 — `EnsureCompanyIsAccessibleAsync` ejecuta una query extra

**🚨 Descripción del problema:**
Cada endpoint con route `companyId` ejecuta primero un `AnyAsync(entity => entity.Id == companyId)` solo para validar existencia, **antes** del query útil. Para `ConfigureAgentProfileEndpoint` son 2 queries (`Company.AnyAsync` + `AgentProfile.FirstOrDefaultAsync`), para `EnableCompanyToolEndpoint` son 3 (`Company.AnyAsync` + `Credential.AnyAsync` + `CompanyTool.FirstOrDefaultAsync`). Esto duplica round‑trips a PostgreSQL.

**🛠️ Propuesta de Refactorización:**
Después de implementar S‑02 (claim‑based company validation), el `EnsureCompanyIsAccessibleAsync` se vuelve **innecesario**: el middleware ya valida ownership contra los `company_id` claims, y los filtros globales EF garantizan aislamiento. Una vez en producción, eliminar todos los `EnsureCompanyIsAccessibleAsync` y confiar en query filters.

**✅ Recomendación adicional:**
Tests de integración deben cubrir: si `companyContext` está vacío, las queries de entidades `CompanyOwned` retornan vacío (ya cubierto por `CompanyIsolationTests.CompanyQueryFilter_WhenCompanyContextMissing_ReturnsNoCompanyOwnedRows`).

---

### 🟡 Hallazgo P‑05 — `Worker` ejecuta `Task.Delay(1 min)` sin trabajo real

**🚨 Descripción del problema:**
`Worker.cs:13` solo loguea y espera. Esto es un placeholder, pero **consume tiempo de CPU y un thread de hosted service** sin hacer trabajo útil. Bajo el modelo de Container Apps con `min replicas = 1`, esto es facturación pura sin valor.

**🔥 Impacto potencial:**
Cuando se implemente el flujo real (queue consumer), el patrón actual no es escalable: un único hosted service no puede procesar N mensajes en paralelo de manera natural. Hay riesgo de que el equipo replique este patrón antinaturalmente.

**🛠️ Propuesta de Refactorización:**
Estructurar el consumidor con paralelismo configurable y backoff cuando la cola esté vacía:

```csharp
public sealed class QueueWorker(
    QueueServiceClient queueServiceClient,
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    ILogger<QueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueClient = queueServiceClient.GetQueueClient(options.Value.QueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        var degreeOfParallelism = options.Value.MaxConcurrency;
        var emptyBackoff = TimeSpan.FromSeconds(2);

        await Parallel.ForEachAsync(
            ProcessMessagesAsync(queueClient, emptyBackoff, stoppingToken),
            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism, CancellationToken = stoppingToken },
            async (message, cancellationToken) =>
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<IIncomingMessageHandler>();
                try
                {
                    await handler.HandleAsync(message, cancellationToken);
                    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed processing message {MessageId}", message.MessageId);
                    // Visibility timeout already protege; deadletter después de N reintentos.
                }
            });
    }
    /* … */
}
```

**✅ Recomendación adicional:**
Adoptar pattern de `IServiceScopeFactory` para scopear el `DbContext` por mensaje. Asegurar que cada mensaje establezca su propio `companyContext` desde el payload, no del HTTP middleware.

---

## 4. 📈 Problemas de Escalabilidad

### 🔴 Hallazgo E‑01 — `Singleton ICompanyContextAccessor` + `DbContextPool` + `AsyncLocal` = anti‑patrón crítico

**🚨 Descripción del problema:**
`InfrastructureRegistrations.cs:25‑26` registra `ICompanyContextAccessor` y `ICompanyContext` como **Singleton**. El accessor usa `AsyncLocal<Guid?>`. El `DbContext` recibe `ICompanyContext` por DI y lo captura en los **delegates de `HasQueryFilter`** durante `OnModelCreating`. Combinado con `AddDbContextPool` (línea 29), donde EF Core **reusa instancias del DbContext** entre requests para evitar la alocación, se generan los siguientes problemas:

1. **`OnModelCreating` se ejecuta una sola vez** por modelo. Los delegates de los query filters capturan la **misma instancia singleton** de `ICompanyContext`. `AsyncLocal` mitiga la fuga **solo si todos los consumers respetan el flow context**. En background services, `IHostedService.StartAsync` y `Task.Run` desde código manual rompen el flow, causando que el `DbContext` reutilizado vea `CompanyId == null` y aplique el filtro `false`.
2. **El pool puede entregar la misma instancia a dos requests concurrentes con diferentes `AsyncLocal` ambient contexts**. Mientras la query aún se ejecuta, los filtros son re‑evaluados por EF (los filtros son delegates capturando el accessor por referencia). En la práctica EF Core compila el filtro la primera vez, pero la re‑evaluación del valor `CompanyId` ocurre en cada query — sin embargo el caching del query plan asume que `CompanyId` no cambia entre queries con el mismo shape, lo que **puede provocar cache hits incorrectos** en escenarios de borde.
3. La `ICompanyContextAccessor.Clear()` se llama en el `finally` del middleware, pero **una excepción no manejada antes de `await next(context)`** puede dejar el contexto sucio para la próxima request (mitigado por `AsyncLocal`, pero frágil).

**🔥 Impacto potencial:**
Bajo alta concurrencia en producción: queries que **devuelven datos de otra empresa** o **devuelven cero filas** intermitentemente, sin patrón reproducible. Es exactamente la clase de bug que causa filtración multi‑tenant silenciosa — el peor escenario para un SaaS. Cuando el Worker procese mensajes en paralelo (P‑05), el problema se agrava porque cada mensaje setea su propio `companyContext` pero comparten el pool de `DbContext`.

**🛠️ Propuesta de Refactorización:**
Tres cambios coordinados, en orden:

```csharp
// 1) ICompanyContextAccessor pasa a SCOPED, no singleton.
services.AddScoped<ICompanyContextAccessor, CompanyContextAccessor>();
services.AddScoped<ICompanyContext>(provider => provider.GetRequiredService<ICompanyContextAccessor>());

// 2) CompanyContextAccessor pasa a instance state, no AsyncLocal.
public sealed class CompanyContextAccessor : ICompanyContextAccessor
{
    public Guid? CompanyId { get; private set; }
    public void SetCompany(Guid companyId) => CompanyId = companyId;
    public void Clear() => CompanyId = null;
}

// 3) Reemplazar DbContextPool por DbContextFactory para Worker, mantener pool sólo para API si el accessor es realmente scoped-safe.
// Para el API (HTTP request = scope), DbContextPool con accessor scoped funciona porque EF Core inyecta el scoped service en cada checkout.
services.AddDbContextPool<CeoAgentDbContext>(/* ... */);
// Para el Worker, usar IDbContextFactory:
services.AddPooledDbContextFactory<CeoAgentDbContext>(/* ... */);
```

Y en el Worker, por mensaje:

```csharp
await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
var companyContext = new CompanyContextAccessor();
companyContext.SetCompany(payload.CompanyId);
// Pasar companyContext explícito al DbContext via constructor variant.
```

**Alternativa robusta:** abandonar el accessor injectado al `DbContext` y, en su lugar, **pasar `companyId` como parámetro explícito** a cada repository/method y aplicar `Where(e => e.CompanyId == companyId)` manualmente. Es más verboso pero elimina por completo la clase de bug ambient‑context vs pool.

**✅ Recomendación adicional:**
Test de carga con `NBomber` o `k6`: 1000 requests concurrentes mezclando 50 empresas, validar que cada respuesta solo contenga datos de su empresa. Añadir test `CompanyIsolationTests.PooledDbContext_WithConcurrentCompanyContexts_DoesNotLeak` que arranca 100 tasks paralelas con `companyContext` diferentes y verifica que ninguna ve filas de otra. Documentar la decisión en `docs/architecture/tenancy.md`.

---

### 🟠 Hallazgo E‑02 — Storage Queue sin DLQ ni poison handling

**🚨 Descripción del problema:**
`AppHost.cs:13` crea un único `queues` storage account. No hay declaración de **dead‑letter queue**, ni configuración de `MaxDequeueCount`, ni tabla de poison messages. Azure Storage Queues (a diferencia de Service Bus) no tiene DLQ nativa; debe implementarse manualmente moviendo mensajes con dequeue count > N a una cola `*-poison`.

**🔥 Impacto potencial:**
En producción, un mensaje que falla consistentemente (e.g. payload corrupto, error en la lógica del agente, integración rota) se reintenta indefinidamente hasta `MaxDequeueCount`, luego desaparece silenciosamente. No hay forma de inspeccionar, replayar ni diagnosticar. Mensajes legítimos pueden perderse durante incidentes de proveedor (WhatsApp Cloud down).

**🛠️ Propuesta de Refactorización:**
Declarar las dos colas y un handler genérico:

```csharp
// AppHost
var queues = storage.AddQueues("queues");
// CeoAgent.Worker — Worker bootstrap:
await queueClient.CreateIfNotExistsAsync();
await poisonClient.CreateIfNotExistsAsync();
// Por mensaje:
if (message.DequeueCount > options.MaxDequeueCount)
{
    await poisonClient.SendMessageAsync(message.MessageText);
    await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
    logger.LogError("Message {MessageId} sent to poison queue after {Count} attempts.", message.MessageId, message.DequeueCount);
    return;
}
```

**✅ Recomendación adicional:**
Considerar migrar a **Azure Service Bus** (DLQ nativa, sesiones, `ScheduledEnqueueTime`, FIFO por sesión = ideal para mantener orden de mensajes WhatsApp por conversación). Service Bus también tiene precio fijo más predecible que Storage Queue para volúmenes > 10M ops/mes.

---

### 🟠 Hallazgo E‑03 — `JsonbConversion` global ignora `ConcurrencyToken`

**🚨 Descripción del problema:**
Las entidades no tienen ningún `ConcurrencyToken` (ni `RowVersion`, ni `xmin` de PostgreSQL). En multi‑tenant SaaS con conversaciones simultáneas (un cliente envía 3 mensajes en 5s, el worker procesa en paralelo), no hay protección optimista contra writes concurrentes. El test `/_test/concurrency` mapea `DbUpdateConcurrencyException` a 409, demostrando que se anticipa el caso, pero **no se ha implementado el mecanismo que lo provoque**.

**🔥 Impacto potencial:**
Race conditions en `Conversation.Status` (cliente cierra, agente reabre simultáneamente), `ConversationState.Snapshot` (lost update), `Company.WorkingHours` (admin actualiza desde dos sesiones). Los datos quedan corruptos sin error visible.

**🛠️ Propuesta de Refactorización:**
Habilitar el `xmin` de PostgreSQL como concurrency token automático:

```csharp
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        /* ... */
        builder.UseXminAsConcurrencyToken();
    }
}
```

Hacerlo de manera **transversal** vía convención en `OnModelCreating`:

```csharp
foreach (var entityType in modelBuilder.Model.GetEntityTypes()
    .Where(et => typeof(AuditableCompanyOwnedEntity).IsAssignableFrom(et.ClrType)))
{
    modelBuilder.Entity(entityType.ClrType).UseXminAsConcurrencyToken();
}
```

**✅ Recomendación adicional:**
Cliente debe ser educado para reintentar 409 con backoff (especialmente desde el Worker). Añadir test que provoque `DbUpdateConcurrencyException` real (no `/__test/concurrency` simulado).

---

### 🟡 Hallazgo E‑04 — `Conversation` unique index `Status = 'Open'` puede causar contención

**🚨 Descripción del problema:**
`ConversationConfiguration.cs:14` crea índice único sobre `(CompanyId, CustomerId, CompanyChannelId) WHERE Status = 'Open'`. Esto es excelente para garantizar 1 conversación abierta por cliente/canal, pero bajo concurrencia (mensaje entrante mientras un agente humano cierra la conversación), genera **deadlocks** o `UniqueViolationException` que debe traducirse a `BusinessRuleException`.

**🔥 Impacto potencial:**
En producción restaurantes (peak hour: 50 mensajes/minuto), si dos webhooks WhatsApp del mismo cliente llegan en < 50ms, ambos workers intentan crear `Conversation` con `Status=Open` y uno falla con `23505 unique_violation`. Sin manejo dedicado, el segundo se va a poison queue.

**🛠️ Propuesta de Refactorización:**
Encapsular la creación de conversaciones en un command con upsert‑style flow:

```csharp
// Patrón "find or create" idempotente con SaveChanges en bucle de retry.
public async Task<Conversation> GetOrCreateOpenConversationAsync(/*...*/)
{
    for (var attempt = 0; attempt < 3; attempt++)
    {
        var existing = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId
                && c.CustomerId == customerId
                && c.CompanyChannelId == channelId
                && c.Status == ConversationStatus.Open,
                cancellationToken);
        if (existing is not null) return existing;

        var draft = new Conversation { /*...*/ };
        dbContext.Conversations.Add(draft);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return draft;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            dbContext.Entry(draft).State = EntityState.Detached;
            // Otra task ya la creó. Reintentar el read.
        }
    }
    throw new BusinessRuleException("conversation_creation_conflict", "Could not establish open conversation.");
}
```

**✅ Recomendación adicional:**
Para escalas mayores, considerar `INSERT ... ON CONFLICT DO NOTHING RETURNING *` (PostgreSQL upsert) ejecutado via `ExecuteSqlInterpolatedAsync` cuando la complejidad lo justifique.

---

### 🟡 Hallazgo E‑05 — Health checks no incluyen storage queues / blobs

**🚨 Descripción del problema:**
`Program.cs:38‑40` solo añade `AddNpgSql` al health pipeline. Storage Queue y Blob son dependencias críticas — si están caídas, el flujo MVP no puede procesar mensajes. El endpoint `/health` reportará "healthy" mientras la app está completamente rota.

**🛠️ Propuesta de Refactorización:**

```csharp
builder.Services
    .AddHealthChecks()
    .AddNpgSql(postgresConnectionString, name: "postgresql", tags: ["ready", "live"])
    .AddAzureQueueStorage(name: "queues", tags: ["ready"])
    .AddAzureBlobStorage(name: "blobs", tags: ["ready"]);

// /alive = solo "live" tag (proceso responde)
// /ready = todos los checks (dependencias arriba)
app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
```

**✅ Recomendación adicional:**
Configurar Container Apps `livenessProbe = /alive` (corto, 1s) y `readinessProbe = /ready` (más generoso, 5s) para evitar ciclos de restart por hiccups transitorios de Postgres.

---

## 5. 📊 Observabilidad y Telemetría

### 🟢 Aspectos positivos

- **OpenTelemetry bien configurado**: traces (`Microsoft.AgentFramework*`, `Microsoft.Extensions.AI*`, `CeoAgent.*`), metrics (AspNetCore + HttpClient + Runtime), logs (con `IncludeScopes` y `ParseStateValues`).
- **Langfuse OTLP exporter** dedicado con auth Basic, separable del OTLP general.
- **Correlation ID middleware** con `Activity.Current?.SetTag("correlation_id", ...)` y propagación al response header.
- **ZLogger con `UseJsonFormatter`** = logs estructurados de alto rendimiento.
- **Filtro de spans** excluye `/health` y `/alive` (línea 82‑85 de `Extensions.cs`), evitando ruido en Langfuse/OTel.

### 🟠 Hallazgo O‑01 — `Worker` no configura ZLogger con JSON formatter

**🚨 Descripción del problema:**
`CeoAgent.Worker/Program.cs:8` invoca `builder.Logging.AddZLoggerConsole()` **sin** `UseJsonFormatter()`, mientras el API lo hace. Esto resulta en logs no estructurados del worker, dificultando correlación cross‑service en Azure Log Analytics.

**🛠️ Propuesta de Refactorización:**

```csharp
builder.Logging.AddZLoggerConsole(options =>
{
    options.IncludeScopes = true;
    options.UseJsonFormatter();
});
```

Mejor aún: **extraer la configuración logging a un helper en `ServiceDefaults`**:

```csharp
public static TBuilder AddConfiguredLogging<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.Logging.ClearProviders();
    builder.Logging.AddZLoggerConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseJsonFormatter();
    });
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.ParseStateValues = true;
    });
    return builder;
}
```

Y llamarlo desde `AddServiceDefaults` para que API y Worker compartan logging coherente.

**✅ Recomendación adicional:**
Añadir test que arranque API y Worker, capture logs, y verifique formato JSON parseable.

---

### 🟠 Hallazgo O‑02 — Logging del `GlobalExceptionHandler` sin nivel de detalle adecuado

**🚨 Descripción del problema:**
El handler loguea con `LogError` para todas las excepciones excepto `OperationCanceledException`. Las `BusinessRuleException` y `NotFoundException` son **excepciones esperadas** (4xx, no 5xx), pero generan stack traces costosos en Application Insights y disparan alertas falsas. Las `DbUpdateConcurrencyException` y `IntegrationException` son **errores transitorios**: degradan al usuario pero no exigen pager.

**🛠️ Propuesta de Refactorización:**

```csharp
var logLevel = exception switch
{
    NotFoundException => LogLevel.Information,           // 404 = ruta esperada
    BusinessRuleException => LogLevel.Information,       // 422 = validación de negocio
    DbUpdateConcurrencyException => LogLevel.Warning,    // 409 = race conditions normales
    IntegrationException => LogLevel.Warning,            // 503 = proveedor caído transitoriamente
    OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => LogLevel.Debug,
    _ => LogLevel.Error,                                  // 500 = bug nuestro
};

logger.Log(logLevel, exception,
    "Request failed with status {StatusCode}. CorrelationId: {CorrelationId}",
    status, correlationIdAccessor.CorrelationId);
```

**✅ Recomendación adicional:**
Configurar reglas de log scrubbing en Langfuse para no enviar contenido de `Message.Text` ni `Transcript` a la cuenta cloud externa.

---

### 🟡 Hallazgo O‑03 — Métricas custom de negocio ausentes

**🚨 Descripción del problema:**
La aplicación instrumenta runtime/AspNetCore/HttpClient pero **no expone métricas de negocio**: mensajes/segundo por canal, latencia agent runner, tokens consumidos por modelo, tool execution latency, queue depth. Estos son los KPIs que importan para SLA y costo.

**🛠️ Propuesta de Refactorización:**

```csharp
public static class CeoAgentMeter
{
    public const string MeterName = "CeoAgent.Business";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> MessagesIngested = Meter.CreateCounter<long>(
        "CeoAgent.messages.ingested",
        unit: "messages",
        description: "Inbound messages persisted to the conversational ledger.");

    public static readonly Histogram<double> AgentRunDurationMs = Meter.CreateHistogram<double>(
        "CeoAgent.agent.run_duration",
        unit: "ms",
        description: "Time to complete an agent run from queue dequeue to outbound message.");

    public static readonly Counter<long> ToolExecutionsByOutcome = Meter.CreateCounter<long>(
        "CeoAgent.tool.executions",
        description: "Tool executions by tool key and outcome.");
}
```

Y registrarlo en `ConfigureOpenTelemetry`:

```csharp
.WithMetrics(metrics =>
{
    metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(CeoAgentMeter.MeterName);
    /* ... */
});
```

**✅ Recomendación adicional:**
Dashboards en Application Insights / Grafana para: latencia P50/P95/P99 por endpoint, costo USD/día por modelo LLM, tool error rate.

---

### 🟡 Hallazgo O‑04 — Health checks tags inconsistentes

**🚨 Descripción del problema:**
`ServiceDefaults.AddDefaultHealthChecks` añade un check `"self"` con tag `"live"`. Pero `AddNpgSql` en `Program.cs` no tagea el check, por lo que `/alive` (filtrado por tag `"live"`) **no** revisa Postgres — eso es correcto para liveness (proceso vivo ≠ dependencia arriba) pero **no se documenta**.

**🛠️ Propuesta de Refactorización:**
Establecer convención explícita y centralizar:

```csharp
.AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);
.AddAzureQueueStorage(/*...*/, tags: ["ready"]);
```

Y mapear:

```csharp
app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
```

**✅ Recomendación adicional:**
Documentar en `docs/operations/health-checks.md` la diferencia liveness vs readiness.

---

## 6. 🧵 Concurrencia y Thread Safety

### 🔴 Hallazgo C‑01 — Singleton `CompanyContextAccessor` con `AsyncLocal<T>`

> Ver en detalle hallazgo **E‑01**. Es a la vez un problema de escalabilidad y de thread‑safety: el `AsyncLocal` **no es seguro** cuando el código background atraviesa `Task.Run` sin capturar `ExecutionContext`, ni cuando el `DbContextPool` retorna instancias compartidas con filtros precompilados. El test `CompanyIsolationTests.CompanyQueryFilter_WhenCompanyContextMissing_ReturnsNoCompanyOwnedRows` cubre el escenario null pero **no** cubre concurrencia real entre dos contexts diferentes con el mismo pool.

---

### 🟠 Hallazgo C‑02 — `CorrelationIdMiddleware.GetOrCreateCorrelationId` validación del header demasiado laxa

**🚨 Descripción del problema:**
El middleware acepta cualquier valor con `Length > 0`:

```csharp
if (context.Request.Headers.TryGetValue(HeaderName, out var values)
    && values is [{ Length: > 0 } id])
{
    return id;
}
```

Esto es un **vector de log injection**: un atacante envía `X-Correlation-Id: "</script><img src=x onerror=...>"` (o similar para sistemas de logging que renderizan en consoles HTML) o `X-Correlation-Id: "12345\n[ERROR] Fake admin log entry"` que se inyecta en logs ZLogger JSON si no se sanitiza al stringificar.

**🔥 Impacto potencial:**
Logs envenenados por atacantes para confundir investigaciones forenses. Si Langfuse/AppInsights renderizan el correlation id en UI sin sanitizar (caso común), pueden producir XSS sobre operadores.

**🛠️ Propuesta de Refactorización:**

```csharp
private static string GetOrCreateCorrelationId(HttpContext context)
{
    if (context.Request.Headers.TryGetValue(HeaderName, out var values)
        && values is [{ Length: <= 64 } presented]
        && IsSafeCorrelationId(presented))
    {
        return presented;
    }

    Span<char> buffer = stackalloc char[36];
    Guid.CreateVersion7().TryFormat(buffer, out _, format: "D");
    return new string(buffer);
}

private static bool IsSafeCorrelationId(ReadOnlySpan<char> value)
{
    foreach (var character in value)
    {
        if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
        {
            return false;
        }
    }
    return value.Length > 0;
}
```

**✅ Recomendación adicional:**
Aplicar la misma sanitización a `X-Company-Id` si se mantiene el patrón header‑based en algún flujo legítimo.

---

### 🟡 Hallazgo C‑03 — `Worker` no protege concurrencia compartida

**🚨 Descripción del problema:**
El worker actual no comparte estado, pero la próxima iteración (queue consumer paralelo) lo hará. Sin guidelines explícitos, es probable que se usen `Dictionary<>` shared, `static` mutable fields, o un `HttpClient` por mensaje (anti‑patrón). Esto es un riesgo arquitectónico de futuro, no actual.

**🛠️ Propuesta de Refactorización:**
Documentar en `docs/architecture/concurrency.md`:

- Estado por mensaje: scoped `IServiceScope`, nunca singleton.
- `HttpClient` siempre via `IHttpClientFactory` (ya viene de Refit, garantizado por `AddRefitClient`).
- Caches in‑memory: usar `MemoryCache` con `SizeLimit` y eviction policy, no `Dictionary<>`.
- Locks: preferir `Channel<T>` y `SemaphoreSlim` sobre `lock` en código async.

**✅ Recomendación adicional:**
Análisis estático con `Microsoft.VisualStudio.Threading.Analyzers` para detectar `.Result`, `.Wait()`, `async void`, y locks dentro de `async` blocks. Ya hay `Meziantou.Analyzer` y `Roslynator`, pero `vs‑threading` aporta reglas únicas para concurrencia.

---

### 🟡 Hallazgo C‑04 — `JsonbConversion.JsonOptions` reutilizado entre threads

**🚨 Descripción del problema:**
`JsonSerializerOptions` es thread‑safe **solo después** del primer uso (cuando se "freezea" internamente). El field estático `JsonOptions` está bien, pero **`JsonSerializerOptions.Web` clonado en cada llamada a `HasJsonbConversion`** crearía una opción no freezeada en cada conversion. Aquí está OK porque es estático, pero hay un caso similar en `JsonElementMappingExtensions.SerializerOptions` (también estático): correcto, pero frágil ante futuras refactorizaciones.

**🛠️ Propuesta de Refactorización:**
Aplicar `.MakeReadOnly()` explícitamente:

```csharp
private static readonly JsonSerializerOptions JsonOptions;

static JsonPropertyBuilderExtensions()
{
    JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    JsonOptions.MakeReadOnly();
}
```

**✅ Recomendación adicional:**
Marcar como `[ThreadSafe]` (custom attribute o XML doc) los singletons usados en hot path.

---

## 7. 🏗️ Cumplimiento Arquitectónico y Acoplamiento

### 🟢 Aspectos positivos

- Direccionalidad correcta general: `Shared` → `Application` → `Infrastructure` → `ApiService/Worker`.
- `Integrations` aislado como contratos puros (puertos).
- `Adapters` separado para implementaciones (helpers Refit, no lógica de dominio).
- Mappers Mapperly **por módulo** (`Modules/Companies/Mappers/CompanyMapper.cs`), no mapper global.
- `Tools` y `Worker` separados — la lógica del agente irá en su propio bounded context.
- `BannedSymbols.txt` enforce `TimeProvider` en lugar de `DateTime.Now`.
- Validators FluentValidation embebidos junto al endpoint (vertical slice estilo).

### 🟠 Hallazgo A‑01 — `ApiService` referencia directamente `Infrastructure`

**🚨 Descripción del problema:**
`CeoAgent.ApiService.csproj` referencia `CeoAgent.Infrastructure.csproj`, y los endpoints inyectan `CeoAgentDbContext` directamente (`CreateCompanyEndpoint(CeoAgentDbContext dbContext)`). Esto es Clean Architecture **rota**: la capa de presentación conoce el ORM concreto, no abstracciones. Vertical slice "puro" permite este atajo (Jimmy Bogard lo defiende), pero la mezcla con Clean Architecture es inconsistente: hay `Application` con `ICompanyContext` (abstracción), pero los endpoints usan el DbContext concreto.

**🔥 Impacto potencial:**
Imposible cambiar EF Core por otro ORM (Dapper, Marten) sin tocar endpoints. Imposible testear endpoints con un fake `IRepository<Company>`. Refactorizar el modelo de datos requiere modificar cada endpoint que lo usa. Si más adelante se introduce CQRS con read models separados, los endpoints quedan acoplados al write model.

**🛠️ Propuesta de Refactorización:**
Decidir el estilo y aplicarlo de manera consistente. Hay dos rutas viables:

**Opción A (Vertical Slice puro, mi recomendación dado el MVP):** mover **toda** la lógica a Commands/Queries de Mediator que viven en `ApiService/Modules/*/Commands`. Los endpoints solo orquestan: `sender.Send(command)`. Los handlers acceden al DbContext. Ya está parcialmente hecho: `RegisterCompanyChannelCommand` sigue este patrón. Aplicarlo a los 4 endpoints restantes.

```csharp
public sealed class CreateCompanyEndpoint(ISender sender)
    : Endpoint<CreateCompanyRequest, CompanyResponse>
{
    public override void Configure() => Post("/v1/admin/companies");

    public override async Task HandleAsync(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var response = await sender.Send(CompanyMapper.ToCommand(request), cancellationToken);
        await Send.CreatedAtAsync<CreateCompanyEndpoint>(new { response.Id }, response, cancellation: cancellationToken);
    }
}

internal sealed record CreateCompanyCommand(string Name, string TimeZoneId) : ICommand<CompanyResponse>;

internal sealed class CreateCompanyCommandHandler(CeoAgentDbContext dbContext)
    : ICommandHandler<CreateCompanyCommand, CompanyResponse>
{
    public async ValueTask<CompanyResponse> Handle(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = new Company { Name = command.Name, TimeZoneId = command.TimeZoneId };
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CompanyMapper.ToResponse(company);
    }
}
```

**Opción B (Clean Architecture estricta):** definir `IRepository<Company>` en `Application`, implementar en `Infrastructure`. Más boilerplate, menos pragmático para un MVP. No recomendado.

**✅ Recomendación adicional:**
Documentar en `docs/architecture/style.md` la decisión: "Vertical Slice + handlers Mediator, sin repositorios. Capa Application reserva sólo: abstracciones cross‑cutting (`ICompanyContext`), excepciones de negocio y contratos sin dependencias EF".

---

### 🟠 Hallazgo A‑02 — `Application` casi vacío, lógica fragmentada en `ApiService`

**🚨 Descripción del problema:**
`CeoAgent.Application` contiene **5 archivos**: 3 interfaces de Company context + 3 exception classes. Toda la lógica de negocio vive en los handlers Mediator dentro de `ApiService/Modules/*/Commands`. Esto es coherente con vertical slice, **pero**:

1. El proyecto `CeoAgent.Application` queda como un dumping ground sin propósito claro.
2. Cuando el Worker necesite ejecutar la misma lógica (e.g. crear conversación, persistir mensaje), tendrá que **duplicar** los handlers o referenciar `ApiService.dll`.

**🛠️ Propuesta de Refactorización:**
Tres opciones, en orden de preferencia:

1. **Mover los handlers Mediator compartibles a `CeoAgent.Application`**. Los endpoints (FastEndpoints) quedan en `ApiService`, los commands/queries y sus handlers viven en `Application`. Worker referencia `Application` y reutiliza handlers.

2. **Crear `CeoAgent.UseCases`** (o `CeoAgent.Modules.*`) modular: `CeoAgent.Modules.Conversations`, `CeoAgent.Modules.Companies`, cada uno con sus commands/queries/handlers. Más cercano a modular monolith real.

3. **Aceptar la duplicación** si los flujos son realmente diferentes (admin via HTTP vs worker via queue).

Recomendación: opción 1 para MVP, opción 2 cuando el modelo crezca.

**✅ Recomendación adicional:**
Renombrar `ApplicationAssembly.cs` a algo significativo si se mantiene; eliminar el proyecto si queda vacío.

---

### 🟡 Hallazgo A‑03 — `Worker` no es modular ni testeable

**🚨 Descripción del problema:**
El `Worker` actual es un `BackgroundService` único con lógica embebida en `ExecuteAsync`. No hay separación entre "consumer de queue" y "handler de mensaje". No hay `IIncomingMessageHandler` ni separation entre tipos de mensajes (inbound WhatsApp, retry, tool result callback). Cuando el flujo MVP se complete, este archivo va a crecer a 500+ líneas inmaiziblemente.

**🛠️ Propuesta de Refactorización:**
Estructura modular:

```
CeoAgent.Worker/
├── Hosting/
│   └── QueueWorker.cs          (consumer genérico)
├── Pipeline/
│   ├── IIncomingMessageHandler.cs
│   ├── MessageEnvelope.cs
│   └── MessageDispatcher.cs    (route por tipo)
├── Handlers/
│   ├── InboundMessageHandler.cs
│   ├── ToolExecutionCallbackHandler.cs
│   └── RetryHandler.cs
└── Program.cs                   (composition root)
```

**✅ Recomendación adicional:**
Cada handler debe ser unit‑testable inyectando `IDbContextFactory<CeoAgentDbContext>`, `IAgentRunner`, etc. Mediator también puede usarse aquí (Worker side handlers).

---

### 🟢 Hallazgo A‑04 — Convención de naming inconsistente menor

**🚨 Descripción del problema:**
`CompanyContextMiddleware.HeaderName` es `"X-Company-Id"` y `CorrelationIdMiddleware.HeaderName` es `"X-Correlation-Id"`. Convención HTTP `X-*` está deprecada por RFC 6648, pero es de uso común. Más relevante: el namespace `CeoAgent.*` en `tracing.AddSource("CeoAgent.*")` (línea 79 de `Extensions.cs`) usa PascalCase distinto a `CeoAgent.*` del project namespace. Inconsistente.

**🛠️ Propuesta de Refactorización:**

```csharp
tracing.AddSource("CeoAgent.*");
```

**✅ Recomendación adicional:**
Estandarizar todo a `CeoAgent.*` o migrar el namespace de proyecto a `CeoAgent.*` (preferible PascalCase moderno). Un branch‑wide find/replace coordinado con tests.

---

## 8. 🧪 Testabilidad

### 🟢 Aspectos positivos

- TUnit como framework (paralelización por defecto, sin `[NotInParallel]` cuando hay aislamiento).
- Testcontainers para PostgreSQL real en `CeoAgent.IntegrationTests`.
- `WebApplicationFactory<Program>` para API integration tests (`ApiFactory`).
- `Persistence:UseInMemoryDatabase = true` con `databaseName` único por test = aislamiento limpio.
- `[NotInParallel]` aplicado a `AdminEndpointAccessTests` (que comparte estado de DI).
- Tests cubren multi‑tenancy isolation: `CompanyIsolationTests` con 3 escenarios.

### 🟠 Hallazgo T‑01 — Singleton `CompanyContextAccessor` con `AsyncLocal` rompe paralelización TUnit

**🚨 Descripción del problema:**
Tests TUnit corren en paralelo por defecto. `CompanyContextAccessor` singleton + `AsyncLocal<Guid?>` mitiga el cross‑test contamination cuando los tests respetan `ExecutionContext`, pero:

- `[NotInParallel]` en `AdminEndpointAccessTests` indica que **el autor ya sospechó del problema**.
- Test `CompanyQueryFilter_WhenCompanyContextMissing_ReturnsNoCompanyOwnedRows` usa `companyContext.Clear()` — si la siguiente prueba paralela usa el **mismo** `CompanyContextAccessor` (porque es singleton **per WebApplicationFactory** pero si dos factories comparten algo), hay race.

**🔥 Impacto potencial:**
Tests flaky en CI: pasan localmente, fallan intermitentemente en GitHub Actions / Azure DevOps bajo paralelismo. Erosión de confianza en la suite.

**🛠️ Propuesta de Refactorización:**
Resolver A‑01 / C‑01 (scoped accessor) y eliminar `[NotInParallel]` cuando deje de ser necesario. Añadir un test específico:

```csharp
[Test]
public async Task ConcurrentRequests_WithDifferentCompanyContexts_DoNotCrossContaminate()
{
    await using var factory = new ApiFactory();

    var tasks = Enumerable.Range(0, 50).Select(async _ =>
    {
        using var client = factory.CreateClient();
        var companyId = await CreateCompanyAsync(client, $"Company-{Guid.CreateVersion7()}");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{companyId}/channels")
        {
            Content = JsonContent.Create(new { provider = "whatsapp_cloud", providerChannelId = $"phone-{companyId}" }),
        };
        request.Headers.Add("X-Company-Id", companyId.ToString());

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<CompanyChannelResponse>();
        return (Expected: companyId, Actual: body!.CompanyId);
    });

    var results = await Task.WhenAll(tasks);
    foreach (var (expected, actual) in results)
    {
        actual.ShouldBe(expected); // Falla si hay cross-tenant leakage.
    }
}
```

**✅ Recomendación adicional:**
Configurar GitHub Actions con `--blame --blame-hang-timeout 5m` para detectar tests flaky tempranamente.

---

### 🟠 Hallazgo T‑02 — `InMemoryDatabase` no soporta `jsonb`, `xmin`, índices únicos parciales

**🚨 Descripción del problema:**
`ApiFactory` usa `UseInMemoryDatabase`. EF Core InMemory **no aplica** comportamientos relacionales clave: filtros únicos parciales (e.g. `HasFilter("status = 'Open'")` en `Conversation`), `jsonb` real (la persistencia ignora el converter), transacciones reales, concurrencia optimista. **Tests pasan** en InMemory pero pueden **fallar en producción** con PostgreSQL.

**🔥 Impacto potencial:**
La unique index parcial sobre `Conversation` `WHERE status = 'Open'` no se valida nunca en tests API. Una migración accidental que rompa esa restricción no se detectaría hasta llegar a staging.

**🛠️ Propuesta de Refactorización:**
Adoptar **Testcontainers también en `Api.Tests`** (ya está en `CeoAgent.IntegrationTests`). Usar una `WebApplicationFactory` que arranca Postgres real:

```csharp
internal sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? postgres;

    public async ValueTask InitializeAsync()
    {
        postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CeoAgent", postgres!.GetConnectionString());
        builder.UseSetting("Persistence:UseInMemoryDatabase", "false");
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (postgres is not null) await postgres.DisposeAsync();
    }
}
```

**✅ Recomendación adicional:**
Mantener InMemory **solo** para tests muy rápidos de mapeo/validación (que no toquen comportamiento relacional). Marcar explícitamente cada test con `[Category("integration")]` o `[Category("unit")]` para correr selectivamente en CI.

---

### 🟡 Hallazgo T‑03 — Tests de webhooks/agent runner inexistentes

**🚨 Descripción del problema:**
No hay tests del flujo conversacional principal porque **no está implementado**. El test `RuntimeShellTests` existe pero solo verifica `/health`. Cuando se construya el webhook handler, será fácil olvidar añadir test de signature validation (vector de S‑03).

**🛠️ Propuesta de Refactorización:**
Antes de implementar el flujo, escribir el test contract en `Api.Tests`:

```csharp
[Test]
public async Task WhatsAppWebhook_WithInvalidSignature_Returns401()
{
    /* ... */
}

[Test]
public async Task WhatsAppWebhook_WithValidSignature_EnqueuesMessage()
{
    /* ... */
}
```

TDD strict ayuda especialmente en webhooks con criptografía.

**✅ Recomendación adicional:**
Contract testing con [Pact](https://pact.io) entre Worker y proveedores externos (WhatsApp Cloud, Google Calendar). Stubbing con WireMock.NET para integration tests de adapters.

---

### 🟢 Hallazgo T‑04 — Mappers tienen tests dedicados pero faltan property‑based

**🚨 Descripción del problema:**
`CompanyMapperTests` (no leído pero presente) cubre casos felices. Mapperly genera código `partial`, pero hay branches custom (e.g. `MapWorkingHours` con `JsonElement?` null vs undefined vs valid). Tests basados en ejemplos pueden no cubrir todos los casos extremos.

**🛠️ Propuesta de Refactorización:**
Usar **FsCheck** o **CsCheck** para property‑based testing:

```csharp
[Test]
public void ToResponse_AlwaysRoundtripsThroughJson()
{
    Prop.ForAll<WorkingHours>(workingHours =>
    {
        var company = new Company { /*...*/ WorkingHours = workingHours };
        var response = CompanyMapper.ToResponse(company);
        var deserialized = JsonSerializer.Deserialize<WorkingHours>(response.WorkingHours!.Value);
        deserialized.ShouldBeEquivalentTo(workingHours);
    }).QuickCheck();
}
```

**✅ Recomendación adicional:**
Property tests aplican especialmente bien a mappers, validators, y conversores jsonb.

---

## 9. 🧠 Uso de Sintaxis Moderna y Optimización de Memoria (.NET 11)

### 🟢 Aspectos positivos ya implementados

- `LangVersion = latest`, `Nullable = enable`, `ImplicitUsings = enable`.
- `Guid.CreateVersion7()` para IDs (ordering temporal en índice b‑tree).
- `TimeProvider` enforced via `BannedSymbols.txt`.
- File‑scoped namespaces.
- Primary constructors en clases (`ApiOptions`, `CompanyChannelConfiguration`, handlers Mediator).
- `Span<char>` en `CorrelationIdMiddleware.GetOrCreateCorrelationId` (asignación zero‑allocation del Guid).
- Records sealed para commands.
- Pattern matching extensivo: `is { Length: > 0 } id`, switch expressions.

### 🟠 Hallazgo M‑01 — Excepciones repetidas en hot path: usar `Throw helpers`

**🚨 Descripción del problema:**
`ArgumentNullException.ThrowIfNull(metadata)` en `ChannelMetadata.ForWhatsAppCloud` es excelente. Pero hay throws manuales en otros sitios que podrían usar throw helpers para reducir JIT overhead y mejorar PGO:

```csharp
// Actual:
throw new InvalidOperationException("Conversation.AgentProfileId is immutable after conversation creation.");

// Mejorado: extract a [DoesNotReturn] static helper.
```

Esto solo importa en hot path. En cambio, **un uso más interesante**: las `Throw.ArgumentException` en stamping cuando `companyOwned.CompanyId == Guid.Empty` Y `ambientCompany is null` — actualmente esto **silenciosamente persiste** una entidad con `CompanyId = Guid.Empty`, lo que rompe los filtros globales (la entidad queda invisible a todos). Mejor lanzar inmediatamente.

**🛠️ Propuesta de Refactorización:**

```csharp
private static class ThrowHelpers
{
    [DoesNotReturn]
    public static void CompanyContextMissing() =>
        throw new InvalidOperationException(
            "AuditableCompanyOwnedEntity persisted without a CompanyId and no ambient company context is available.");

    [DoesNotReturn]
    public static void AgentProfileImmutable() =>
        throw new InvalidOperationException("Conversation.AgentProfileId is immutable after conversation creation.");
}

// Usar:
if (companyOwned.CompanyId == Guid.Empty)
{
    if (companyContext.CompanyId is not { } companyId) ThrowHelpers.CompanyContextMissing();
    companyOwned.CompanyId = companyContext.CompanyId.Value; // Nunca llega si lanza.
}
```

**✅ Recomendación adicional:**
`[DoesNotReturn]` permite al compilador eliminar paths inalcanzables. Con `<TieredPGO>true</TieredPGO>` (default en .NET 11), throw helpers se optimizan agresivamente.

---

### 🟠 Hallazgo M‑02 — `JsonElementMappingExtensions.SerializerOptions` no freezeada

**🚨 Descripción del problema:**

```csharp
private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
```

Es estática y se usa concurrentemente. .NET 8+ freeza la instancia al primer uso, pero llamar `.MakeReadOnly()` explícitamente es más claro y previene mutaciones accidentales en futuras refactorizaciones.

**🛠️ Propuesta de Refactorización:**

```csharp
internal static class JsonElementMappingExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.MakeReadOnly();
        return options;
    }
    /* ... */
}
```

**✅ Recomendación adicional:**
Considerar **JSON Source Generation** (`[JsonSerializable]`) para todos los `JsonDocument` payload classes — elimina reflection en serialización y reduce tamaño en Native AOT:

```csharp
[JsonSerializable(typeof(WorkingHours))]
[JsonSerializable(typeof(ChannelMetadata))]
[JsonSerializable(typeof(MessagePayload))]
[JsonSerializable(typeof(ToolConfiguration))]
[JsonSerializable(typeof(CredentialMetadata))]
[JsonSerializable(typeof(ToolExecutionRequest))]
[JsonSerializable(typeof(ToolExecutionResult))]
[JsonSerializable(typeof(ConversationStateSnapshot))]
internal sealed partial class CeoAgentJsonContext : JsonSerializerContext;

// Uso:
JsonSerializer.Serialize(document, CeoAgentJsonContext.Default.WorkingHours);
```

Coherente con el README ("AOT‑aware, priorizando mantenibilidad sin bloquear Native AOT futuro").

---

### 🟡 Hallazgo M‑03 — `Dictionary<DayOfWeek, List<TimeSlot>>` en `WorkingHours` aloca por enum

**🚨 Descripción del problema:**
`WorkingHours.Schedule` usa `Dictionary<DayOfWeek, List<TimeSlot>>`. El enum `DayOfWeek` tiene **7 valores fijos**. Una array de 7 slots `TimeSlot[]?[]` sería más compacto, sin boxing, sin overhead de hashing. Para entidades raramente modificadas (working hours), también se beneficiaría de inmutabilidad.

**🛠️ Propuesta de Refactorización:**

```csharp
public sealed record WorkingHours
{
    private readonly TimeSlot[]?[] schedule = new TimeSlot[7][];

    public ReadOnlySpan<TimeSlot> GetSlots(DayOfWeek day) =>
        schedule[(int)day] ?? [];

    public void SetSlots(DayOfWeek day, ReadOnlySpan<TimeSlot> slots) =>
        schedule[(int)day] = slots.ToArray();

    public List<SpecialDay> Holidays { get; init; } = [];
}
```

**✅ Recomendación adicional:**
Trade‑off: el JSON deserialization de `TimeSlot[]?[]` requiere custom converter. Solo merece la pena si el endpoint de `WorkingHours` es hot path. Para admin endpoints (raros), no justifica.

---

### 🟡 Hallazgo M‑04 — `Guid.CreateVersion7().ToString()` evitar concatenación

**🚨 Descripción del problema:**
Es práctica común escribir `$"channel-{Guid.CreateVersion7()}"` en seeds y tests. Esto aloca un `string` intermedio + `StringBuilder`. Para hot path se prefiere `string.Create`:

```csharp
public static string CreateChannelName()
{
    return string.Create(44, default(int), (span, _) =>
    {
        "channel-".CopyTo(span);
        Guid.CreateVersion7().TryFormat(span[8..], out _, format: "D");
    });
}
```

Solo aplica si hay miles de invocaciones/s. En tests no merece la pena.

**✅ Recomendación adicional:**
No optimizar prematuramente; aplicar solo si benchmark revela hot path.

---

### 🟡 Hallazgo M‑05 — `ICollection<T> = new List<T>()` en entidades

**🚨 Descripción del problema:**

```csharp
public ICollection<CompanyChannel> Channels { get; } = new List<CompanyChannel>();
```

Cada instancia nueva (incluso si no se materializan las colecciones) aloca `List<T>` con default capacity 0 → 4 → 8. EF Core las puebla via reflection. Cuando se rehidratan muchas entidades, la suma de allocations es relevante.

**🛠️ Propuesta de Refactorización:**

```csharp
public ICollection<CompanyChannel> Channels { get; } = [];
```

(Equivalente con `collection expression`, EF Core lo soporta desde 8). O usar lazy `null`‑initialized con backing field:

```csharp
private List<CompanyChannel>? channels;
public ICollection<CompanyChannel> Channels => channels ??= new();
```

**✅ Recomendación adicional:**
Pequeño impacto pero acumulativo. Aplicar uniforme.

---

## 10. ☁️ Optimización de Costos en Azure (FinOps & Resource Efficiency)

### 🟠 Hallazgo $‑01 — `DbContextPool` sin `poolSize` explícito

**🚨 Descripción del problema:**
`AddDbContextPool<CeoAgentDbContext>` usa default pool size 1024. En App Services con bursts de tráfico, este pool aumenta uso de memoria del proceso (cada DbContext en pool retiene change tracker + state). En Container Apps con scaling auto basado en memoria, esto **acelera el scale‑out** y aumenta facturación.

**🛠️ Propuesta de Refactorización:**

```csharp
services.AddDbContextPool<CeoAgentDbContext>(
    (provider, options) => { /* ... */ },
    poolSize: 64); // Tamaño moderado, alineado con max degree of parallelism típico.
```

**✅ Recomendación adicional:**
Métrica `dotnet_dbcontext_pool_active_contexts` (custom) y alarma si el pool está siempre al máximo (señal de tener que aumentar). Recordar: `ICompanyContextAccessor` debe pasar a Scoped (E‑01) **antes** de optimizar el pool.

---

### 🟠 Hallazgo $‑02 — Langfuse OTLP exporter no batch, no retry policy

**🚨 Descripción del problema:**
`AddOtlpExporter(options => { options.Endpoint = ...; })` usa defaults. Sin `BatchExportProcessorOptions` configurado, cada span se envía individualmente, consumiendo más ancho de banda y aumentando egress costs en Azure (Langfuse Cloud está fuera del data center). Sin retry policy, hiccups de red causan spans perdidos (gap en observabilidad LLM = imposible auditar costos del modelo).

**🛠️ Propuesta de Refactorización:**

```csharp
tracing.AddOtlpExporter(options =>
{
    options.Endpoint = langfuseOptions.GetOtlpTracesEndpoint();
    options.Protocol = OtlpExportProtocol.HttpProtobuf;
    options.Headers = $"Authorization=Basic {authString},x-langfuse-ingestion-version=4";
    options.ExportProcessorType = ExportProcessorType.Batch;
    options.BatchExportProcessorOptions = new BatchExportActivityProcessorOptions
    {
        MaxQueueSize = 8192,
        ScheduledDelayMilliseconds = 5000,
        ExporterTimeoutMilliseconds = 30_000,
        MaxExportBatchSize = 512,
    };
});
```

**✅ Recomendación adicional:**
Monitorear `otel_exporter_dropped_spans` metric. Considerar Langfuse self‑hosted si la facturación cloud + egress supera $200/mes.

---

### 🟠 Hallazgo $‑03 — `HttpClient` Refit sin connection pooling configurado

**🚨 Descripción del problema:**
Refit usa `IHttpClientFactory` por default (correcto), pero sin `SocketsHttpHandler.PooledConnectionLifetime` ni `MaxConnectionsPerServer` explícitos. Para llamadas a WhatsApp Cloud (`graph.facebook.com`) y Google Calendar (`www.googleapis.com`) bajo carga, sin pooling explícito hay riesgo de `SocketException: address already in use` (ephemeral port exhaustion) o de mantener conexiones DNS stale (cuando Meta cambia su backend).

**🛠️ Propuesta de Refactorización:**

```csharp
services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),    // refresca DNS
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        MaxConnectionsPerServer = 32,
        EnableMultipleHttp2Connections = true,
    });
    http.AddStandardResilienceHandler();
    http.AddServiceDiscovery();
});
```

**✅ Recomendación adicional:**
Métricas `http.client.active_requests` y `http.client.connection.duration` deben alertarse cuando los proveedores externos están degradados — aborta requests temprano para no pagar tiempo de App Service esperando timeouts.

---

### 🟡 Hallazgo $‑04 — Aspire emulator vs Azure storage costs in development

**🚨 Descripción del problema:**
`storage.RunAsEmulator()` está bien para dev local. Pero si alguien deja `RunAsEmulator()` activo en publish, no se usa el storage real → fallos en producción. Ya hay `ExecutionContext.IsPublishMode` switch en Langfuse; aplicarlo también a storage.

**🛠️ Propuesta de Refactorización:**

```csharp
var storage = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureStorage("storage")
    : builder.AddAzureStorage("storage").RunAsEmulator();
```

**✅ Recomendación adicional:**
Configurar Azure Policy a nivel de subscription que prohíba storage account sin encryption at rest, tagging FinOps obligatorio (cost-center, environment).

---

### 🟡 Hallazgo $‑05 — `IncludeFormattedMessage = true` duplica payload de logs

**🚨 Descripción del problema:**
OTel logging con `IncludeFormattedMessage = true` envía **ambos** el template `"Request failed with status {StatusCode}"` y el mensaje formateado `"Request failed with status 500"`. En Application Insights / Log Analytics esto duplica el storage cost por log entry.

**🛠️ Propuesta de Refactorización:**

```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = false; // El template + state values bastan.
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
});
```

**✅ Recomendación adicional:**
En App Insights, configurar sampling adaptivo a 1‑10% para logs `Information`, 100% para `Warning+`. Reduce ingestion cost drásticamente sin perder señal.

---

## 11. 📐 Buenas Prácticas de Ingeniería y Estándares de Diseño

### 🟢 Aspectos positivos significativos

- **SOLID respetado**: factory methods (`CompanyChannel.ForWhatsAppCloud`) mantienen Open/Closed; entidades con setters `private`/`init` favorecen Liskov; `ICompanyContext` permite Dependency Inversion (excepto por la fuga en endpoints).
- **DRY moderado**: `WithDefaultTracking`, `HasJsonbConversion`, `DeserializeOptional` evitan repetición. Hay duplicación en `EnsureCompanyIsAccessibleAsync` (presente en 3 endpoints) que es **deliberada** (vertical slice no comparte por design), pero compensable con un PreProcessor de FastEndpoints (ver T‑01).
- **KISS**: `CompanyContextMiddleware` es 18 líneas, no 180. Buen instinto.
- **Configuración fuerte**: `ApiOptions`, `PersistenceOptions`, `ServiceDefaultsOptions` con `Validate` + `ValidateOnStart`. Excelente práctica.
- **Convenciones consistentes**: snake_case para Postgres, jsonb explícito, índices únicos en columnas correctas (`(provider, provider_channel_id)`, `(company_id, provider_message_id)` con filter).
- **Banned APIs**: `BannedSymbols.txt` enforce `TimeProvider`. Excelente para evitar `DateTime.Now` en código nuevo.
- **Tests en CI**: `dotnet test --no-build` recomendado en README, `Microsoft.Testing.Platform` configurado.

### 🟠 Hallazgo B‑01 — Duplicación de `EnsureCompanyIsAccessibleAsync` en 4 endpoints

**🚨 Descripción del problema:**
La misma lógica `if (companyContext.CompanyId != companyId || !await dbContext.Companies.AnyAsync(...)) throw new NotFoundException(...)` aparece en `ConfigureAgentProfileEndpoint`, `EnableCompanyToolEndpoint`, `RegisterIntegrationCredentialEndpoint`, y `RegisterCompanyChannelCommandHandler`. Cuatro copias de la **misma** decisión de seguridad. Una refactorización rompe una; las otras quedan inseguras. Viola DRY donde es crítico (en lógica de autorización).

**🛠️ Propuesta de Refactorización:**
Después de resolver S‑01 / S‑02 (claim‑based auth), la verificación ya no vive en handlers — vive en el middleware. Mientras tanto, un **PreProcessor de FastEndpoints**:

```csharp
public sealed class CompanyRouteGuard(ICompanyContext companyContext)
    : IPreProcessor<object>
{
    public Task PreProcessAsync(IPreProcessorContext<object> context, CancellationToken cancellationToken)
    {
        if (context.HttpContext.Request.RouteValues.TryGetValue("companyId", out var raw)
            && Guid.TryParse(raw?.ToString(), out var routeCompanyId)
            && companyContext.CompanyId != routeCompanyId)
        {
            throw new NotFoundException("company", routeCompanyId);
        }
        return Task.CompletedTask;
    }
}

// Configure global en ApiRegistrations:
services.AddFastEndpoints(options =>
{
    options.IncludeAbstractValidators = true;
    options.GlobalPreProcessors.Add(typeof(CompanyRouteGuard));
});
```

**✅ Recomendación adicional:**
Aplicar el mismo patrón al `AnyAsync` (cross‑existence) — ahora una única query global, cacheable con `IMemoryCache` (TTL 30s) para `Company.Exists`.

---

### 🟠 Hallazgo B‑02 — Naming inconsistente `CeoAgent` vs `CeoAgent` vs `CeoAgent.ApiService.Tests`

**🚨 Descripción del problema:**
- Namespace: `CeoAgent.*` (all caps).
- Trace source: `CeoAgent.*` (PascalCase). Inconsistente.
- Test project: `CeoAgent.ApiService.Tests.csproj` (matches namespace), pero el folder físico es `tests/Api.Tests/` (diferente). El test project name dice "todas las pruebas" pero solo contiene API tests.
- `CeoAgent.IntegrationTests` (otro namespace, no `CeoAgent.CeoAgent.IntegrationTests`).

**🛠️ Propuesta de Refactorización:**
Estandarizar:

- Namespace base: `CeoAgent` (PascalCase moderno, no all‑caps salvo siglas en medio).
- Folder + project + namespace = mismo nombre. `CeoAgent.ApiService.Tests`, `CeoAgent.IntegrationTests`, etc.
- Activity source: `CeoAgent.*` (ya correcto).

Esto es un branch de refactor mecánico, no urgente, pero hacerlo antes de que el proyecto crezca.

**✅ Recomendación adicional:**
Configurar `.editorconfig` con `dotnet_naming_rule` strict para enforce convención. Migración con `dotnet rename` + global search/replace + tests verdes.

---

### 🟠 Hallazgo B‑03 — `Guid.Empty` como sentinela en `AuditableCompanyOwnedEntity`

**🚨 Descripción del problema:**
`StampAuditableEntities` chequea `companyOwned.CompanyId == Guid.Empty` para decidir si setear el ambient. `Guid.Empty` como sentinela es **frágil**: un cliente podría enviar literalmente `00000000-0000-0000-0000-000000000000` en un Request, los validators no lo rechazan explícitamente, y el stamping lo sobreescribe con el ambient — comportamiento sorprendente.

**🛠️ Propuesta de Refactorización:**
Reglar que `CompanyId` se asigna **explícitamente** al añadir la entidad, no por convención mágica:

```csharp
public abstract class CompanyOwnedEntity
{
    public Guid CompanyId { get; }
    protected CompanyOwnedEntity(Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Company-owned entity requires a non-empty CompanyId.", nameof(companyId));
        CompanyId = companyId;
    }
}
```

Y el stamping ya no setea CompanyId (es immutable post‑creation). El handler/Mediator lo provee al construir.

**✅ Recomendación adicional:**
Refactor invasivo pero clarificador. Test que confirme que `Guid.Empty` POST nunca persiste.

---

### 🟡 Hallazgo B‑04 — Falta de documentación arquitectónica en `docs/`

**🚨 Descripción del problema:**
`docs/reviewer.md` es referenciado por README pero no se vio en el listado de archivos críticos. No hay ADRs (`docs/adr/0001-multi-tenancy.md`), no hay `docs/security.md`, `docs/observability.md`, `docs/architecture/style.md`. Las decisiones grandes (vertical slice vs clean, AsyncLocal vs scoped, jsonb vs columnas, etc.) no están registradas — riesgo de drift en equipos crecientes.

**🛠️ Propuesta de Refactorización:**
Adoptar [MADR](https://adr.github.io/madr/) lite:

```
docs/
├── architecture/
│   ├── style.md            (Vertical Slice + Clean híbrido)
│   ├── tenancy.md          (cómo se resuelve company_id)
│   └── concurrency.md      (locks, async, AsyncLocal policy)
├── adr/
│   ├── 0001-multi-tenancy-via-query-filters.md
│   ├── 0002-vertical-slice-with-mediator.md
│   └── 0003-jsonb-vs-relational-columns.md
├── operations/
│   ├── health-checks.md
│   └── observability.md
└── security.md
```

**✅ Recomendación adicional:**
Una ADR por decisión arquitectónica reversible cuesta 30 minutos y ahorra semanas de re‑debate.

---

### 🟡 Hallazgo B‑05 — `Conversation.AgentProfileId` immutability check via `SaveChangesAsync` interceptor

**🚨 Descripción del problema:**
La regla "AgentProfileId is immutable" se enforce en `StampAuditableEntities` a través de `entry.Property.IsModified`. Esto es **runtime check** y rompe Single Responsibility (el stamp method tiene dos jobs: stamping y rule enforcement). Mejor candidato: un EF Core `ISaveChangesInterceptor`.

**🛠️ Propuesta de Refactorización:**

```csharp
internal sealed class ConversationImmutabilityInterceptor : ISaveChangesInterceptor
{
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null) return ValueTask.FromResult(result);

        foreach (var entry in eventData.Context.ChangeTracker.Entries<Conversation>())
        {
            if (entry.State == EntityState.Modified
                && entry.Property(nameof(Conversation.AgentProfileId)).IsModified)
            {
                throw new InvalidOperationException("Conversation.AgentProfileId is immutable after creation.");
            }
        }
        return ValueTask.FromResult(result);
    }
}

// En DI:
options.AddInterceptors(new ConversationImmutabilityInterceptor());
```

**✅ Recomendación adicional:**
Patrón replicable para otras invariantes (e.g. `Company.Status` solo puede ir `Active → Suspended`, no `Suspended → Active` sin proceso explícito).

---

### 🟢 Hallazgo B‑06 — Mappers Mapperly correctamente segmentados

`CompanyMapper` agrupa correctamente todas las operaciones del slice "Companies" (Request → Entity, Entity → Response, Request → Command). `AutoUserMappings = false` y `RequiredMappingStrategy = RequiredMappingStrategy.Target` son la configuración estricta correcta. Mantener este patrón en futuros slices.

---

# 📊 Resumen Ejecutivo de Hallazgos

| ID | Sección | Severidad | Hallazgo | Bloquea producción |
| --- | --- | --- | --- | --- |
| S‑01 | Seguridad | 🔴 Crítica | Endpoints admin sin autenticación | **Sí** |
| S‑02 | Seguridad | 🔴 Crítica | IDOR por header `X-Company-Id` | **Sí** |
| C‑01 / E‑01 | Concurrencia / Escalabilidad | 🔴 Crítica | Singleton CompanyContext + AsyncLocal + DbContextPool | **Sí** |
| S‑03 | Seguridad | 🟠 Alta | Webhook HMAC validation no implementada | Sí (cuando se conecte el webhook) |
| S‑04 | Seguridad | 🟠 Alta | `GlobalExceptionHandler` puede filtrar detalles | No, pero recomendado |
| E‑02 | Escalabilidad | 🟠 Alta | Storage Queue sin DLQ ni poison handling | Recomendado antes de carga real |
| E‑03 | Escalabilidad | 🟠 Alta | Sin concurrency tokens (`xmin`) | Recomendado |
| P‑01 | Performance | 🟠 Alta | `ChangeTracker.Entries().ToArray()` innecesario | No, pero económico |
| P‑02 | Performance | 🟠 Alta | `HasJsonbConversion` reserializa para comparer | Recomendado |
| A‑01 | Arquitectura | 🟠 Alta | `ApiService` accede directo a `DbContext` | Decisión consciente, documentar |
| A‑02 | Arquitectura | 🟠 Alta | `Application` casi vacío | Recomendado antes de crecer |
| T‑02 | Testabilidad | 🟠 Alta | API tests usan InMemoryDB que ignora jsonb / partial indices | Sí |
| O‑01 | Observabilidad | 🟠 Media | Worker logging sin JsonFormatter | Recomendado |
| O‑02 | Observabilidad | 🟠 Media | Niveles de log incorrectos para `BusinessRule/NotFound` | Recomendado |
| S‑05 | Seguridad | 🟡 Media | CORS demasiado permisivo cuando se configura | Cuando se configure |
| S‑06 | Seguridad | 🟡 Media | Refit clients sin auth handler centralizado | Cuando se conecten adapters |
| C‑02 | Concurrencia | 🟡 Media | Correlation ID sin sanitizar (log injection) | Sí |
| E‑04 | Escalabilidad | 🟡 Media | Unique index parcial puede causar 23505 sin handling | Cuando haya volumen |
| E‑05 | Escalabilidad | 🟡 Media | Health checks no incluyen storage queues/blobs | Sí para producción |
| P‑03 / P‑04 | Performance | 🟡 Media | NoTracking default + queries duplicadas | Recomendado |
| P‑05 | Performance | 🟡 Media | Worker stub sin paralelización | Cuando se implemente |
| O‑03 / O‑04 | Observabilidad | 🟡 Media | Métricas de negocio ausentes, health tags inconsistentes | Recomendado |
| M‑01..M‑05 | Memoria/.NET | 🟡 Baja | Optimizaciones sintaxis moderna, source‑gen JSON | Opcional |
| $‑01..$‑05 | Costos Azure | 🟡 Media | Pool size, OTLP batch, HttpClient pooling, logs duplicados | Recomendado |
| T‑01 | Testabilidad | 🟠 Alta | `[NotInParallel]` indica fragilidad concurrency | Sí (resuelto al fixar C‑01) |
| T‑03 / T‑04 | Testabilidad | 🟡 Baja | Webhook/agent tests ausentes (no impl), property‑based opcional | Cuando se implemente |
| B‑01..B‑06 | Buenas prácticas | 🟡 Baja | Duplicación de auth checks, naming, sentinelas Guid.Empty | Recomendado |

---

## 🎯 Próximos pasos recomendados (priorizados)

1. **Hoy / Antes del próximo merge:** Implementar S‑01 (autenticación admin), S‑02 (claim‑based company validation), C‑02 (sanitizar correlation id).
2. **Esta semana:** Resolver C‑01 / E‑01 (scoped CompanyContextAccessor + revisión del DbContextPool). Añadir test de concurrencia T‑01. Migrar Api.Tests a Testcontainers (T‑02).
3. **Este sprint:** Health checks completos (E‑05), concurrency tokens (E‑03), DLQ (E‑02), métricas de negocio (O‑03), nivel de log correcto (O‑02), logging coherente Worker (O‑01).
4. **Próximo sprint (antes de implementar webhook real):** S‑03 (HMAC), S‑06 (auth handlers Refit), pipeline Worker modular (A‑03), eliminar duplicación de guards (B‑01).
5. **Continuo:** ADRs (B‑04), source‑gen JSON (M‑02), benchmarks (P‑01, P‑02), Azure FinOps (Sección 10).

---

> 🔚 **Cierre:** CeoAgent tiene los huesos de un sistema enterprise sólido. La selección tecnológica, la disciplina de configuración y la calidad del código existente son notables para un MVP pre‑release. Las correcciones críticas (S‑01, S‑02, C‑01) son **acotadas y mecánicas**: pueden cerrarse en 2‑3 días de trabajo focalizado. Una vez resueltas, el proyecto puede pasar de "Aceptable con riesgos" a "Excelente" y avanzar con seguridad hacia el flujo MVP completo.
