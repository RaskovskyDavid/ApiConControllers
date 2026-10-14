# Autenticación JWT en .NET Web API con controllers

Guía de repaso de cómo está implementada la autenticación en este proyecto: registro, login y roles con **JWT**, **ASP.NET Core Identity** y **SQLite**. Es la base sobre la que se apoyan el resto de guías.

## Guías relacionadas

| Guía | Qué añade |
| --- | --- |
| [REFRESH-TOKENS.md](REFRESH-TOKENS.md) | Renovar la sesión, rotación y revocación |
| [ROLES-AUTORIZACION.md](ROLES-AUTORIZACION.md) | Roles, políticas y el seed |
| [CLAIMS-AUTORIZACION.md](CLAIMS-AUTORIZACION.md) | Autorización por atributos del usuario (departamento, suscripción) |
| [PERMISSION-BASED-AUTHORIZATION.md](PERMISSION-BASED-AUTHORIZATION.md) | Permisos como claims de los roles, modos `Token` y `Lookup` |

## Cómo funciona

El servidor firma un token JWT cuando el usuario inicia sesión. El cliente lo envía en cada petición y el servidor solo verifica la firma; no guarda sesión.

1. **Register:** se crea el usuario (Identity hashea la contraseña) y recibe el rol `User`.
2. **Login:** se validan email y contraseña; si son correctos, `TokenService` firma un JWT con los claims `sub`, `email`, `name`, `jti` y `role`, más los claims del usuario (`department`, `subscription`). Los permisos entran en el token solo en modo `Token`.
3. **Petición protegida:** el cliente manda `Authorization: Bearer <token>`; el middleware JwtBearer valida firma, emisor, audiencia y expiración.
4. **Autorización:** `[Authorize]` exige un token válido; una política (`[Authorize(Policy = ...)]`) exige además un rol, un claim o un permiso.

El login y el registro devuelven también un **refresh token** y la lista de permisos del usuario (ver [REFRESH-TOKENS.md](REFRESH-TOKENS.md)).

## Mapa de archivos

| Archivo | Responsabilidad |
| --- | --- |
| `appsettings.json` | `JwtSettings`, `Permissions`, cadena de conexión y `Seed` |
| `Auth/JwtSettings.cs` | Clase tipada de `JwtSettings` (clave, emisor, audiencia, duraciones) |
| `Auth/ITokenService.cs`, `Auth/TokenService.cs` | Crean y firman el JWT y el refresh token |
| `Auth/Policies.cs` | Políticas de roles y claims, política fallback y registro de permisos |
| `Authorization/` | Requisito, handler, proveedor de políticas y almacén de permisos |
| `Entities/ApplicationUser.cs` | `IdentityUser` + `FirstName` y `LastName` |
| `Entities/Roles.cs` | Constantes `User`, `Manager`, `Admin` y la lista `All` |
| `Entities/RefreshToken.cs` | Fila de la tabla de refresh tokens |
| `Data/AppDbContext.cs` | `IdentityDbContext<ApplicationUser>` + `RefreshTokens` |
| `Data/IdentitySeeder.cs`, `Data/SeedOptions.cs` | Siembra roles, permisos de roles y usuarios desde `appsettings.json` |
| `Models/AuthModels.cs` | DTOs de entrada y `AuthResponse` |
| `Controllers/AuthController.cs` | `register`, `login`, `refresh`, `revoke`, `add-role`, `set-claim` |
| `Program.cs` | Registro de servicios, validación del JWT, seed y pipeline |

## Paso 1: paquetes

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

Usa la misma versión de EF Core en todos.

## Paso 2: configuración

```json
{
  "JwtSettings": {
    "Key": "clave-secreta-de-al-menos-32-caracteres",
    "Issuer": "https://mi-api.com",
    "Audience": "mi-api-clientes",
    "ExpiryMinutes": 15,
    "RefreshTokenDays": 7
  },
  "Permissions": { "Source": "Lookup" },
  "ConnectionStrings": { "DefaultConnection": "Data Source=auth.db" },
  "Seed": { "RolePermissions": { ... }, "Users": [ ... ] }
}
```

- La `Key` debe tener al menos 32 caracteres (HMAC-SHA256).
- `ExpiryMinutes` es la vida del access token (corta); `RefreshTokenDays`, la del refresh token.
- `Permissions:Source` y `Seed` se explican en [PERMISSION-BASED-AUTHORIZATION.md](PERMISSION-BASED-AUTHORIZATION.md).
- En producción guarda la `Key` y las contraseñas del seed en user-secrets o variables de entorno, no en el repositorio.

## Paso 3: usuario, roles y DbContext

```csharp
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public static class Roles
{
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static readonly string[] All = [User, Manager, Admin];
}

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    // OnModelCreating: índice único y relación de RefreshToken (ver REFRESH-TOKENS.md)
}
```

`Roles.All` lo usa el seeder: añadir un rol es una constante y una entrada en esa lista.

## Paso 4: crear el token

`TokenService.CreateAccessToken` recibe el usuario, sus roles, sus claims y sus permisos. Arma los claims, firma con la clave y devuelve el token con su fecha de expiración.

```csharp
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, user.Id),
    new(JwtRegisteredClaimNames.Email, user.Email!),
    new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
};
claims.AddRange(roles.Select(role => new Claim("role", role)));   // un claim por rol
claims.AddRange(userClaims);                                       // department, subscription...

if (_permissions.Source == PermissionSource.Token)                 // solo en modo Token
    claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
var descriptor = new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(claims),
    Expires = expiresAt,
    Issuer = _settings.Issuer,
    Audience = _settings.Audience,
    SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
};
var token = new JsonWebTokenHandler().CreateToken(descriptor);
```

## Paso 5: Program.cs

### Servicios

```csharp
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<PermissionSettings>(builder.Configuration.GetSection("Permissions"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// AddIdentityCore (no AddIdentity) para que Identity no cambie el esquema por defecto a cookies.
builder.Services
    .AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ITokenService>(sp => sp.GetRequiredService<TokenService>());
builder.Services.AddIdentitySeeder(builder.Configuration);
builder.Services.AddAuthorizationPolicies();   // política fallback, roles, claims y permisos
builder.Services.AddSingleton<ProductStore>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // mantiene los nombres de claim del token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = "role"          // debe coincidir con el claim que crea TokenService
        };
    });
```

`TokenService` se registra también como clase concreta para que `DiagnosticsController` reutilice su camino de firma.

### Datos iniciales

`await app.SeedIdentityAsync()` ejecuta `IdentitySeeder`: crea la base (`EnsureCreatedAsync`), los roles de `Roles.All`, los permisos de cada rol y los usuarios de `Seed:Users`. Sin un admin nadie podría usar `add-role` ni gestionar permisos.

### Pipeline

```csharp
app.UseHttpsRedirection();
app.UseAuthentication();   // 1.º: lee el token y arma el usuario
app.UseAuthorization();    // 2.º: decide si ese usuario puede entrar
app.MapControllers();
```

`UseAuthentication` debe ir **antes** de `UseAuthorization`, o `[Authorize]` siempre responderá 401.

### API segura por defecto

`AddAuthorizationPolicies` define una **política fallback**: todo endpoint exige un usuario autenticado salvo que opte por salir con `[AllowAnonymous]`. Por eso `register`, `login`, `refresh` y `revoke` llevan `[AllowAnonymous]`, y las rutas de documentación también salen explícitamente:

```csharp
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference().AllowAnonymous();
```

## Paso 6: AuthController

| Endpoint | Acceso | Qué hace | Respuestas |
| --- | --- | --- | --- |
| `POST /auth/register` | Anónimo | Crea el usuario, le asigna `User` y devuelve los tokens | 200, 400 (email duplicado o contraseña inválida) |
| `POST /auth/login` | Anónimo | Valida credenciales y devuelve los tokens | 200, 401 (mensaje genérico) |
| `POST /auth/refresh` | Anónimo | Cambia un refresh token válido por un par nuevo | 200, 401 |
| `POST /auth/revoke` | Anónimo | Revoca un refresh token (logout) | 200, 404 |
| `POST /auth/add-role` | Solo `Admin` | Asigna un rol existente a un usuario | 200, 400, 404, 409 |
| `POST /auth/set-claim` | Solo `Admin` | Asigna `department` o `subscription` a un usuario | 200, 400, 404 |

`register` y `login` devuelven un `AuthResponse` con el access token, el refresh token, los roles y los permisos del usuario.

Puntos a recordar:

- El controller inyecta `UserManager`, `RoleManager`, `ITokenService`, `PermissionStore`, `AppDbContext` y `JwtSettings`.
- En `login` el mensaje de error es el mismo si el email no existe o la contraseña es incorrecta, para no revelar qué usuarios existen.
- Un rol o un claim nuevo solo aparece en tokens emitidos **después** del cambio: el usuario debe iniciar sesión de nuevo o renovar con `/auth/refresh`.
- `register`, `login` y `refresh` comparten `IssueTokensAsync`, así que no hay código duplicado.

## Probar el flujo

| # | Petición | Resultado esperado |
| --- | --- | --- |
| 1 | `POST /auth/register` con un usuario nuevo | 200 y los tokens |
| 2 | `POST /auth/register` con el mismo email | 400 |
| 3 | `POST /auth/login` con contraseña incorrecta | 401 |
| 4 | `POST /auth/login` correcto | 200 y los tokens |
| 5 | `POST /auth/add-role` sin token | 401 |
| 6 | `POST /auth/add-role` con token de un `User` | 403 |
| 7 | `POST /auth/add-role` con token del admin sembrado | 200 |
| 8 | `GET /WeatherForecast` sin token | 401 |
| 9 | `GET /WeatherForecast` con token de Manager / de User | 200 / 403 |

```bash
curl -X POST http://localhost:5224/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}'

curl http://localhost:5224/WeatherForecast -H "Authorization: Bearer <ACCESS_TOKEN>"
```

El archivo [ApiConControllers.http](ApiConControllers.http) tiene estas peticiones listas. Pega el token en [jwt.io](https://jwt.io) para ver los claims. En Swagger/Scalar, pega solo el token (sin el prefijo `Bearer`); el botón Authorize conserva el último token usado, así que si pruebas con varios usuarios, cámbialo cada vez.

## Errores comunes

| Síntoma | Causa probable | Solución |
| --- | --- | --- |
| `[Authorize]` siempre devuelve 401 | Falta `UseAuthentication()` o va después de `UseAuthorization()` | Ponerlo antes |
| 403 aunque el usuario tiene el rol | `RoleClaimType` no coincide con el claim `role`, o el token se emitió antes de asignar el rol | Usar `RoleClaimType = "role"` y volver a iniciar sesión |
| 401 con un token recién creado | `Issuer`, `Audience` o `Key` distintos entre `TokenService` y `AddJwtBearer` | Ambos leen la misma sección `JwtSettings` |
| 401 en un endpoint nuevo sin `[Authorize]` | La política fallback exige autenticación en todo | Añadir `[AllowAnonymous]` si debe ser público |
| Error de clave demasiado corta | `Key` con menos de 32 caracteres | Usar 32 o más caracteres |
| Redirige a login o pide cookies | Se usó `AddIdentity` en vez de `AddIdentityCore` | Usar `AddIdentityCore` + `AddRoles` |
| Swagger siempre responde 401 | Falta el esquema `Bearer` o el token se pegó con el prefijo `Bearer` | Pegar solo el token |

## Notas del proyecto

- `Middlewears/BasicAuthMiddleweare.cs` sigue en el repositorio pero ya no se usa en el pipeline.
- La base se crea con `EnsureCreatedAsync`, que **no añade tablas ni columnas a una base que ya existe**. Si cambias el esquema, borra `auth.db` (en desarrollo) o pasa a migraciones con `Migrate()`.
- `auth.db` se genera al ejecutar la app; conviene agregarlo al `.gitignore`.
- **Esto es código de estudio, no apto para producción tal cual:** la `Key` del JWT y las contraseñas del seed están en `appsettings.json`, la base se crea con `EnsureCreatedAsync` y el login no bloquea cuentas. Mira la [sección 12 de PERMISSION-BASED-AUTHORIZATION.md](PERMISSION-BASED-AUTHORIZATION.md#12-antes-de-replicar-este-código) antes de replicarlo.
