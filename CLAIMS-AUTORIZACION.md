# Autorización basada en claims: guía de estudio

Continuación de [ROLES-AUTORIZACION.md](ROLES-AUTORIZACION.md). Los roles responden **"¿qué tipo de usuario eres?"**. Los claims responden a preguntas más finas: **"¿de qué departamento eres?"** o **"¿qué suscripción tienes?"**.

## 1. Qué es un claim

Un claim es una afirmación sobre el usuario: un par **tipo → valor**. Ya los usas: `sub`, `email`, `name` y `role` son claims dentro del JWT. Aquí se añaden dos propios:

| Claim | Valores posibles | Significa |
| --- | --- | --- |
| `department` | `engineering`, `operations`, `none` | Departamento al que pertenece |
| `subscription` | `free`, `standard`, `premium` | Nivel de suscripción |

Un rol es, en el fondo, un claim más (de tipo `role`). La diferencia es práctica:

| | Rol | Claim |
| --- | --- | --- |
| Granularidad | Gruesa: pocos grupos | Fina: cualquier atributo |
| Ejemplo | `Admin`, `Manager` | `department = engineering`, `subscription = premium` |
| Cuándo | "Qué función cumple" | "Qué atributo tiene" (departamento, plan, país, edad…) |
| Combinables | Sí | Sí, incluso con roles |

Regla de decisión: si el permiso depende de **quién eres en la organización**, usa un rol; si depende de **un dato del usuario**, usa un claim.

## 2. El recorrido de un claim

1. **Seed o `set-claim`** guardan el claim en la tabla `AspNetUserClaims` de Identity.
2. En **login y refresh**, el controller lee roles y claims con `userManager.GetRolesAsync` y `GetClaimsAsync`, y se los pasa a `TokenService`.
3. `TokenService` los escribe dentro del JWT (`"department":"engineering"`).
4. En cada petición, JwtBearer reconstruye `User` con esos claims.
5. `[Authorize(Policy = ...)]` evalúa la política contra `User`.

Importante: los claims **viajan dentro del token**. Si cambias un claim, el token que ya está en circulación conserva el valor viejo hasta que expire o se renueve con `/auth/refresh` (ambos releen la base).

## 3. Datos del seed

| Usuario | Rol | `department` | `subscription` |
| --- | --- | --- | --- |
| admin@example.com | Admin | engineering | premium |
| manager@example.com | Manager | operations | standard |
| user@example.com | User | none | free |

## 4. Paso a paso de la implementación

### Paso 1: nombres y valores, sin strings sueltos

`Entities/AppClaimTypes.cs` define los tipos de claim y qué valores acepta cada uno. `Entities/ClaimValues.cs` define los valores.

```csharp
public static class AppClaimTypes
{
    public const string Department = "department";
    public const string Subscription = "subscription";

    private static readonly Dictionary<string, string[]> AllowedValues = new()
    {
        [Department] = Departments.All,
        [Subscription] = Subscriptions.All
    };

    public static bool IsKnownType(string type) => AllowedValues.ContainsKey(type);
    public static bool IsValid(string type, string value) =>
        AllowedValues.TryGetValue(type, out var values) && values.Contains(value);
}
```

Los **valores y los tipos distinguen mayúsculas**: `Engineering` no es `engineering`. Por eso se centralizan: un typo en un string sería un 403 silencioso.

`Subscriptions.All` está ordenado de menor a mayor, y ese orden define la jerarquía (`Rank`).

### Paso 2: guardar los claims con Identity

Identity ya trae la tabla de claims de usuario; no hace falta crear nada. `Auth/UserManagerExtensions.cs` añade un método para dejar **exactamente un claim por tipo**:

```csharp
public static async Task<IdentityResult> SetClaimAsync(
    this UserManager<ApplicationUser> userManager, ApplicationUser user, string type, string value)
```

Si el claim ya está igual, no hace nada; si existe con otro valor, lo quita y pone el nuevo; si no existe, lo añade. Lo usan el seeder y el endpoint `set-claim`, así la regla no se escribe dos veces.

### Paso 3: meter los claims en el token

```csharp
// AuthController.IssueTokensAsync
var roles  = await userManager.GetRolesAsync(user);
var claims = await userManager.GetClaimsAsync(user);
var (accessToken, accessExpiresAt) = tokenService.CreateAccessToken(user, roles, claims);
```

```csharp
// TokenService.CreateAccessToken
claims.AddRange(roles.Select(role => new Claim("role", role)));
claims.AddRange(userClaims);   // department, subscription...
```

Como `register`, `login` y `refresh` pasan por `IssueTokensAsync`, los tres incluyen los claims sin repetir código.

### Paso 4: las políticas

Todas viven en `Auth/Policies.cs`. Hay cuatro tipos de regla:

| Política | Regla | Cómo se escribe |
| --- | --- | --- |
| `EngineeringOnly` | `department` es `engineering` | `RequireClaim("department", "engineering")` |
| `HasDepartment` | `department` es `engineering` **o** `operations` | `RequireClaim("department", "engineering", "operations")` |
| `StandardOrAbove` / `PremiumOnly` | suscripción de ese nivel **o superior** | requisito propio (`SubscriptionRequirement`) |
| `EngineeringAdmin` | rol `Admin` **y** `department = engineering` | `RequireRole(...).RequireClaim(...)` en una sola política |

Reglas para recordar:

- `RequireClaim(tipo, v1, v2)`: el claim debe **existir** y su valor ser v1 **o** v2.
- Los requisitos de **una misma política** se combinan con **Y**. Varios valores dentro de un mismo `RequireClaim` son **O**.

### Paso 5: un requisito propio para "este nivel o superior"

`RequireClaim` solo compara valores exactos: para "standard o premium" tendrías que listarlos a mano. La jerarquía se expresa mejor con un requisito y un handler (`Auth/SubscriptionRequirement.cs`):

```csharp
public class SubscriptionRequirement(string minimum) : IAuthorizationRequirement
{
    public string Minimum { get; } = minimum;
}

public class SubscriptionHandler : AuthorizationHandler<SubscriptionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SubscriptionRequirement requirement)
    {
        var current = context.User.FindFirst(AppClaimTypes.Subscription)?.Value;

        if (Subscriptions.Rank(current) >= Subscriptions.Rank(requirement.Minimum))
            context.Succeed(requirement);

        return Task.CompletedTask;   // no cumple: no llamamos a Fail()
    }
}
```

Piezas: **requisito** (qué se pide, solo datos), **handler** (cómo se evalúa) y **política** (el nombre que se usa en `[Authorize]`). El handler se registra con `services.AddSingleton<IAuthorizationHandler, SubscriptionHandler>()`.

`context.Fail()` es un veto: aunque otro handler diga que sí, la política falla. Para "no cumple" basta con **no llamar a `Succeed`**.

### Paso 6: el seed con claims

`SeedUser` ahora tiene `Department` y `Subscription` (opcionales). El seeder cambió de filosofía:

| | Antes (roles) | Ahora |
| --- | --- | --- |
| Usuario que ya existe | Se saltaba | Se **reconcilia**: se asegura su rol y sus claims |
| Resultado | Solo creaba lo que faltaba | Converge al estado descrito en `appsettings.json` |

Esto es necesario: tus usuarios ya existían en `auth.db` sin claims. Con el diseño anterior, el seed los habría ignorado y nunca habrían recibido `department` ni `subscription`.

```csharp
var user = await userManager.FindByEmailAsync(seed.Email) ?? await CreateUserAsync(seed);
if (user is null) continue;

await EnsureRoleAsync(user, seed.Role);
await EnsureClaimAsync(user, AppClaimTypes.Department, seed.Department);
await EnsureClaimAsync(user, AppClaimTypes.Subscription, seed.Subscription);
```

Además valida cada usuario: un rol, departamento o suscripción desconocidos se omiten con una advertencia (`Seed user '...' skipped: unknown department 'Engineering'`).

Consecuencia a tener en cuenta: el seed es fuente de verdad **en cada arranque**. Si cambias un claim con `set-claim` y reinicias la app, el seed lo devuelve al valor de `appsettings.json`. Es lo esperado para datos de desarrollo; en producción no se configura la sección `Seed`.

### Paso 7: endpoints de demostración

`Controllers/ReportsController.cs`:

| Endpoint | Política | Pasa quien... |
| --- | --- | --- |
| `GET /Reports/me` | solo `[Authorize]` | tenga token. Devuelve los claims del token (para depurar) |
| `GET /Reports/department` | `HasDepartment` | sea de engineering u operations |
| `GET /Reports/engineering` | `EngineeringOnly` | sea de engineering |
| `GET /Reports/standard` | `StandardOrAbove` | tenga standard o premium |
| `GET /Reports/premium` | `PremiumOnly` | tenga premium |
| `GET /Reports/engineering-admin` | `EngineeringAdmin` | sea Admin **y** de engineering |

Además, `POST /auth/set-claim` (solo Admin) cambia un claim de un usuario. Valida tipo y valor contra `AppClaimTypes`.

```json
{ "email": "manager@example.com", "type": "department", "value": "engineering" }
```

## 5. Resultados esperados

| Endpoint | Admin | Manager | User | Sin token |
| --- | --- | --- | --- | --- |
| `/Reports/me` | 200 | 200 | 200 | 401 |
| `/Reports/department` | 200 | 200 | **403** | 401 |
| `/Reports/engineering` | 200 | 403 | 403 | 401 |
| `/Reports/standard` | 200 | 200 | 403 | 401 |
| `/Reports/premium` | 200 | 403 | 403 | 401 |
| `/Reports/engineering-admin` | 200 | 403 | 403 | 401 |

Fíjate en `User` en `/Reports/department`: su claim `department` **existe** (vale `none`), y aun así recibe 403. Es la trampa que explica la sección siguiente.

## 6. Cosas que sorprenden

- **Tener el claim no es lo mismo que tener un valor útil.** `RequireClaim("department")` sin valores aprueba a cualquiera que tenga ese claim, incluido `none`. Por eso `HasDepartment` lista los valores válidos (`engineering`, `operations`) en vez de pedir solo que el claim exista. Otra opción es no crear el claim cuando el usuario no tiene departamento (dejando el campo vacío en el seed).
- **Mayúsculas importan.** `Engineering` ≠ `engineering`, y `Department` ≠ `department`.
- **Un claim cambiado no llega al token viejo.** Tras `set-claim`, el manager sigue con el token anterior (403 en `/engineering`) hasta que haga login o `/auth/refresh`. Se comprobó en las pruebas.
- **Rol y claim son independientes.** Darle `department = engineering` al Manager le abre `/Reports/engineering`, pero `/Reports/engineering-admin` sigue en 403 porque no es Admin.
- **Y y O.** Varios valores en un `RequireClaim` = O. Varios requisitos en una política = Y. Varios `[Authorize]` apilados = Y.
- **El token es legible por cualquiera.** Los claims van firmados, no cifrados. No guardes en ellos datos sensibles.

## 7. Cuándo usar rol, claim o requisito propio

| Necesidad | Herramienta |
| --- | --- |
| "Solo los administradores" | Rol: `RequireRole` |
| "Solo los de ingeniería" | Claim: `RequireClaim(tipo, valor)` |
| "Plan standard o superior" | Requisito + handler (hay orden entre valores) |
| "Mayor de 18 años" / "el dueño del recurso" | Requisito + handler (compara con lógica o con el recurso) |
| Combinación de las anteriores | Una política con varios requisitos (Y) |

## 8. Problemas que había y cómo quedaron

| # | Problema | Corrección |
| --- | --- | --- |
| 1 | `AppClaimTypes` era una clase no estática, sin uso y sin validación | `static class` con valores permitidos y `IsValid` |
| 2 | `TokenService` no incluía claims del usuario | `CreateAccessToken` recibe los claims y los escribe en el JWT |
| 3 | El seed **saltaba los usuarios existentes**, así que tus tres usuarios no recibirían claims | El seeder reconcilia rol y claims de usuarios nuevos y existentes |
| 4 | `SeedUser` no tenía `Department` ni `Subscription` | Propiedades opcionales añadidas |
| 5 | El valor `none` pasaría un `RequireClaim("department")` simple | Política `HasDepartment` con valores explícitos |
| 6 | Sin forma de cambiar un claim sin tocar la base | Endpoint `POST /auth/set-claim` (Admin) |
| 7 | Alta/cambio de claims se habría escrito dos veces (seeder y controller) | Método de extensión `SetClaimAsync` |
| 8 | Valores mal escritos en el seed se sembrarían igual | Validación con advertencia y omisión del usuario |
| 9 | Método `AddRolePolicies` ya no describía lo que hace | Renombrado a `AddAuthorizationPolicies` |

## 9. Cómo probarlo

1. Haz login con los tres usuarios del seed y llama a `GET /Reports/me` con cada token: verás sus claims.
2. Recorre la matriz de la sección 5.
3. Con el Admin: `POST /auth/set-claim` con `manager@example.com`, `department`, `engineering`.
4. Con el **token viejo** del manager: `/Reports/engineering` sigue en 403.
5. Haz login otra vez con el manager (o usa `/auth/refresh`): ahora da 200.
6. Prueba errores: valor `Engineering` (400), tipo `salary` (400), usuario inexistente (404) y `set-claim` con un token de Manager (403).
7. Reinicia la app: el seed devuelve `department` del manager a `operations`.

## 10. Para seguir practicando

- Un requisito propio con datos del recurso, por ejemplo "solo el creador puede editar": `IAuthorizationService.AuthorizeAsync(User, recurso, política)`.
- Un claim numérico (por ejemplo `age`) con un handler que compare `>= 18`.
- Un endpoint `DELETE` para quitar un claim.
- Una `IClaimsTransformation` para añadir claims calculados en cada petición.
- Releer los claims desde la base en cada petición (por ejemplo en `OnTokenValidated`) para que un cambio surta efecto sin esperar al refresh.
