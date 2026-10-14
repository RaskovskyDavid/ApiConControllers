# Autorización basada en permisos con controllers: guía de estudio

Implementación del artículo [Permission-Based Authorization in ASP.NET Core](https://codewithmukesh.com/blog/permission-based-authorization-in-aspnet-core/) de Mukesh Murugan (codewithmukesh), adaptada a **controllers** en lugar de minimal API. El código de referencia del autor está en su [repositorio](https://github.com/codewithmukesh/dotnet-webapi-zero-to-hero-course/tree/main/modules/05-api-security/permission-based-authorization-in-aspnet-core) (licencia MIT).

> **Código de estudio.** Antes de llevarlo a producción, lee la [sección 12](#12-antes-de-replicar-este-código): hay decisiones del proyecto (secretos en `appsettings.json`, `EnsureCreatedAsync`, login sin bloqueo de cuenta…) que no siguen las recomendaciones de Microsoft.

Continuación de [AUTENTICACION-JWT.md](AUTENTICACION-JWT.md), [ROLES-AUTORIZACION.md](ROLES-AUTORIZACION.md) y [CLAIMS-AUTORIZACION.md](CLAIMS-AUTORIZACION.md).

## 1. La idea en una frase

Los **roles** siguen existiendo, pero ya no se escriben en los endpoints: son el contenedor del que cuelgan los **permisos**. Un permiso es un claim del **rol** (tabla `AspNetRoleClaims`), y cambiar lo que puede hacer un rol es una llamada a la API, no un redespliegue.

| | Por roles (`[Authorize(Roles = "Admin")]`) | Por permisos (`[Authorize(Policy = Permissions.Products.Delete)]`) |
| --- | --- | --- |
| Qué dice el endpoint | "Solo Admin" | "Requiere borrar productos" |
| Dónde está la regla | Compilada en el código | En la base de datos (claims del rol) |
| Dar a Manager el permiso de borrar | Cambiar código y redesplegar | `POST /admin/roles/Manager/permissions` |
| Añadir un permiso nuevo | Editar atributos | Un dato nuevo; no hace falta una política por permiso |

## 2. El modelo de datos

No hay tablas nuevas: `IdentityDbContext` ya trae `AspNetRoleClaims`, que es donde viven los permisos.

```
AspNetUsers ──< AspNetUserRoles >── AspNetRoles ──< AspNetRoleClaims
   (Id)          (UserId, RoleId)     (Id, Name)      (RoleId, ClaimType = "permission",
                                                                ClaimValue = "Permissions.Products.View")
```

| Usuario | Rol | Permisos del rol (filas en `AspNetRoleClaims`) |
| --- | --- | --- |
| admin@example.com | Admin | Products.View, Create, Edit, Delete |
| manager@example.com | Manager | Products.View, Create |
| user@example.com | User | Products.View |

Un usuario hereda los permisos de **todos sus roles** (la unión). Dar un permiso a un rol lo da a todos sus usuarios.

## 3. Las piezas

| Archivo | Responsabilidad |
| --- | --- |
| `Entities/Permissions.cs` | Constantes de permisos, `Prefix`, `ClaimType` y la lista `All` |
| `Auth/PermissionSettings.cs` | El interruptor `Token` / `Lookup` |
| `Authorization/PermissionRequirement.cs` | El requisito: lleva el permiso exigido como dato |
| `Authorization/PermissionAuthorizationHandler.cs` | Decide si quien llama tiene el permiso |
| `Authorization/PermissionPolicyProvider.cs` | Fabrica la política de cualquier `Permissions.*` a pedido |
| `Authorization/PermissionStore.cs` | Lee, concede y revoca permisos de un rol, con caché |
| `Auth/TokenService.cs` | Opcionalmente graba los permisos en el JWT |
| `Data/IdentitySeeder.cs` | Siembra roles, permisos de roles y usuarios |
| `Controllers/ProductsController.cs`, `Controllers/OrdersController.cs` | Endpoints protegidos por permiso (un controller por módulo) |
| `Controllers/RolePermissionsController.cs` | Gestionar permisos de un rol en caliente |
| `Controllers/DiagnosticsController.cs` | Mide el costo de los permisos en el token |

## 4. Paso a paso

### Paso 1: los permisos como constantes

```csharp
public static class Permissions
{
    public const string ClaimType = "permission";
    public const string Prefix = "Permissions.";

    public static class Products
    {
        public const string View   = "Permissions.Products.View";
        public const string Create = "Permissions.Products.Create";
        public const string Edit   = "Permissions.Products.Edit";
        public const string Delete = "Permissions.Products.Delete";
    }

    public static class Orders { /* View, Create, Edit, Delete: igual que Products */ }

    private static readonly string[] Modules = [nameof(Products), nameof(Orders)];

    // Se construye a partir de los módulos: no se escribe a mano.
    public static IReadOnlyList<string> All { get; } = Modules.SelectMany(ForModule).ToList();

    public static IReadOnlyList<string> ForModule(string module) =>
        [$"{Prefix}{module}.View", $"{Prefix}{module}.Create", $"{Prefix}{module}.Edit", $"{Prefix}{module}.Delete"];

    public static IEnumerable<string> Expand(string entry) =>   // "Permissions.Orders.*" -> los cuatro
        entry.EndsWith(".*") ? All.Where(p => p.StartsWith(entry[..^1])) : [entry];
}
```

El nombre sigue la forma `Permissions.Módulo.Acción`. El prefijo `Permissions.` es lo que luego permite reconocer que un nombre de política es un permiso.

Las clases anidadas (`Products`, `Orders`) existen para tener constantes en los atributos `[Authorize(Policy = ...)]`. La lista `All` **no repite** las cadenas: la genera `ForModule` a partir de `Modules`. Para añadir un módulo, ver la sección 10.

### Paso 2: un requisito para todos los permisos

```csharp
public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
```

Una sola clase sirve para todo el sistema: el permiso viaja como dato en la instancia, no hay una clase por regla.

### Paso 3: el proveedor de políticas

Sin él, cada permiso exigiría su propio `AddPolicy` al arrancar. Con él, cualquier nombre que empiece por `Permissions.` recibe una política construida a pedido:

```csharp
public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
{
    if (policyName.StartsWith(Permissions.Prefix, StringComparison.OrdinalIgnoreCase))
    {
        var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    return _fallbackProvider.GetPolicyAsync(policyName);   // UserOrAbove, AdminOnly...
}
```

Dos reglas que no se pueden saltar:

- ASP.NET Core usa **un solo** `IAuthorizationPolicyProvider`. Todo lo que este no reconoce se entrega al proveedor por defecto (`_fallbackProvider`), o dejan de resolverse tus políticas con nombre.
- `GetFallbackPolicyAsync` debe devolver la política **fallback**, no la *default*. Copiar `GetDefaultPolicyAsync` ahí aplica en silencio un requisito de usuario autenticado a todo endpoint que nunca lo pidió.

### Paso 4: el handler

```csharp
var granted = settings.Value.Source == PermissionSource.Token
    ? ReadFromToken(context.User)
    : await store.GetForRolesAsync(ReadRoles(context.User));

if (granted.Contains(requirement.Permission))
    context.Succeed(requirement);
```

| Decisión | Motivo |
| --- | --- |
| Si no está autenticado, `return` | Un anónimo recibe **401**, no 403 |
| Nunca llama a `context.Fail()` | Vetaría a otros handlers del mismo requisito |
| Lee los roles con `identity.RoleClaimType` | Fijar `ClaimTypes.Role` se rompe con `MapInboundClaims = false`, que trae el claim corto `role` |

### Paso 5: el almacén de permisos

`PermissionStore` es la fuente de verdad de "qué permisos tiene este rol":

| Método | Qué hace |
| --- | --- |
| `GetForRoleAsync(rol)` | Los permisos del rol; usa caché y, si falta, lee `AspNetRoleClaims` |
| `GetForRolesAsync(roles)` | La unión de varios roles |
| `GrantAsync(rol, permiso)` | Añade el claim al rol y **expulsa la caché** |
| `RevokeAsync(rol, permiso)` | Quita el claim y expulsa la caché |

Es *singleton* y crea su propio *scope* para usar `RoleManager`. La caché evita una consulta a la base en cada petición autorizada; en una app real iría `HybridCache` con un TTL corto, para sobrevivir a reinicios y compartirse entre instancias.

### Paso 6: dos modos para entregar los permisos

`appsettings.json`:

```json
"Permissions": { "Source": "Lookup" }
```

| | `Lookup` (por defecto) | `Token` |
| --- | --- | --- |
| Qué viaja en el JWT | Solo los roles | Roles y todos los permisos |
| Cómo se deciden | Se consultan al `PermissionStore` (con caché) | Se leen de los claims del token |
| Tamaño del token | Pequeño y constante | Crece con cada permiso |
| Revocar un permiso | **Inmediato** | Solo cuando el token expira |
| Costo por petición | Una consulta cacheada | Ninguno |

`TokenService` solo graba los permisos cuando el modo es `Token`:

```csharp
if (_permissions.Source == PermissionSource.Token)
    claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));
```

### Paso 7: el seed

Los permisos se conceden a los **roles**. En `appsettings.json`:

```json
"Seed": {
  "RolePermissions": {
    "Admin":   [ "Permissions.Products.*", "Permissions.Orders.*" ],
    "Manager": [ "Permissions.Products.View", "Permissions.Products.Create",
                 "Permissions.Orders.View", "Permissions.Orders.Create", "Permissions.Orders.Edit" ],
    "User":    [ "Permissions.Products.View" ]
  },
  "Users": [ ... ]
}
```

Una entrada puede ser un permiso concreto o un **comodín de módulo** (`Permissions.Orders.*`), que `Permissions.Expand` convierte en los cuatro permisos del módulo. Así no hay que escribirlos a mano en cada rol.

`IdentitySeeder.SeedAsync` hace, en orden: crear la base, crear los roles (`Roles.All`), conceder los permisos y crear los usuarios. Es idempotente. Valida cada rol y cada permiso contra las listas conocidas y registra una advertencia en lugar de sembrar algo que ninguna política reconoce; un comodín que no coincide con ningún módulo (`Permissions.Fly.*`) también se omite con su advertencia.

### Paso 8: registrar todo

`Auth/Policies.cs` (`AddAuthorizationPolicies`) agrupa el registro:

```csharp
services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    // ...políticas de roles y claims de las guías anteriores...

services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
services.AddSingleton<PermissionStore>();
```

La **política fallback** hace la API segura por defecto: todo endpoint exige un usuario autenticado salvo que opte por salir con `[AllowAnonymous]`. Por eso los endpoints de `AuthController` (`register`, `login`, `refresh`, `revoke`) ya llevan `[AllowAnonymous]`, y en `Program.cs` la documentación también debe salir:

```csharp
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference().AllowAnonymous();
```

En `Program.cs` también se registran `Configure<PermissionSettings>(...GetSection("Permissions"))`, `TokenService` (concreto, para los diagnósticos) y `ProductStore`.

### Paso 9: proteger los endpoints (la parte de controllers)

El artículo usa `RequireAuthorization(permiso)` en minimal API. Con controllers es `[Authorize(Policy = permiso)]`:

```csharp
[ApiController]
[Route("[controller]")]
public class ProductsController(ProductStore store) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.Products.View)]
    public ActionResult<IEnumerable<Product>> GetAll() => Ok(store.GetAll());

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Products.Delete)]
    public IActionResult Delete(int id) => store.Delete(id) ? NoContent() : NotFound();
}
```

El string del permiso **es** el nombre de la política. No hay un solo `AddPolicy` para permisos: `PermissionPolicyProvider` fabrica cada una la primera vez que se pide.

| Minimal API (artículo) | Controllers (este proyecto) |
| --- | --- |
| `group.MapGet(...).RequireAuthorization(Permissions.Products.View)` | `[HttpGet] [Authorize(Policy = Permissions.Products.View)]` |
| `app.MapGroup("/api/admin/roles").RequireAuthorization(...)` | `[Route("admin/roles")] [Authorize(Policy = ...)]` en la clase |
| `.AllowAnonymous()` en el grupo de auth | `[AllowAnonymous]` en cada acción |
| `Results.Ok(...)`, `Results.NotFound(...)` | `Ok(...)`, `NotFound(...)` |
| Parámetros inyectados en el delegado | Constructor primario del controller |
| `[FromBody]` obligatorio en `MapDelete` | Igual: `[FromBody]` en el `DELETE` con cuerpo |

### Paso 10: gestionar permisos en caliente

`RolePermissionsController` (exige `Permissions.Products.Edit`, así que en el seed solo el Admin):

| Endpoint | Para qué |
| --- | --- |
| `GET /admin/roles/{role}/permissions` | Ver los permisos de un rol |
| `POST /admin/roles/{role}/permissions` | Conceder un permiso |
| `DELETE /admin/roles/{role}/permissions` | Revocar un permiso (con cuerpo `{ "permission": "..." }`) |

Valida el permiso contra `Permissions.All` (400 si no existe) y que el rol exista (404).

## 5. Resultados esperados

| Petición | Admin | Manager | User | Sin token |
| --- | --- | --- | --- | --- |
| `GET /Products` (View) | 200 | 200 | 200 | 401 |
| `POST /Products` (Create) | 201 | 201 | 403 | 401 |
| `PUT /Products/1` (Edit) | 200 | 403 | 403 | 401 |
| `DELETE /Products/1` (Delete) | 204 | 403 | 403 | 401 |
| `GET /admin/roles/Manager/permissions` | 200 | 403 | 403 | 401 |

Sigue la distinción 401/403: **401** = no hay identidad válida; **403** = hay identidad, pero falta el permiso.

## 6. La prueba que importa: revocar en caliente

Con el Admin: `DELETE /admin/roles/Manager/permissions` con `Permissions.Products.Create`. Después el Manager intenta `POST /Products` con el **mismo token** que ya tenía:

| Modo | Resultado | Por qué |
| --- | --- | --- |
| `Lookup` | **403** de inmediato | El handler consulta al store, y el store ya expulsó la caché |
| `Token` | **201** hasta que el token expire | El permiso está grabado dentro del token, que no se puede cancelar |

Se comprobó en ambos modos. En `Token`, un login nuevo ya da 403, y tras volver a conceder el permiso, el token emitido mientras estaba revocado sigue dando 403 hasta renovarlo.

Esa diferencia es el motivo del interruptor: `Lookup` revoca al instante y mantiene el token pequeño; `Token` no consulta nada, pero crece y queda desactualizado.

## 7. Cuánto cuesta poner permisos en el token

`GET /Diagnostics/token-size` firma tokens con 0, 10, 50, 200 y 500 permisos por el mismo camino que un login real. Con 200 permisos el token pesa unos 8 KB, cerca del 26 % del límite de cabeceras de Kestrel (32 KB). Según el artículo, cada permiso añade unos 40 bytes y, detrás de un proxy como nginx, el límite puede alcanzarse alrededor de los 190 permisos.

## 8. Trampas (del artículo y de la práctica)

| Trampa | Efecto | Qué hacer |
| --- | --- | --- |
| `GetFallbackPolicyAsync` devuelve la política *default* | Exige autenticación en todos los endpoints sin avisar | Devolver la política **fallback** |
| Un segundo `IAuthorizationPolicyProvider` | Solo se usa uno | Delegar en el proveedor por defecto lo que no reconoces |
| Olvidar `.AllowAnonymous()` en OpenAPI/Scalar | La documentación responde 401 | Marcar esas rutas como anónimas |
| `ClaimTypes.Role` fijo en el handler | No encuentra los roles con `MapInboundClaims = false` | Leer `identity.RoleClaimType` |
| Llamar a `context.Fail()` | Veta a todos los demás handlers | Dejar el requisito sin cumplir |
| Permisos dentro del token | Un permiso revocado sigue valiendo; el token crece | Usar `Lookup`, o tokens de vida corta |
| El proveedor acepta cualquier `Permissions.*` | Un typo (`Permissions.Prodcuts.View`) produce un 403 eterno | Usar siempre las constantes, nunca strings sueltos |
| Filtrar claims por el emisor `LOCAL AUTHORITY` | Con JWT el emisor de los claims es el del token, así que un filtro así los descarta | No filtrar por emisor al leer permisos |
| El seed solo **añade** permisos | Si revocas uno y reinicias, el seed lo vuelve a conceder | Quitarlo también de `appsettings.json` |
| `Diagnostics` es anónimo | Cualquiera puede medir el token | Es una herramienta de estudio: no lo expongas en producción |

## 9. Cómo probarlo

1. Arranca la app. No hace falta borrar `auth.db`: el esquema no cambió (los permisos son filas en una tabla que ya existía), y el seed añade los permisos de los roles.
2. Abre [ApiConControllers.http](ApiConControllers.http) y recórrelo de arriba abajo con `Permissions:Source` en `Lookup`.
3. Repite con `"Token"` en `appsettings.json` (o con la variable de entorno `Permissions__Source=Token`) y compara el paso de revocar y volver a crear con el mismo token.
4. Llama a `GET /Diagnostics/token-size` y a `GET /Diagnostics/claims` con el token de un usuario para ver los claims reales.

## 10. Añadir un módulo nuevo

Un módulo es un grupo de cuatro permisos (`View`, `Create`, `Edit`, `Delete`) sobre un recurso. `Orders` se añadió como ejemplo y **no exigió tocar** el handler, el proveedor de políticas ni el `PermissionStore`.

| # | Paso | Dónde |
| --- | --- | --- |
| 1 | Declarar la clase anidada con las cuatro constantes | `Entities/Permissions.cs` |
| 2 | Añadir su nombre a `Modules` (con `nameof`) | `Entities/Permissions.cs` |
| 3 | Concederlo a los roles, con el comodín `Permissions.Orders.*` o permiso a permiso | `appsettings.json` → `Seed:RolePermissions` |
| 4 | Crear el controller con `[Authorize(Policy = Permissions.Orders.View)]`, etc. | `Controllers/OrdersController.cs` |
| 5 | Registrar su almacén de datos (si lo tiene) | `Program.cs` |

Paso 1 y 2, el único código de permisos que se escribe:

```csharp
public static class Orders
{
    public const string View   = "Permissions.Orders.View";
    public const string Create = "Permissions.Orders.Create";
    public const string Edit   = "Permissions.Orders.Edit";
    public const string Delete = "Permissions.Orders.Delete";
}

private static readonly string[] Modules = [nameof(Products), nameof(Orders)];   // <- añadir aquí
```

Con el paso 2, `Permissions.All` incluye los cuatro permisos nuevos y todo lo que valida contra esa lista los reconoce: el seed y `POST /admin/roles/{role}/permissions`. Si olvidas el paso 2, el seed los descarta con la advertencia `unknown permission` y el endpoint responde 400.

Resultado con el seed del proyecto (los códigos reales de la prueba):

| Petición | Admin | Manager | User | Sin token |
| --- | --- | --- | --- | --- |
| `GET /Orders` (View) | 200 | 200 | 403 | 401 |
| `POST /Orders` (Create) | 201 | 201 | 403 | 401 |
| `PUT /Orders/1` (Edit) | 200 | 200 | 403 | 401 |
| `DELETE /Orders/2` (Delete) | 204 | 403 | 403 | 401 |

Observa que `User` tiene `Products.View` pero ningún permiso de `Orders`: **cada módulo se concede por separado**, y por eso conviene el nombre `Módulo.Acción`. Un `Delete` genérico valdría para todos los recursos.

**Comprobaciones al añadir un módulo:**

- Que las constantes sigan el patrón `Permissions.<Módulo>.<Acción>` con el mismo nombre que la clase anidada: `ForModule(nameof(Orders))` genera esas cadenas, y si una constante difiere, el endpoint daría 403 aunque el rol tenga el permiso.
- Que `Modules` se declare **antes** que `All`: los inicializadores estáticos corren en orden de aparición.
- Que el rol destino esté en `Roles.All`.

## 11. Qué cambió respecto a la implementación anterior

La versión anterior modelaba permisos **por departamento** con una tabla `DepartmentMemberships`. Esta guía la reemplaza por el patrón del artículo, que no tiene departamentos: los permisos dependen solo del rol.

| Antes (membresías por departamento) | Ahora (artículo) |
| --- | --- |
| Rol por departamento (tabla `DepartmentMemberships`) | Rol global; permisos como claims del rol |
| Permisos `Notes.*` deducidos con `RolePermissions` en código | Permisos `Permissions.Products.*` en `AspNetRoleClaims` |
| `[HasPermission]` y `IMembershipService` | `[Authorize(Policy = ...)]` y `PermissionStore` |
| `NotesController`, `MembershipsController` | `ProductsController`, `RolePermissionsController`, `DiagnosticsController` |
| Siempre consulta a la base | Interruptor `Token` / `Lookup` |

Los claims de usuario `department` y `subscription` (y sus políticas) se conservan. Las guías de la versión anterior (permisos globales y por departamento) se eliminaron del repositorio, y [JWT-Tablas-Claims-Permisos.pptx](JWT-Tablas-Claims-Permisos.pptx) ya está actualizado a este enfoque.

## 12. Antes de replicar este código

Este proyecto es **código de estudio**. La mecánica de autorización sigue lo que Microsoft documenta, pero varias decisiones del proyecto no son aptas para producción. Esta sección dice cuáles, para que quien lo replique las tenga en cuenta. Revisada el 14 de octubre de 2026 contra estas páginas de Microsoft Learn:

- [Custom authorization policy providers](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/custom-authorization-policy-providers)
- [Policy-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [Create and Drop APIs (EnsureCreated)](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created)
- [Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)

> **Los cambios de esta sección son recomendaciones. No están aplicados en el código del proyecto.**

### 12.1 Lo que sí sigue la guía de Microsoft

| Recomendación | En el proyecto |
| --- | --- |
| Un solo `IAuthorizationPolicyProvider`, registrado como singleton, que delega en el proveedor por defecto lo que no reconoce | `PermissionPolicyProvider` |
| `GetFallbackPolicyAsync` delega en el proveedor por defecto | Igual |
| Política fallback que exige usuario autenticado (*secure by default*), con `[AllowAnonymous]` en lo público | `Auth/Policies.cs` y `AuthController` |
| Requisito y handler registrados; el handler no llama a `Fail()` salvo que quiera vetar; no se depende del orden de los handlers | `PermissionAuthorizationHandler` |
| Los handlers se ejecutan aunque la autenticación falle: hay que comprobar que el usuario está autenticado | El handler sale si no lo está |
| La política construida lleva al menos un esquema de autenticación | `JwtBearerDefaults.AuthenticationScheme` |

### 12.2 Lo que no cumple, con su fuente

| # | Tema | Qué dice la fuente | Estado en el proyecto | Qué hacer |
| --- | --- | --- | --- | --- |
| 1 | **Secretos** | Microsoft: *"Never store passwords or other sensitive data in source code or configuration files"*; los secretos no se despliegan con la app; en producción, un medio controlado como Azure Key Vault | La `Key` de firma del JWT y las contraseñas del seed están en `appsettings.json` | Ver 12.3 |
| 2 | **`EnsureCreatedAsync`** | Microsoft: es una alternativa ligera para prototipos, pruebas y datos transitorios; no funciona bien con migraciones y no añade tablas si ya existe alguna | La app guarda usuarios reales y su esquema cambió varias veces | Ver 12.4 |
| 3 | **Atributo tipado** | Microsoft: con un proveedor propio, el enfoque recomendado es un `AuthorizeAttribute` fuertemente tipado; escribir el nombre de la política como string *no* es lo mejor | Se usan constantes (`Permissions.Products.View`), que reducen el riesgo, pero no hay atributo propio | Ver 12.5 |
| 4 | **Validar el nombre de la política** | El proveedor de ejemplo de Microsoft solo crea la política si el nombre se interpreta bien (por ejemplo, la edad es un número válido) | El proveedor acepta cualquier nombre con prefijo `Permissions.`: un typo da 403 para siempre y en silencio | Ver 12.5 |

### 12.3 Secretos fuera del repositorio

En desarrollo, el gestor de secretos; en producción, variables de entorno o Key Vault:

```bash
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Key" "<clave aleatoria de al menos 32 caracteres>"
dotnet user-secrets set "Seed:Users:0:Password" "<contraseña>"
```

Las variables de entorno usan `__`: `JwtSettings__Key`. Después, quita la `Key` y las contraseñas de `appsettings.json` y deja solo valores no sensibles.

Avisos de Microsoft que conviene recordar: el gestor de secretos **no cifra** los valores y es solo para desarrollo, y las variables de entorno suelen guardarse en texto plano.

**Si clonas este repositorio, no reutilices la `Key` del `appsettings.json`:** está en el historial de git, así que debes considerarla comprometida y generar una nueva.

### 12.4 Migraciones en lugar de `EnsureCreatedAsync`

```bash
dotnet ef migrations add Initial
```

y en el seeder, `await db.Database.MigrateAsync();` en lugar de `EnsureCreatedAsync()`.

Microsoft advierte que pasar de `EnsureCreatedAsync` a migraciones **no es transparente**: lo más simple es borrar la base y recrearla con migraciones. En este proyecto ya se vio el síntoma: al añadir una tabla, una base existente no la recibe y aparece `no such table`.

### 12.5 Atributo tipado y proveedor que valida

Un atributo propio, como recomienda Microsoft (la política es el propio permiso):

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute(string permission) : AuthorizeAttribute(permission);

// Uso
[HasPermission(Permissions.Products.Delete)]
```

Y el proveedor, devolviendo `null` si el nombre lleva el prefijo pero no es un permiso real. Así un typo produce un error visible en lugar de un 403 silencioso:

```csharp
if (policyName.StartsWith(Permissions.Prefix, StringComparison.OrdinalIgnoreCase))
{
    if (!Permissions.All.Contains(policyName, StringComparer.OrdinalIgnoreCase))
        return Task.FromResult<AuthorizationPolicy?>(null);

    // ...construir la política como hasta ahora
}
```

### 12.6 Otras consideraciones (criterio propio)

Estas **no** salen de las páginas de Microsoft consultadas: son mi valoración, y conviene contrastarlas con la documentación oficial antes de decidir.

| Tema | Situación en el proyecto | Recomendación |
| --- | --- | --- |
| **Bloqueo de cuenta** | `login` usa `CheckPasswordAsync`, que no cuenta los intentos fallidos: no hay protección contra fuerza bruta | Contar fallos con `AccessFailedAsync`, comprobar `IsLockedOutAsync` y reiniciar con `ResetAccessFailedCountAsync` al acertar; añadir limitación de peticiones (`AddRateLimiter`) |
| **Caché de permisos** | `PermissionStore` guarda los permisos en memoria y no caducan | `HybridCache` con un TTL corto; sin eso, un cambio hecho directamente en la base, o con varias instancias, queda desactualizado |
| **`DiagnosticsController` anónimo** | Cualquiera puede llamarlo | Quitarlo, o limitarlo a desarrollo o a `Policies.AdminOnly` |
| **Emisión propia de JWT** | Clave simétrica (HS256) y emisión desde la propia API | Válido para aprender; en producción suele preferirse un proveedor de identidad (Entra ID, Keycloak…) |
| **Datos de ejemplo** | Usuarios con contraseñas conocidas (`Admin123!`…) | No sembrar usuarios con contraseñas fijas fuera de desarrollo |

### 12.7 Lista de comprobación

- [ ] La `Key` del JWT y las contraseñas están fuera del repositorio y la `Key` es nueva.
- [ ] La base se crea con migraciones.
- [ ] Los endpoints usan un atributo tipado y el proveedor valida el nombre de la política.
- [ ] El login cuenta intentos fallidos y hay limitación de peticiones.
- [ ] Los endpoints de diagnóstico no son públicos.
- [ ] La caché de permisos tiene caducidad, o hay una sola instancia.
- [ ] No hay usuarios sembrados con contraseñas conocidas.

## 13. Para seguir practicando

- Cambiar la caché del `PermissionStore` por `HybridCache` con un TTL corto.
- Un endpoint que liste todos los permisos disponibles (`Permissions.All`).
- Un endpoint para crear roles nuevos y asignarles permisos.
- Migraciones de EF Core en lugar de `EnsureCreatedAsync`.
- Permisos por usuario (excepciones puntuales) sumados a los del rol.
- Si necesitas limitar un permiso a un departamento o a un dato concreto, esto se combina con autorización por recurso (`IAuthorizationService.AuthorizeAsync(User, recurso, política)`).
