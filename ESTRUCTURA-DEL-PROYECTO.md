# Estructura del proyecto

Dónde está cada pieza, cómo se conectan y qué tocar para cada tipo de cambio.

## 1. Vista general

```text
ApiConControllers/
├── Program.cs                      Arranque: servicios, autenticación, seed y pipeline
├── appsettings.json                Configuración: JWT, permisos, base de datos y seed
├── appsettings.Development.json    Niveles de log en desarrollo
├── ApiConControllers.csproj        Proyecto net10.0 y paquetes NuGet
├── ApiConControllers.slnx          Solución
├── ApiConControllers.http          Peticiones de prueba (REST Client / Visual Studio)
├── Properties/
│   └── launchSettings.json         Perfiles de ejecución: http (5224) y https (7102)
│
├── Controllers/                    Endpoints HTTP
├── Auth/                           JWT, políticas de roles y claims, configuración de tokens
├── Authorization/                  Permisos: requisito, handler, proveedor de políticas y almacén
├── Data/                           DbContext (EF Core) y seed
├── Entities/                       Entidades y constantes del dominio
├── Models/                         DTOs de entrada y salida
├── Services/                       Almacenes en memoria de ejemplo (productos y pedidos)
├── Middlewears/                    Middlewares propios
│
├── README.md                       Presentación del proyecto
├── PUESTA-EN-MARCHA.md             Instalar, configurar y ejecutar
├── PRUEBAS-SWAGGER-CURL.md         Probar la API y códigos esperados
├── ESTRUCTURA-DEL-PROYECTO.md      Este documento
├── *-AUTORIZACION.md, ...          Guías de estudio (ver la sección 9)
├── LICENSE                         Licencia MIT
└── THIRD-PARTY-NOTICES.md          Avisos de licencias de terceros
```

Las carpetas `bin/` y `obj/` (compilación) y el archivo `auth.db` (base SQLite) se generan al compilar y ejecutar; no forman parte del código.

## 2. Carpeta por carpeta

### `Controllers/`: los endpoints

| Archivo | Rutas | Protección |
| --- | --- | --- |
| `AuthController.cs` | `/Auth/register`, `login`, `refresh`, `revoke`, `add-role`, `set-claim` | Las cuatro primeras, anónimas; las dos últimas, `AdminOnly` |
| `ProductsController.cs` | `/Products` | Permisos `Permissions.Products.*` |
| `OrdersController.cs` | `/Orders` | Permisos `Permissions.Orders.*` |
| `RolePermissionsController.cs` | `/admin/roles/{role}/permissions` | Permiso `Permissions.Products.Edit` |
| `WeatherForecastController.cs` | `/WeatherForecast` | Políticas por **rol** |
| `ReportsController.cs` | `/Reports/...` | Políticas por **claims** |
| `DiagnosticsController.cs` | `/Diagnostics/claims`, `token-size` | Anónimo (herramienta de estudio) |

### `Auth/`: tokens y políticas por rol y claim

| Archivo | Responsabilidad |
| --- | --- |
| `TokenService.cs`, `ITokenService.cs` | Crea y firma el JWT, el refresh token y su hash |
| `JwtSettings.cs` | Sección `JwtSettings` de la configuración |
| `PermissionSettings.cs` | Sección `Permissions`: modo `Token` o `Lookup` |
| `Policies.cs` | Políticas por rol y claim, la política **fallback**, y el registro de los componentes de permisos |
| `SubscriptionRequirement.cs` | Requisito propio: nivel de suscripción mínimo |
| `UserManagerExtensions.cs` | `SetClaimAsync`: deja un único claim de un tipo en un usuario |

### `Authorization/`: autorización por permisos

| Archivo | Responsabilidad |
| --- | --- |
| `PermissionRequirement.cs` | Un único tipo de requisito para todos los permisos |
| `PermissionAuthorizationHandler.cs` | Decide si quien llama tiene el permiso, según el modo |
| `PermissionPolicyProvider.cs` | Fabrica la política de cualquier nombre `Permissions.*` |
| `PermissionStore.cs` | Lee, concede y revoca los permisos de un rol, con caché |

### `Data/`: persistencia y datos iniciales

| Archivo | Responsabilidad |
| --- | --- |
| `AppDbContext.cs` | `IdentityDbContext` más la tabla `RefreshTokens` |
| `IdentitySeeder.cs` | Crea la base, los roles, los permisos de los roles y los usuarios de ejemplo |
| `SeedOptions.cs` | Clases que reciben la sección `Seed` de la configuración |

### `Entities/`: dominio y constantes

| Archivo | Contenido |
| --- | --- |
| `ApplicationUser.cs` | `IdentityUser` con `FirstName` y `LastName` |
| `RefreshToken.cs` | Fila de la tabla de refresh tokens (guarda el hash) |
| `Roles.cs` | `User`, `Manager`, `Admin` y la lista `All` |
| `Permissions.cs` | Permisos `Permissions.<Módulo>.<Acción>`, módulos y comodines |
| `AppClaimTypes.cs`, `ClaimValues.cs` | Tipos y valores de `department` y `subscription` |

### `Models/`, `Services/` y `Middlewears/`

| Archivo | Contenido |
| --- | --- |
| `Models/AuthModels.cs` | Peticiones de autenticación y `AuthResponse` |
| `Models/ProductModels.cs`, `OrderModels.cs` | Modelos y peticiones de los módulos de ejemplo |
| `Services/ProductStore.cs`, `OrderStore.cs` | Listas en memoria (se pierden al reiniciar, a propósito) |
| `Middlewears/RequestLoggingMiddelwear.cs` | Registra cada petición y su respuesta |
| `Middlewears/BasicAuthMiddleweare.cs` | Autenticación básica **sin usar**: quedó de un paso anterior |
| `WeatherForecast.cs` | Modelo del ejemplo de clima |

> Los nombres `Middlewears` y `...Middelwear.cs` conservan la ortografía original del proyecto.

## 3. Base de datos

SQLite en el archivo `auth.db`. Las tablas vienen de Identity más una propia:

```text
AspNetUsers ──< AspNetUserRoles >── AspNetRoles ──< AspNetRoleClaims   (permisos del rol)
     │
     ├──< AspNetUserClaims                                             (department, subscription)
     └──< RefreshTokens                                                (solo el hash del token)
```

El detalle de cada tabla y cómo se leen para armar el JWT está en la presentación [JWT-Tablas-Claims-Permisos.pptx](JWT-Tablas-Claims-Permisos.pptx) y en [PERMISSION-BASED-AUTHORIZATION.md](PERMISSION-BASED-AUTHORIZATION.md#2-el-modelo-de-datos).

## 4. Pipeline de una petición

Orden de los middlewares en `Program.cs`:

| # | Middleware | Qué hace |
| --- | --- | --- |
| 1 | `UseCors` | Aplica la política de orígenes permitidos |
| 2 | Swagger, Scalar, OpenAPI (solo `Development`) | Documentación, marcada como anónima |
| 3 | `UseHttpsRedirection` | Redirige HTTP a HTTPS |
| 4 | `UseAuthentication` | Valida el JWT y construye el usuario |
| 5 | `UseAuthorization` | Evalúa roles, claims y permisos del endpoint |
| 6 | `UseRequestLogging` | Registra la petición y la respuesta |
| 7 | `MapControllers` | Ejecuta el controller |

Antes de aceptar peticiones, `await app.SeedIdentityAsync()` crea la base y los datos iniciales.

`UseAuthentication` debe ir **antes** de `UseAuthorization`; si se invierten, todo responde 401.

## 5. Registro de servicios (inyección de dependencias)

| Servicio | Vida | Quién lo usa |
| --- | --- | --- |
| `AppDbContext` | Scoped | Identity, controllers y el seeder |
| `TokenService` / `ITokenService` | Scoped | `AuthController`, `DiagnosticsController` |
| `IdentitySeeder` | Scoped | Arranque |
| `PermissionStore` | **Singleton** | Handler, `AuthController`, seeder y `RolePermissionsController` |
| `PermissionPolicyProvider` | **Singleton** | El framework (debe haber uno solo) |
| `PermissionAuthorizationHandler`, `SubscriptionHandler` | Singleton | La autorización |
| `ProductStore`, `OrderStore` | Singleton | Los controllers de ejemplo |

## 6. Configuración

| Sección de `appsettings.json` | Clase que la recibe |
| --- | --- |
| `JwtSettings` | `Auth/JwtSettings.cs` |
| `Permissions` | `Auth/PermissionSettings.cs` |
| `ConnectionStrings:DefaultConnection` | `AddDbContext` en `Program.cs` |
| `Seed:RolePermissions`, `Seed:Users` | `Data/SeedOptions.cs` |

## 7. Cómo fluye una autorización por permiso

```text
Petición con "Authorization: Bearer <JWT>"
  └─ UseAuthentication            → valida la firma y deja el usuario con sus claims
      └─ UseAuthorization         → el endpoint pide [Authorize(Policy = Permissions.Products.Delete)]
          └─ PermissionPolicyProvider   → fabrica la política desde el prefijo "Permissions."
              └─ PermissionAuthorizationHandler
                    ├─ modo Token  → lee los claims "permission" del token
                    └─ modo Lookup → lee los roles del token y le pregunta a PermissionStore
                                      └─ PermissionStore → caché, o AspNetRoleClaims en la base
```

## 8. Dónde tocar para cada cambio

| Quiero... | Toca... |
| --- | --- |
| Añadir un endpoint protegido por permiso | Un método con `[Authorize(Policy = Permissions.X.Accion)]` en un controller |
| Añadir un **módulo** de permisos | `Entities/Permissions.cs` (clase y `Modules`), `appsettings.json` (`Seed:RolePermissions`) y un controller. Ver la [sección 10 de la guía de permisos](PERMISSION-BASED-AUTHORIZATION.md#10-añadir-un-módulo-nuevo) |
| Cambiar los permisos de un rol | Con la API (`/admin/roles/{role}/permissions`) o en `appsettings.json` |
| Añadir un rol | `Entities/Roles.cs` (constante y `Roles.All`) |
| Añadir un usuario de ejemplo | `appsettings.json` → `Seed:Users` |
| Añadir un tipo de claim | `Entities/AppClaimTypes.cs` y `Entities/ClaimValues.cs`, y una política en `Auth/Policies.cs` |
| Cambiar la duración de los tokens | `appsettings.json` → `JwtSettings` |
| Cambiar el modo `Token` / `Lookup` | `appsettings.json` → `Permissions:Source` |
| Añadir una tabla | Una entidad en `Entities/`, su `DbSet` y su configuración en `Data/AppDbContext.cs`; hay que recrear `auth.db` |
| Añadir un middleware | `Middlewears/` y registrarlo en `Program.cs`, respetando el orden |

## 9. Documentación

| Documento | Tipo |
| --- | --- |
| [README.md](README.md) | Presentación |
| [PUESTA-EN-MARCHA.md](PUESTA-EN-MARCHA.md) | Instalación y ejecución |
| [PRUEBAS-SWAGGER-CURL.md](PRUEBAS-SWAGGER-CURL.md) | Pruebas manuales |
| [AUTENTICACION-JWT.md](AUTENTICACION-JWT.md) | Guía de estudio 1: registro, login y JWT |
| [REFRESH-TOKENS.md](REFRESH-TOKENS.md) | Guía de estudio 2: renovación de sesión |
| [ROLES-AUTORIZACION.md](ROLES-AUTORIZACION.md) | Guía de estudio 3: roles y políticas |
| [CLAIMS-AUTORIZACION.md](CLAIMS-AUTORIZACION.md) | Guía de estudio 4: claims |
| [PERMISSION-BASED-AUTHORIZATION.md](PERMISSION-BASED-AUTHORIZATION.md) | Guía de estudio 5: permisos y consideraciones de producción |
| [JWT-Tablas-Claims-Permisos.pptx](JWT-Tablas-Claims-Permisos.pptx) | Presentación |
