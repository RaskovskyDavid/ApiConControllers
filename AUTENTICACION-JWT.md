# Autenticación JWT en .NET Web API con controllers

Guía de repaso de cómo está implementada la autenticación en este proyecto: registro, login y asignación de roles con **JWT**, **ASP.NET Core Identity** y **SQLite**.

> **Actualización:** el proyecto ya incluye refresh tokens. `TokenService.CreateToken` pasó a llamarse `CreateAccessToken`, `AuthResponse` devuelve ahora access token y refresh token, y `ExpiryMinutes` es 15. Esta guía describe la base JWT; el refresh token está en [REFRESH-TOKENS.md](REFRESH-TOKENS.md).

> **Roles y seed:** el seed ya no vive en `Program.cs` ni usa `SeedAdmin`. Ahora hay roles `User`, `Manager` y `Admin`, políticas de autorización y un `IdentitySeeder`; todo está en [ROLES-AUTORIZACION.md](ROLES-AUTORIZACION.md).

## Cómo funciona

El servidor firma un token JWT cuando el usuario inicia sesión. El cliente lo envía en cada petición y el servidor solo verifica la firma; no guarda sesión.

1. **Register:** se crea el usuario (Identity hashea la contraseña) y recibe el rol `User`.
2. **Login:** se validan email y contraseña; si son correctos, `TokenService` firma un JWT con los claims `sub`, `email`, `name`, `jti` y `role`.
3. **Petición protegida:** el cliente manda `Authorization: Bearer <token>`; el middleware JwtBearer valida firma, emisor, audiencia y expiración.
4. **Autorización:** `[Authorize]` exige un token válido; `[Authorize(Roles = "Admin")]` exige además el rol en el claim `role`.

## Mapa de archivos

| Archivo | Responsabilidad |
| --- | --- |
| `appsettings.json` | Sección `JwtSettings`, cadena de conexión y `SeedAdmin` |
| `Auth/JwtSettings.cs` | Clase tipada de la sección `JwtSettings` |
| `Auth/ITokenService.cs`, `Auth/TokenService.cs` | Crean y firman el JWT |
| `Entities/ApplicationUser.cs` | `IdentityUser` + `FirstName` y `LastName` |
| `Entities/Roles.cs` | Constantes `User` y `Admin` |
| `Data/AppDbContext.cs` | `IdentityDbContext<ApplicationUser>` (tablas de Identity) |
| `Models/AuthModels.cs` | DTOs: `RegisterRequest`, `LoginRequest`, `AddRoleRequest`, `AuthResponse` |
| `Controllers/AuthController.cs` | Endpoints `register`, `login`, `add-role` |
| `Program.cs` | Registro de servicios, validación del JWT, datos iniciales y pipeline |

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
    "ExpiryMinutes": 60
  },
  "ConnectionStrings": { "DefaultConnection": "Data Source=auth.db" },
  "SeedAdmin": { "Email": "admin@example.com", "Password": "Admin123!", "FirstName": "Admin", "LastName": "Root" }
}
```

- La `Key` debe tener al menos 32 caracteres (HMAC-SHA256).
- En producción guarda la `Key` y la contraseña del admin en user-secrets o variables de entorno, no en el repositorio.

## Paso 3: usuario, roles y DbContext

```csharp
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";
}

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);
```

## Paso 4: crear el token

`TokenService` arma los claims, firma con la clave y devuelve el token con su fecha de expiración. Cada rol del usuario se guarda como un claim `role`.

```csharp
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, user.Id),
    new(JwtRegisteredClaimNames.Email, user.Email!),
    new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
};
claims.AddRange(roles.Select(role => new Claim("role", role)));

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
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// AddIdentityCore (no AddIdentity) para que Identity no cambie el esquema por defecto a cookies.
builder.Services
    .AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<ITokenService, TokenService>();

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

### Datos iniciales

Al arrancar se crea la base (`EnsureCreatedAsync`), los roles `User` y `Admin`, y un usuario admin leído de `SeedAdmin`. Sin admin nadie podría usar `add-role`.

### Pipeline

```csharp
app.UseHttpsRedirection();
app.UseAuthentication();   // 1.º: lee el token y arma el usuario
app.UseAuthorization();    // 2.º: decide si ese usuario puede entrar
app.MapControllers();
```

`UseAuthentication` debe ir **antes** de `UseAuthorization`, o `[Authorize]` siempre responderá 401.

## Paso 6: AuthController

| Endpoint | Acceso | Qué hace | Respuestas |
| --- | --- | --- | --- |
| `POST /auth/register` | Anónimo | Crea el usuario, le asigna `User` y devuelve un token | 200, 400 (email duplicado o contraseña inválida) |
| `POST /auth/login` | Anónimo | Valida credenciales y devuelve un token | 200, 401 (mensaje genérico) |
| `POST /auth/add-role` | Solo `Admin` | Asigna un rol existente a un usuario | 200, 400 (rol no existe), 404 (usuario no existe), 409 (ya tiene el rol) |

Puntos a recordar:

- El controller inyecta `UserManager<ApplicationUser>`, `RoleManager<IdentityRole>` y `ITokenService`.
- En `login` el mensaje de error es el mismo si el email no existe o la contraseña es incorrecta, para no revelar qué usuarios existen.
- El rol nuevo solo aparece en tokens emitidos **después** de `add-role`; el usuario debe iniciar sesión de nuevo.
- Para proteger cualquier otro controller basta `[Authorize]` en la clase o el método.

## Probar el flujo

| # | Petición | Resultado esperado |
| --- | --- | --- |
| 1 | `POST /auth/register` con un usuario nuevo | 200 y un token |
| 2 | `POST /auth/register` con el mismo email | 400 |
| 3 | `POST /auth/login` con contraseña incorrecta | 401 |
| 4 | `POST /auth/login` correcto | 200 y un token |
| 5 | `POST /auth/add-role` sin token | 401 |
| 6 | `POST /auth/add-role` con token de un `User` | 403 |
| 7 | `POST /auth/add-role` con token del admin sembrado | 200 |
| 8 | `GET /WeatherForecast` sin token / con token | 401 / 200 |

```bash
curl -X POST http://localhost:5224/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}'

curl http://localhost:5224/WeatherForecast -H "Authorization: Bearer <TOKEN>"
```

Pega el token en [jwt.io](https://jwt.io) para ver los claims. En Swagger/Scalar, pega solo el token (sin el prefijo `Bearer`); el botón Authorize conserva el último token usado, así que si pruebas con varios usuarios, cámbialo cada vez.

## Errores comunes

| Síntoma | Causa probable | Solución |
| --- | --- | --- |
| `[Authorize]` siempre devuelve 401 | Falta `UseAuthentication()` o va después de `UseAuthorization()` | Ponerlo antes |
| 403 aunque el usuario tiene el rol | `RoleClaimType` no coincide con el claim `role`, o el token se emitió antes de asignar el rol | Usar `RoleClaimType = "role"` y volver a iniciar sesión |
| 401 con un token recién creado | `Issuer`, `Audience` o `Key` distintos entre `TokenService` y `AddJwtBearer` | Ambos leen la misma sección `JwtSettings` |
| Error de clave demasiado corta | `Key` con menos de 32 caracteres | Usar 32 o más caracteres |
| Redirige a login o pide cookies | Se usó `AddIdentity` en vez de `AddIdentityCore` | Usar `AddIdentityCore` + `AddRoles` |
| Swagger siempre responde 401 | Falta el esquema `Bearer` o el token se pegó con el prefijo `Bearer` | Pegar solo el token |

## Notas del proyecto

- `Middlewears/BasicAuthMiddleweare.cs` sigue en el repositorio pero ya no se usa en el pipeline.
- La base se crea con `EnsureCreatedAsync`, sin migraciones. Para practicar `dotnet ef migrations`, hay que reemplazarlo por `Migrate()`.
- `auth.db` se genera al ejecutar la app; conviene agregarlo al `.gitignore`.
