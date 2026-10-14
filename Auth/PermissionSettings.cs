namespace ApiConControllers.Auth;

// De dónde lee el handler de autorización los permisos de quien llama.
public enum PermissionSource
{
    // Los permisos se graban en el JWT al iniciar sesión y se leen de vuelta del ClaimsPrincipal.
    // Costo de consulta cero, pero el token crece con cada permiso y queda desactualizado hasta que expire.
    Token,

    // Solo los roles viajan en el token. Los permisos se resuelven en cada petición desde el
    // PermissionStore (con caché). Tokens pequeños, revocación inmediata, una consulta cacheada.
    Lookup
}

// Mapea la sección "Permissions" de appsettings.json.
public class PermissionSettings
{
    public PermissionSource Source { get; set; } = PermissionSource.Lookup;
}
