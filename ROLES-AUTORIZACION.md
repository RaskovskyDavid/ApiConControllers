# Autorización basada en roles (RBAC): guía de estudio

Continuación de [AUTENTICACION-JWT.md](AUTENTICACION-JWT.md) y [REFRESH-TOKENS.md](REFRESH-TOKENS.md). Aquí se agrega **qué puede hacer cada usuario según su rol**: `User`, `Manager` y `Admin`.

## 1. Autenticación vs. autorización

| | Autenticación | Autorización |
| --- | --- | --- |
| Pregunta | ¿Quién eres? | ¿Qué puedes hacer? |
| En este proyecto | Login, JWT, refresh token | Roles y políticas |
| Si falla | **401 Unauthorized** (no hay identidad válida) | **403 Forbidden** (sabemos quién eres, pero no tienes permiso) |

Distinguir 401 de 403 es la mejor forma de depurar: un 401 es un problema del token; un 403 es un problema del rol.

## 2. Cómo viaja el rol: del login a la decisión

1. En el login, `TokenService` mete un claim `role` por cada rol del usuario dentro del JWT.
2. En cada petición, el middleware JwtBearer valida el token y construye el usuario (`HttpContext.User`) con esos claims.
3. `[Authorize(Policy = ...)]` revisa si `User` tiene alguno de los roles que pide la política.

Dos líneas de `Program.cs` hacen que el paso 3 encuentre el rol:

```csharp
options.MapInboundClaims = false;   // conserva el nombre "role" tal como viene en el token
RoleClaimType = "role"              // le dice a ASP.NET qué claim contiene los roles
```

Si `RoleClaimType` no coincide con el nombre del claim que crea `TokenService`, **todo da 403 aunque el rol sea correcto**.

## 3. Roles y política de permisos

Jerarquía usada: `Admin` ⊃ `Manager` ⊃ `User`. Un rol superior puede hacer lo que hace uno inferior.

| Endpoint | User | Manager | Admin | Política |
| --- | --- | --- | --- | --- |
| `GET /WeatherForecast/{id}` | ✅ | ✅ | ✅ | `UserOrAbove` |
| `GET /WeatherForecast` | ❌ 403 | ✅ | ✅ | `ManagerOrAbove` |
| `DELETE /WeatherForecast/{id}` | ❌ 403 | ✅ | ✅ | `ManagerOrAbove` |
| `POST /auth/add-role` | ❌ 403 | ❌ 403 | ✅ | `AdminOnly` |
| Sin token (cualquiera) | 401 | 401 | 401 | — |

## 4. Tres formas de proteger un endpoint

| Forma | Ejemplo | Cuándo usarla |
| --- | --- | --- |
| Solo autenticado | `[Authorize]` | Cualquier usuario con token válido, sin importar el rol |
| Por rol | `[Authorize(Roles = "Admin,Manager")]` | Casos sueltos. Los textos se repiten por todo el código |
| **Por política** | `[Authorize(Policy = Policies.ManagerOrAbove)]` | Recomendada: la regla vive en un solo sitio |

Reglas que conviene memorizar:

- **Dentro de un mismo atributo, la coma es "O":** `Roles = "Admin,Manager"` permite Admin **o** Manager.
- **Atributos apilados son "Y":** `[Authorize(Roles="Admin")]` + `[Authorize(Roles="Manager")]` exige **ambos** roles a la vez. Por eso `[Authorize]` en la clase y `[Authorize(Policy=...)]` en el método funcionan bien juntos: la clase solo pide estar autenticado, y el método añade la restricción.
- `[AllowAnonymous]` en un método anula los `[Authorize]` de la clase.

## 5. Paso a paso de la implementación

### Paso 1: los roles como constantes

`Entities/Roles.cs`. Es `static` y tiene la lista `All`, que usa el seeder.

```csharp
public static class Roles
{
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static readonly string[] All = [User, Manager, Admin];
}
```

Para añadir un rol nuevo: una constante y una entrada en `All`. El seeder lo crea solo.

### Paso 2: las políticas

`Auth/Policies.cs` concentra las reglas con nombre. Los controllers no repiten listas de roles.

```csharp
public static class Policies
{
    public const string UserOrAbove = nameof(UserOrAbove);
    public const string ManagerOrAbove = nameof(ManagerOrAbove);
    public const string AdminOnly = nameof(AdminOnly);

    public static IServiceCollection AddRolePolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(UserOrAbove, p => p.RequireRole(Roles.User, Roles.Manager, Roles.Admin))
            .AddPolicy(ManagerOrAbove, p => p.RequireRole(Roles.Manager, Roles.Admin))
            .AddPolicy(AdminOnly, p => p.RequireRole(Roles.Admin));
        return services;
    }
}
```

`RequireRole(a, b)` significa "a **o** b". Si mañana `Manager` pierde acceso a algo, se cambia aquí y se aplica a todos los endpoints que usan la política.

### Paso 3: el seed (datos iniciales)

Antes estaba dentro de `Program.cs` y solo creaba un admin. Ahora son tres archivos con una responsabilidad cada uno.

**Configuración** (`appsettings.json`): una lista de usuarios, cada uno con su rol.

```json
"Seed": {
  "Users": [
    { "Email": "admin@example.com",   "Password": "Admin123!",   "FirstName": "Admin",   "LastName": "Root",    "Role": "Admin" },
    { "Email": "manager@example.com", "Password": "Manager123!", "FirstName": "Manager", "LastName": "User",    "Role": "Manager" },
    { "Email": "user@example.com",    "Password": "User123!",    "FirstName": "User",    "LastName": "Example", "Role": "User" }
  ]
}
```

**Modelo** (`Data/SeedOptions.cs`): clases que reciben esa sección, siguiendo el *Options pattern* (igual que `JwtSettings`).

**Seeder** (`Data/IdentitySeeder.cs`): una clase con tres métodos pequeños.

```csharp
public async Task SeedAsync()
{
    await db.Database.EnsureCreatedAsync();
    await SeedRolesAsync();   // recorre Roles.All y crea los que falten
    await SeedUsersAsync();   // crea los usuarios de Seed:Users que falten
}
```

**Conexión con `Program.cs`**: solo dos líneas.

```csharp
builder.Services.AddIdentitySeeder(builder.Configuration);   // registra opciones + seeder
...
await app.SeedIdentityAsync();                               // lo ejecuta al arrancar
```

Por qué este patrón:

| Decisión | Motivo |
| --- | --- |
| Clase propia en vez de código en `Program.cs` | `Program.cs` queda legible y el seeder se puede probar y reutilizar |
| Datos en configuración, no en código | Cambiar o añadir usuarios no requiere recompilar; en producción simplemente no se configura la sección |
| **Idempotente** | Si el rol o el usuario ya existe, lo salta; se puede ejecutar en cada arranque sin duplicados |
| Valida el rol de cada usuario | Un rol mal escrito (por ejemplo `"Chef"`) se omite con una advertencia |
| Registra los errores | Antes un fallo (como una contraseña débil) era silencioso y el usuario simplemente no existía |
| Servicio *scoped* con scope manual | Usa `DbContext`, que es *scoped*; fuera de una petición HTTP hay que crear el scope a mano (`CreateScope()`) |

Alternativas: un `IHostedService` que corra al iniciar (útil si hay más tareas de arranque) o `HasData` en el modelo de EF (pero no sirve para usuarios con contraseña, porque hay que hashearla con `UserManager`).

### Paso 4: usar las políticas en los controllers

```csharp
[Authorize]                                        // la clase: basta con estar autenticado
public class WeatherForecastController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ManagerOrAbove)]  // el método añade la restricción
    public IEnumerable<WeatherForecast> Get(...) { ... }
}
```

En `AuthController`, `add-role` usa `Policies.AdminOnly`.



## 6. Cómo probarlo

El seed crea tres usuarios. Haz login con cada uno y prueba con su `accessToken`.

| Petición | User | Manager | Admin | Sin token |
| --- | --- | --- | --- | --- |
| `GET /WeatherForecast` | 403 | 200 | 200 | 401 |
| `GET /WeatherForecast/0` | 200 | 200 | 200 | 401 |
| `GET /WeatherForecast/99` | 404 | 404 | 404 | 401 |
| `DELETE /WeatherForecast/0` | 403 | 204 | 204 | 401 |
| `POST /auth/add-role` | 403 | 403 | 200 | 401 |

Prueba además estos tres casos:

1. **Persistencia del DELETE:** borra un elemento con un Manager y vuelve a pedir la lista; debe tener un elemento menos.
2. **Idempotencia del seed:** reinicia la app dos veces; no debe haber errores ni usuarios duplicados.
3. **Seed con error:** pon una contraseña débil (`"abc"`) o un rol inexistente (`"Chef"`) en `Seed:Users`; el log debe mostrar `Seed failed ...` o `Seed user ... skipped`.

Para ver los roles de un token, pégalo en [jwt.io](https://jwt.io) y busca el claim `role`.

## 7. Cosas que sorprenden

- **Los roles viajan dentro del token.** Si un admin le cambia el rol a alguien con `add-role`, su access token actual **sigue con los roles viejos** hasta que expire (15 min) o hasta que haga `/auth/refresh`, que sí relee los roles de la base. Es el mismo límite stateless explicado en la guía de refresh tokens.
- **Un usuario puede tener varios roles.** `add-role` suma roles, no los reemplaza; por eso un Manager que también es User tiene dos claims `role`.
- **Hay que volver a crear `auth.db` solo si cambia el esquema.** El seed nuevo no cambia tablas; si la base ya tiene los usuarios, simplemente crea los que falten (`manager@` y `user@`).
- **Las contraseñas del seed son solo para desarrollo.** En un entorno real, no pongas contraseñas en `appsettings.json`: usa `dotnet user-secrets` o variables de entorno (`Seed__Users__0__Password`), o deja la sección vacía.

## 8. Para seguir practicando

- Endpoint `POST /auth/remove-role` (solo Admin), con la protección de no quitarse el último Admin a uno mismo.
- Políticas con **claims** (por ejemplo `RequireClaim("department", "sales")`) y con requisitos propios (`IAuthorizationRequirement` + `AuthorizationHandler`).
- Autorización por **recurso** (el dueño de un registro puede editarlo): `IAuthorizationService.AuthorizeAsync(User, resource, policy)`.
- Migrar de `EnsureCreatedAsync` a migraciones de EF Core.
- Escribir pruebas de integración con `WebApplicationFactory` que verifiquen la tabla de la sección 6.
