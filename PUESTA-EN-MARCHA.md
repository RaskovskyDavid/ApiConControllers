# Puesta en marcha

Cómo instalar, configurar y ejecutar el proyecto desde cero. Al terminar tendrás la API corriendo con usuarios de ejemplo para probarla.

## 1. Requisitos

| Requisito | Versión | Para qué |
| --- | --- | --- |
| [SDK de .NET](https://dotnet.microsoft.com/download) | **10.0** o superior (el proyecto apunta a `net10.0`) | Compilar y ejecutar |
| Git | Cualquiera reciente | Clonar el repositorio |
| Un cliente HTTP | `curl`, PowerShell, o la extensión REST Client de VS Code | Probar la API (ver [PRUEBAS-SWAGGER-CURL.md](PRUEBAS-SWAGGER-CURL.md)) |

Comprueba el SDK:

```bash
dotnet --version     # debe mostrar 10.x
```

No hace falta instalar ninguna base de datos: usa SQLite en un archivo local.

## 2. Obtener el código

```bash
git clone https://github.com/RaskovskyDavid/ApiConControllers.git
cd ApiConControllers
```

Los comandos de esta guía se ejecutan desde la carpeta que contiene `ApiConControllers.csproj`.

## 3. Restaurar y compilar

```bash
dotnet restore
dotnet build
```

Deben aparecer `0 Errores`. Es normal ver algunas advertencias de nulabilidad en `BasicAuthMiddleweare.cs`: ese middleware ya no se usa en el pipeline.

## 4. Configuración

Todo está en [appsettings.json](appsettings.json):

| Sección | Qué configura | Valor de ejemplo |
| --- | --- | --- |
| `JwtSettings` | Clave de firma, emisor, audiencia, duración del access token y del refresh token | `ExpiryMinutes: 15`, `RefreshTokenDays: 7` |
| `Permissions:Source` | Dónde se leen los permisos: `Lookup` (por defecto) o `Token` | `"Lookup"` |
| `ConnectionStrings:DefaultConnection` | Base de datos SQLite | `Data Source=auth.db` |
| `Seed:RolePermissions` | Permisos que se conceden a cada rol al arrancar | Admin, Manager y User |
| `Seed:Users` | Usuarios de ejemplo con su rol y sus claims | Ver la sección 7 |

> **Antes de usarlo fuera de tu máquina, cambia la `Key` de `JwtSettings`.** La que viene en el repositorio es de ejemplo y es pública. Lo ideal es no guardarla en `appsettings.json`: usa el gestor de secretos o variables de entorno. Los pasos están en la [sección 12.3 de la guía de permisos](PERMISSION-BASED-AUTHORIZATION.md#123-secretos-fuera-del-repositorio).

### Variables de entorno útiles

Sobrescriben el contenido de `appsettings.json`. El doble guion bajo (`__`) reemplaza a los dos puntos:

| Variable | Efecto |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT=Development` | Activa Swagger, Scalar y OpenAPI (los perfiles de `dotnet run` ya la ponen) |
| `ASPNETCORE_URLS=http://localhost:5299` | Cambia la URL y el puerto |
| `ConnectionStrings__DefaultConnection="Data Source=otra.db"` | Usa otro archivo de base de datos |
| `JwtSettings__Key=<clave de 32+ caracteres>` | Define la clave de firma sin tocar el archivo |
| `Permissions__Source=Token` | Cambia el modo de permisos |

En PowerShell: `$env:Permissions__Source = "Token"`. En bash: `export Permissions__Source=Token`.

## 5. Ejecutar

El proyecto tiene dos perfiles en [Properties/launchSettings.json](Properties/launchSettings.json):

| Perfil | Comando | URLs |
| --- | --- | --- |
| `http` | `dotnet run --launch-profile http` | `http://localhost:5224` |
| `https` | `dotnet run --launch-profile https` | `https://localhost:7102` y `http://localhost:5224` |

```bash
dotnet run --launch-profile http
```

La consola muestra `Now listening on: http://localhost:5224`. Para detener la API, pulsa `Ctrl+C`.

**Aviso conocido con el perfil `http`:** aparece `Failed to determine the https port for redirect`. No es un error: la app tiene `UseHttpsRedirection()` pero ese perfil solo escucha en HTTP. Para evitarlo, usa el perfil `https`.

### HTTPS en desarrollo

El perfil `https` necesita el certificado de desarrollo de .NET. Si es la primera vez:

```bash
dotnet dev-certs https --trust
```

## 6. Qué ocurre en el primer arranque

Antes de aceptar peticiones, la app ejecuta el seed ([Data/IdentitySeeder.cs](Data/IdentitySeeder.cs)), en este orden:

1. Crea el archivo `auth.db` con todas las tablas (`EnsureCreatedAsync`).
2. Crea los roles `User`, `Manager` y `Admin`.
3. Concede a cada rol sus permisos (según `Seed:RolePermissions`).
4. Crea los tres usuarios de ejemplo, con su rol y sus claims.

Es **idempotente**: puedes reiniciar la app sin duplicar nada. Los permisos solo se añaden: si revocas uno con la API y reinicias, el seed lo vuelve a conceder mientras siga en `appsettings.json`.

`auth.db` se crea en el directorio desde el que ejecutas la app (con `dotnet run`, la carpeta del proyecto).

## 7. Usuarios de ejemplo

| Email | Contraseña | Rol | `department` | `subscription` |
| --- | --- | --- | --- | --- |
| `admin@example.com` | `Admin123!` | Admin | engineering | premium |
| `manager@example.com` | `Manager123!` | Manager | operations | standard |
| `user@example.com` | `User123!` | User | none | free |

## 8. Comprobar que funciona

Con la API en marcha, en otra terminal:

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5224/swagger/index.html   # 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5224/Products             # 401 (sin token)
```

Y en el navegador:

| URL | Qué es |
| --- | --- |
| <http://localhost:5224/swagger> | Swagger UI, con el botón **Authorize** para el token |
| <http://localhost:5224/scalar/v1> | Scalar, otra interfaz de documentación |
| <http://localhost:5224/openapi/v1.json> | La especificación OpenAPI (generada por `Microsoft.AspNetCore.OpenApi`) |

Estas rutas solo existen en `Development`. A partir de aquí, sigue con [PRUEBAS-SWAGGER-CURL.md](PRUEBAS-SWAGGER-CURL.md).

## 9. Reiniciar la base de datos

Para empezar de cero, detén la app y borra el archivo:

```bash
rm auth.db            # bash
Remove-Item auth.db   # PowerShell
```

Al volver a arrancar se recrea con el seed. Necesitarás hacerlo cada vez que cambie el esquema (por ejemplo, tras actualizar el código y aparecer `no such table`), porque `EnsureCreatedAsync` no añade tablas a una base que ya existe.

## 10. Problemas frecuentes

| Síntoma | Causa probable | Solución |
| --- | --- | --- |
| `Failed to determine the https port for redirect` | Perfil `http` con `UseHttpsRedirection()` | Es un aviso; usa el perfil `https` si quieres quitarlo |
| `Address already in use` | El puerto 5224 (o 7102) está ocupado, a veces por una ejecución anterior que no terminó | Cierra el otro proceso, o usa `ASPNETCORE_URLS` con otro puerto |
| `no such table: ...` | `auth.db` se creó con una versión anterior del esquema | Borra `auth.db` (sección 9) |
| `database is locked` | Dos instancias usan el mismo archivo `auth.db` | Cierra una de ellas |
| Todo responde 401, incluso con token | Token vencido (15 min), clave distinta a la que lo firmó, o pegaste el prefijo `Bearer` en Swagger | Inicia sesión de nuevo; en Swagger pega solo el token |
| 403 con un token válido | El usuario no tiene el rol o el permiso | Mira la matriz en [PRUEBAS-SWAGGER-CURL.md](PRUEBAS-SWAGGER-CURL.md); recuerda que un cambio de rol no llega a tokens ya emitidos |
| Un cambio en `appsettings.json` no surte efecto | La app lee el archivo al arrancar, o una variable de entorno lo sobrescribe | Reinicia la app y revisa las variables `JwtSettings__...`, `Seed__...` |
| `NETSDK1045: The current .NET SDK does not support targeting .NET 10.0` | Tienes una versión anterior del SDK | Instala el SDK de .NET 10 |
| Los datos de `Products` y `Orders` se pierden al reiniciar | Son listas en memoria a propósito | Es el comportamiento esperado de ese ejemplo |

## 11. Siguientes pasos

- [PRUEBAS-SWAGGER-CURL.md](PRUEBAS-SWAGGER-CURL.md): recorrer los escenarios y los códigos esperados.
- [ESTRUCTURA-DEL-PROYECTO.md](ESTRUCTURA-DEL-PROYECTO.md): entender dónde está cada pieza.
- [AUTENTICACION-JWT.md](AUTENTICACION-JWT.md): la primera guía de estudio.
