# Pruebas con Swagger, cURL y PowerShell

Cómo probar la API a mano y qué respuesta esperar en cada caso. Los escenarios de cURL de esta guía se ejecutaron contra la API real y los códigos indicados son los obtenidos.

## 1. Antes de empezar

- La API debe estar en marcha (ver [PUESTA-EN-MARCHA.md](PUESTA-EN-MARCHA.md)): `dotnet run --launch-profile http`, en `http://localhost:5224`.
- Usa una base recién creada si quieres reproducir los códigos exactos: algunas pruebas cambian datos (por ejemplo, borran un producto). Para reiniciar, borra `auth.db`.
- Los usuarios de ejemplo son `admin@example.com`, `manager@example.com` y `user@example.com`; sus contraseñas están en [PUESTA-EN-MARCHA.md](PUESTA-EN-MARCHA.md#7-usuarios-de-ejemplo).
- El **access token dura 15 minutos**. Si una prueba que funcionaba empieza a dar 401, inicia sesión de nuevo.
- Todas las rutas son insensibles a mayúsculas (`/Auth/login` = `/auth/login`).

## 2. Con Swagger UI

Abre <http://localhost:5224/swagger>.

1. Despliega **`POST /Auth/login`** → **Try it out**.
2. En el cuerpo escribe:

   ```json
   { "email": "admin@example.com", "password": "Admin123!" }
   ```

3. **Execute**. En la respuesta, copia el valor de **`accessToken`** (sin comillas).
4. Pulsa el botón **Authorize** (arriba a la derecha) y pega el token. **Pega solo el token, sin la palabra `Bearer`**. Pulsa **Authorize** y luego **Close**.
5. Prueba cualquier endpoint: **Try it out** → **Execute**. El candado cerrado indica que la petición lleva el token.

**Para cambiar de usuario:** vuelve a **Authorize**, pulsa **Logout**, inicia sesión con el otro usuario y pega su token. Swagger conserva el último token pegado, y es la causa más común de resultados que parecen incoherentes.

**Qué mirar en la respuesta:** el código HTTP y, si es 400, el campo `errors`. Los 401 y 403 llegan sin cuerpo.

Scalar (<http://localhost:5224/scalar/v1>) ofrece la misma documentación con otra interfaz; el token se introduce en su sección de autenticación Bearer.

## 3. Con cURL (bash, Git Bash, macOS, Linux)

En PowerShell los comandos `curl` se comportan distinto (ver la sección 4).

### Preparación

Define unas funciones y obtén un token por usuario:

```bash
BASE=http://localhost:5224
JSON="Content-Type: application/json"
code() { curl -s -o /dev/null -w "%{http_code}" "$@"; }
login() { curl -s -X POST "$BASE/Auth/login" -H "$JSON" -d "{\"email\":\"$1\",\"password\":\"$2\"}"; }
token() { sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p'; }
refresh() { sed -n 's/.*"refreshToken":"\([^"]*\)".*/\1/p'; }

TOKEN_ADMIN=$(login admin@example.com 'Admin123!' | token)
TOKEN_MANAGER=$(login manager@example.com 'Manager123!' | token)
TOKEN_USER=$(login user@example.com 'User123!' | token)
H_ADMIN="Authorization: Bearer $TOKEN_ADMIN"
H_MANAGER="Authorization: Bearer $TOKEN_MANAGER"
H_USER="Authorization: Bearer $TOKEN_USER"
```

`code` imprime solo el código HTTP, y `H_ADMIN`, `H_MANAGER` y `H_USER` son las cabeceras con el token de cada usuario. Si tienes `jq`, `token()` puede ser `jq -r .accessToken`. Cada comando de los escenarios lleva a la derecha el código esperado.

### A. Autenticación

```bash
code -X POST "$BASE/Auth/login" -H "$JSON" -d '{"email":"admin@example.com","password":"incorrecta"}'   # 401
code "$BASE/Products"                                                                                  # 401
code "$BASE/Products" -H "$H_ADMIN"                                                                    # 200

EMAIL="ana$RANDOM@example.com"
code -X POST "$BASE/Auth/register" -H "$JSON" -d "{\"firstName\":\"Ana\",\"lastName\":\"Lopez\",\"email\":\"$EMAIL\",\"password\":\"Passw0rd!\"}"   # 200
code -X POST "$BASE/Auth/register" -H "$JSON" -d "{\"firstName\":\"Ana\",\"lastName\":\"Lopez\",\"email\":\"$EMAIL\",\"password\":\"Passw0rd!\"}"   # 400
TOKEN_ANA=$(login "$EMAIL" 'Passw0rd!' | token)
```

- Credenciales incorrectas: **401** con un mensaje genérico (no revela si el email existe).
- Sin token: **401**. Con un token válido: **200**.
- Registrar el mismo email dos veces: **400** (el segundo). Se usa `$RANDOM` para que el email sea nuevo cada vez.

### B. Permisos sobre productos

```bash
code "$BASE/Products" -H "$H_USER"                                                                    # 200
code -X POST "$BASE/Products" -H "$JSON" -H "$H_USER" -d '{"name":"Desk","price":429}'                # 403
code -X POST "$BASE/Products" -H "$JSON" -H "$H_MANAGER" -d '{"name":"Desk","price":429}'             # 201
code -X PUT "$BASE/Products/2" -H "$JSON" -H "$H_MANAGER" -d '{"name":"Monitor","price":700}'         # 403
code -X PUT "$BASE/Products/2" -H "$JSON" -H "$H_ADMIN" -d '{"name":"Monitor","price":700}'           # 200
code -X DELETE "$BASE/Products/1" -H "$H_MANAGER"                                                     # 403
code -X DELETE "$BASE/Products/1" -H "$H_ADMIN"                                                       # 204
code -X DELETE "$BASE/Products/1" -H "$H_ADMIN"                                                       # 404
```

Cada acción exige un permiso distinto: `View` (listar), `Create`, `Edit` y `Delete`. Manager solo tiene `View` y `Create`; User, solo `View`. Un `403` significa "estás autenticado, pero te falta el permiso".

### C. Segundo módulo: pedidos

```bash
code "$BASE/Orders" -H "$H_MANAGER"                                          # 200
code "$BASE/Orders" -H "$H_USER"                                             # 403
code -X DELETE "$BASE/Orders/2" -H "$H_MANAGER"                              # 403
code -X DELETE "$BASE/Orders/2" -H "$H_ADMIN"                                # 204
```

User puede ver productos pero **no tiene ningún permiso de `Orders`**: cada módulo se concede por separado.

### D. Gestionar los permisos de un rol en caliente

```bash
code "$BASE/admin/roles/Manager/permissions" -H "$H_MANAGER"                                                                              # 403
code -X DELETE "$BASE/admin/roles/Manager/permissions" -H "$JSON" -H "$H_ADMIN" -d '{"permission":"Permissions.Products.Create"}'        # 200
code -X POST "$BASE/Products" -H "$JSON" -H "$H_MANAGER" -d '{"name":"Lamp","price":59}'                                                  # 403
code -X POST "$BASE/admin/roles/Manager/permissions" -H "$JSON" -H "$H_ADMIN" -d '{"permission":"Permissions.Products.Create"}'          # 200
code -X POST "$BASE/Products" -H "$JSON" -H "$H_MANAGER" -d '{"name":"Lamp","price":59}'                                                  # 201
code -X POST "$BASE/admin/roles/Manager/permissions" -H "$JSON" -H "$H_ADMIN" -d '{"permission":"Permissions.Fly.View"}'                  # 400
```

Para ver los permisos que tiene hoy el rol:

```text
curl -s "$BASE/admin/roles/Manager/permissions" -H "$H_ADMIN"
```

El Manager vuelve a crear productos **con el mismo token** que tenía: en modo `Lookup` el cambio vale en la siguiente petición. En modo `Token` el resultado sería distinto; ver la [sección 6 de la guía de permisos](PERMISSION-BASED-AUTHORIZATION.md#6-la-prueba-que-importa-revocar-en-caliente).

### E. Roles y claims

```bash
code "$BASE/WeatherForecast" -H "$H_MANAGER"                 # 200
code "$BASE/WeatherForecast" -H "$H_USER"                    # 403
code "$BASE/WeatherForecast/0" -H "$H_USER"                  # 200
code "$BASE/Reports/premium" -H "$H_ADMIN"                   # 200
code "$BASE/Reports/premium" -H "$H_MANAGER"                 # 403
code "$BASE/Reports/engineering-admin" -H "$H_ADMIN"         # 200
code "$BASE/Reports/department" -H "$H_USER"                 # 403
```

`WeatherForecast` usa políticas por **rol** y `Reports` usa políticas por **claims** (`department` y `subscription`). Para ver los claims de tu token:

```text
curl -s "$BASE/Reports/me" -H "$H_MANAGER"
```

### F. Refresh tokens

```bash
RT=$(login manager@example.com 'Manager123!' | refresh)
code -X POST "$BASE/Auth/refresh" -H "$JSON" -d "{\"refreshToken\":\"$RT\"}"    # 200
code -X POST "$BASE/Auth/refresh" -H "$JSON" -d "{\"refreshToken\":\"$RT\"}"    # 401

RT2=$(login user@example.com 'User123!' | refresh)
code -X POST "$BASE/Auth/revoke" -H "$JSON" -d "{\"refreshToken\":\"$RT2\"}"    # 200
code -X POST "$BASE/Auth/refresh" -H "$JSON" -d "{\"refreshToken\":\"$RT2\"}"   # 401
```

- Un refresh token **sirve una sola vez**: el primer uso da **200** y un par nuevo; reutilizar el mismo da **401** (se trata como un posible robo y se revocan todos los tokens activos de ese usuario).
- Tras `revoke`, el refresh token ya no se puede usar.
- El access token ya emitido sigue funcionando hasta que expire.

### G. Administración de roles y claims

```bash
code "$BASE/WeatherForecast" -H "Authorization: Bearer $TOKEN_ANA"                                                     # 403
code -X POST "$BASE/Auth/add-role" -H "$JSON" -H "$H_MANAGER" -d "{\"email\":\"$EMAIL\",\"role\":\"Manager\"}"          # 403
code -X POST "$BASE/Auth/add-role" -H "$JSON" -H "$H_ADMIN" -d "{\"email\":\"$EMAIL\",\"role\":\"Manager\"}"            # 200
code -X POST "$BASE/Auth/add-role" -H "$JSON" -H "$H_ADMIN" -d "{\"email\":\"$EMAIL\",\"role\":\"Manager\"}"            # 409
code "$BASE/WeatherForecast" -H "Authorization: Bearer $TOKEN_ANA"                                                     # 403
TOKEN_ANA=$(login "$EMAIL" 'Passw0rd!' | token)
code "$BASE/WeatherForecast" -H "Authorization: Bearer $TOKEN_ANA"                                                     # 200
code -X POST "$BASE/Auth/set-claim" -H "$JSON" -H "$H_ADMIN" -d "{\"email\":\"$EMAIL\",\"type\":\"subscription\",\"value\":\"premium\"}"   # 200
code -X POST "$BASE/Auth/set-claim" -H "$JSON" -H "$H_ADMIN" -d "{\"email\":\"$EMAIL\",\"type\":\"subscription\",\"value\":\"gold\"}"      # 400
```

Fíjate en las líneas con `WeatherForecast`: tras `add-role`, el **token viejo de Ana sigue dando 403**; solo un token nuevo (volviendo a iniciar sesión) incluye el rol. Los roles y los claims viajan dentro del token y no se actualizan hasta renovarlo.

### H. Diagnóstico

```bash
code "$BASE/Diagnostics/claims"                              # 200
code "$BASE/Diagnostics/token-size?permissions=50"           # 200
```

`token-size` calcula cuánto pesaría un token con N permisos dentro (modo `Token`). `claims` muestra los claims de quien llama, con el emisor de cada uno. Son endpoints anónimos de estudio.

### Leer un token

Para ver el contenido de un JWT, pégalo en [jwt.io](https://jwt.io), o decodifica la parte central:

```text
echo "$TOKEN_ADMIN" | cut -d. -f2 | base64 -d 2>/dev/null
```

Verás `sub`, `email`, `name`, `role`, `department`, `subscription`, `jti` y las fechas. En modo `Lookup` **no** aparecen los permisos.

## 4. Con PowerShell

`curl` en Windows PowerShell 5.1 es un alias de `Invoke-WebRequest`, y para usar el programa real hay que escribir `curl.exe`. Además, **las comillas del JSON se pierden** al pasar a un programa externo. Resultado de probar cuatro formas:

| Forma | Resultado |
| --- | --- |
| `curl.exe ... -d '{"email":"..."}'` (JSON entre comillas simples) | ❌ **400**: PowerShell 5.1 quita las comillas internas |
| `curl.exe ... -d "{\`"email\`":\`"...\`"}"` (comillas escapadas) | ✅ 200 |
| `curl.exe ... -d "@login.json"` (JSON en un archivo) | ✅ 200 |
| `Invoke-RestMethod` | ✅ 200 (la opción más cómoda) |

### Con `Invoke-RestMethod`

```powershell
$base = "http://localhost:5224"

function Get-Token($email, $password) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    (Invoke-RestMethod -Method Post -Uri "$base/Auth/login" -ContentType "application/json" -Body $body).accessToken
}

$admin   = Get-Token "admin@example.com"   "Admin123!"
$manager = Get-Token "manager@example.com" "Manager123!"
$user    = Get-Token "user@example.com"    "User123!"

Invoke-RestMethod "$base/Products" -Headers @{ Authorization = "Bearer $admin" }
```

### Ver solo el código de estado

Funciona en Windows PowerShell 5.1 y en PowerShell 7:

```powershell
function Test-Api($method, $path, $token, $body) {
    $params = @{ Method = $method; Uri = "$base$path"; ContentType = "application/json"; UseBasicParsing = $true }
    if ($token) { $params.Headers = @{ Authorization = "Bearer $token" } }
    if ($body)  { $params.Body = $body }
    try { (Invoke-WebRequest @params).StatusCode } catch { [int]$_.Exception.Response.StatusCode }
}

Test-Api GET    "/Products"    $admin                                       # 200
Test-Api GET    "/Products"    $null                                        # 401
Test-Api POST   "/Products"    $user    '{"name":"Desk","price":429}'       # 403
Test-Api POST   "/Products"    $manager '{"name":"Desk","price":429}'       # 201
Test-Api DELETE "/Products/2"  $manager                                     # 403
```

Los escenarios de la sección 3 se reproducen igual cambiando el método, la ruta, el token y el cuerpo.

## 5. Con el archivo `.http`

[ApiConControllers.http](ApiConControllers.http) trae las peticiones ya escritas para la extensión **REST Client** de VS Code (o el editor de archivos `.http` de Visual Studio).

1. Abre el archivo y pulsa **Send Request** sobre `adminLogin`, `managerLogin` y `userLogin` (llevan `# @name`).
2. Las demás peticiones usan `{{adminLogin.response.body.accessToken}}` y equivalentes, así que no hay que copiar tokens a mano.
3. El archivo recorre la demostración de permisos: borrar sin token (401), como User (403) y como Admin (204), la revocación en caliente y el módulo `Orders`.

Si cambias el puerto, edita la variable `@host` del inicio.

## 6. Resultados esperados

Los códigos de las matrices salen de ejecutar los escenarios anteriores. Cada celda es el código HTTP; "—" indica sin token.

| Petición | Admin | Manager | User | Sin token |
| --- | --- | --- | --- | --- |
| `GET /Products` | 200 | 200 | 200 | 401 |
| `POST /Products` | 201 | 201 | 403 | 401 |
| `PUT /Products/{id}` | 200 | 403 | 403 | 401 |
| `DELETE /Products/{id}` | 204 | 403 | 403 | 401 |
| `GET /Orders` | 200 | 200 | 403 | 401 |
| `POST /Orders` | 201 | 201 | 403 | 401 |
| `PUT /Orders/{id}` | 200 | 200 | 403 | 401 |
| `DELETE /Orders/{id}` | 204 | 403 | 403 | 401 |
| `GET /admin/roles/{role}/permissions` | 200 | 403 | 403 | 401 |
| `GET /WeatherForecast` | 200 | 200 | 403 | 401 |
| `GET /WeatherForecast/{id}` | 200 | 200 | 200 | 401 |
| `GET /Reports/department` | 200 | 200 | 403 | 401 |
| `GET /Reports/engineering` | 200 | 403 | 403 | 401 |
| `GET /Reports/standard` | 200 | 200 | 403 | 401 |
| `GET /Reports/premium` | 200 | 403 | 403 | 401 |
| `GET /Reports/engineering-admin` | 200 | 403 | 403 | 401 |
| `GET /Reports/me` | 200 | 200 | 200 | 401 |
| `POST /Auth/add-role`, `POST /Auth/set-claim` | 200 | 403 | 403 | 401 |

**Cómo leer los códigos**

| Código | Significado en esta API |
| --- | --- |
| 200 / 201 / 204 | Correcto (lectura, creación, borrado sin contenido) |
| 400 | Datos inválidos: email repetido, permiso o valor desconocido, cuerpo mal formado |
| 401 | **No hay identidad válida**: sin token, token vencido o firma incorrecta |
| 403 | **Hay identidad, pero falta el rol, el claim o el permiso** |
| 404 | El recurso no existe (producto, pedido o rol) |
| 409 | Conflicto: el usuario ya tiene ese rol |

## 7. Si algo no sale como esperabas

| Síntoma | Causa probable | Solución |
| --- | --- | --- |
| Todo da 401 de repente | El access token venció (15 min) | Vuelve a iniciar sesión |
| Un `403` que antes era `200` | Probaste con otro usuario, o se revocó un permiso o un rol | Revisa con `GET /Reports/me` los claims de tu token; reinicia `auth.db` para restaurar el seed |
| El resultado es distinto al de la tabla | Una prueba anterior cambió datos (un producto borrado, un permiso revocado) | Reinicia la base (borra `auth.db`) |
| `400` con cuerpo `'e' is an invalid start of a property name` en PowerShell | Las comillas del JSON se perdieron | Usa `Invoke-RestMethod` o un archivo (sección 4) |
| Swagger muestra el candado abierto | No hay token cargado o se cerró la sesión | Vuelve a **Authorize** y pega el token |
| `404` en `/swagger` | La app no está en `Development` | Arranca con un perfil de `launchSettings.json` o define `ASPNETCORE_ENVIRONMENT=Development` |
| Un cambio de rol no se refleja | El token viejo sigue con los roles anteriores | Inicia sesión de nuevo o usa `POST /Auth/refresh` |

## 8. Más pruebas

- **Modo `Token` frente a `Lookup`:** arranca con `Permissions__Source=Token` y repite el escenario D con el mismo token: el permiso revocado **sigue valiendo** hasta que el token expire. Está explicado en la [sección 6 de la guía de permisos](PERMISSION-BASED-AUTHORIZATION.md#6-la-prueba-que-importa-revocar-en-caliente).
- **Añadir un módulo nuevo y probarlo:** [sección 10 de la guía de permisos](PERMISSION-BASED-AUTHORIZATION.md#10-añadir-un-módulo-nuevo).
- **Pruebas automáticas:** el proyecto no tiene un proyecto de pruebas. Una mejora natural es `WebApplicationFactory` con estos mismos escenarios.
