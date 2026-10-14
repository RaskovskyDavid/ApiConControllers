# ApiConControllers

API web de **ASP.NET Core con controllers** para aprender, paso a paso, autenticación y autorización: JWT, refresh tokens, roles, claims y permisos. Es un proyecto de práctica con documentación de estudio, no un producto listo para producción.

> **Código de estudio.** Guarda la clave del JWT y las contraseñas de ejemplo en `appsettings.json`, crea la base con `EnsureCreatedAsync` y no bloquea cuentas tras intentos fallidos. Antes de usar algo de esto en serio, lee la [sección 12 de la guía de permisos](PERMISSION-BASED-AUTHORIZATION.md#12-antes-de-replicar-este-código).

## Qué incluye

| Tema | Qué hay |
| --- | --- |
| **Autenticación** | Registro y login con ASP.NET Core Identity; JWT firmado (HS256) |
| **Refresh tokens** | Renovación de sesión con rotación, revocación y detección de reutilización; solo se guarda el hash |
| **Roles** | `Admin`, `Manager` y `User`, con políticas con nombre |
| **Claims** | `department` y `subscription` por usuario, y un requisito propio para niveles de suscripción |
| **Permisos** | Permisos como claims de los roles, con proveedor de políticas dinámico, `PermissionStore` con caché y modos `Token` / `Lookup` |
| **Módulos de permisos** | `Products` y `Orders`; añadir un módulo es una clase y una línea |
| **Seed** | Roles, permisos y usuarios de ejemplo sembrados desde `appsettings.json` |
| **API segura por defecto** | Política fallback: todo exige autenticación salvo `[AllowAnonymous]` |

## Tecnologías

.NET 10 · ASP.NET Core Web API (controllers) · ASP.NET Core Identity · Entity Framework Core 10 con SQLite · JWT Bearer · Swagger UI (Swashbuckle) y Scalar

## Inicio rápido

Requiere el [SDK de .NET 10](https://dotnet.microsoft.com/download). Desde la carpeta del proyecto:

```bash
dotnet run --launch-profile http
```

La API queda en `http://localhost:5224`. Abre **<http://localhost:5224/swagger>**, inicia sesión con un usuario de ejemplo (`POST /Auth/login`), copia el `accessToken` y pégalo en el botón **Authorize**.

La primera vez se crea `auth.db` (SQLite) con los roles, los permisos y los usuarios de ejemplo. Los pasos completos, con configuración y solución de problemas, están en [PUESTA-EN-MARCHA.md](PUESTA-EN-MARCHA.md).

## Usuarios de ejemplo

| Email | Contraseña | Rol | Permisos |
| --- | --- | --- | --- |
| `admin@example.com` | `Admin123!` | Admin | Todos los de `Products` y `Orders` |
| `manager@example.com` | `Manager123!` | Manager | `Products`: View, Create. `Orders`: View, Create, Edit |
| `user@example.com` | `User123!` | User | `Products`: View |

## Endpoints principales

| Grupo | Rutas | Protección |
| --- | --- | --- |
| Autenticación | `POST /Auth/register`, `login`, `refresh`, `revoke` | Anónimo |
| Administración | `POST /Auth/add-role`, `POST /Auth/set-claim` | Solo `Admin` |
| Productos | `GET`, `POST /Products`; `PUT`, `DELETE /Products/{id}` | Permisos `Permissions.Products.*` |
| Pedidos | `GET`, `POST /Orders`; `PUT`, `DELETE /Orders/{id}` | Permisos `Permissions.Orders.*` |
| Permisos de un rol | `GET`, `POST`, `DELETE /admin/roles/{role}/permissions` | Permiso `Permissions.Products.Edit` |
| Clima | `GET /WeatherForecast`, `GET`/`DELETE /WeatherForecast/{id}` | Políticas por rol |
| Reportes | `GET /Reports/me`, `department`, `engineering`, `standard`, `premium`, `engineering-admin` | Políticas por claims |
| Diagnóstico | `GET /Diagnostics/claims`, `GET /Diagnostics/token-size` | Anónimo (solo estudio) |

La lista completa con códigos de respuesta esperados está en [PRUEBAS-SWAGGER-CURL.md](PRUEBAS-SWAGGER-CURL.md).

## Documentación

**Para empezar**

| Documento | Contenido |
| --- | --- |
| [PUESTA-EN-MARCHA.md](PUESTA-EN-MARCHA.md) | Requisitos, instalación, configuración, ejecución y problemas frecuentes |
| [PRUEBAS-SWAGGER-CURL.md](PRUEBAS-SWAGGER-CURL.md) | Probar la API con Swagger, cURL, PowerShell y el archivo `.http` |
| [ESTRUCTURA-DEL-PROYECTO.md](ESTRUCTURA-DEL-PROYECTO.md) | Carpetas, archivos, pipeline y dónde tocar para cada cambio |

**Guías de estudio** (conviene leerlas en este orden)

| Guía | Tema |
| --- | --- |
| [AUTENTICACION-JWT.md](AUTENTICACION-JWT.md) | Registro, login y JWT |
| [REFRESH-TOKENS.md](REFRESH-TOKENS.md) | Renovar la sesión, rotación y revocación |
| [ROLES-AUTORIZACION.md](ROLES-AUTORIZACION.md) | Roles, políticas y seed |
| [CLAIMS-AUTORIZACION.md](CLAIMS-AUTORIZACION.md) | Autorización por atributos del usuario |
| [PERMISSION-BASED-AUTHORIZATION.md](PERMISSION-BASED-AUTHORIZATION.md) | Permisos como claims de los roles; qué cambiar antes de producción |

También hay una presentación, [JWT-Tablas-Claims-Permisos.pptx](JWT-Tablas-Claims-Permisos.pptx), que explica cómo se leen las tablas de la base de datos para generar el JWT.

## Licencia

Distribuido bajo la [licencia MIT](LICENSE). Parte del código de permisos está adaptado de un proyecto de terceros, también MIT: ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Créditos

- El patrón de autorización por permisos se basa en el artículo [Permission-Based Authorization in ASP.NET Core](https://codewithmukesh.com/blog/permission-based-authorization-in-aspnet-core/) de Mukesh Murugan (codewithmukesh), adaptado a controllers.
- Las guías se contrastaron con la documentación de [Microsoft Learn](https://learn.microsoft.com/aspnet/core/security/).
