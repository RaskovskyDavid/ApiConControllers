namespace ApiConControllers.Entities;

// Los permisos son strings con la forma "Permissions.<Módulo>.<Acción>". Las constantes dan seguridad
// en tiempo de compilación en cada endpoint y un único lugar donde renombrarlos.
//
// PARA AÑADIR UN MÓDULO NUEVO (por ejemplo Orders):
//   1. Una clase anidada con sus cuatro constantes (para usarlas en [Authorize(Policy = ...)]).
//   2. Su nombre en "Modules". Con eso "All" lo incluye solo, y el seed y el endpoint de gestión lo reconocen.
//   3. Concederlo a los roles en Seed:RolePermissions, por ejemplo con el comodín "Permissions.Orders.*".
public static class Permissions
{
    // Todos los claims de permiso usan este tipo. El proveedor de políticas se guía por el
    // PREFIJO del valor (abajo), no por este tipo.
    public const string ClaimType = "permission";

    // Un nombre de política que empiece con este prefijo recibe una política generada a pedido.
    public const string Prefix = "Permissions.";

    public static class Products
    {
        public const string View = "Permissions.Products.View";
        public const string Create = "Permissions.Products.Create";
        public const string Edit = "Permissions.Products.Edit";
        public const string Delete = "Permissions.Products.Delete";
    }

    public static class Orders
    {
        public const string View = "Permissions.Orders.View";
        public const string Create = "Permissions.Orders.Create";
        public const string Edit = "Permissions.Orders.Edit";
        public const string Delete = "Permissions.Orders.Delete";
    }

    // Los módulos con permisos. Usa nameof para que el nombre coincida con la clase anidada.
    // OJO: debe declararse ANTES que All (los inicializadores estáticos corren en orden de aparición).
    private static readonly string[] Modules = [nameof(Products), nameof(Orders)];

    // Todos los permisos de la app, construidos a partir de los módulos: no se escriben a mano.
    public static IReadOnlyList<string> All { get; } = Modules.SelectMany(ForModule).ToList();

    // Las cuatro acciones estándar de un módulo, sin escribir a mano las cuatro constantes.
    public static IReadOnlyList<string> ForModule(string module) =>
    [
        $"{Prefix}{module}.View",
        $"{Prefix}{module}.Create",
        $"{Prefix}{module}.Edit",
        $"{Prefix}{module}.Delete"
    ];

    // Expande una entrada de configuración: "Permissions.Orders.*" son todos los permisos del módulo;
    // cualquier otra entrada se devuelve tal cual. Si el comodín no coincide con nada, devuelve vacío.
    public static IEnumerable<string> Expand(string entry) =>
        entry.EndsWith(".*", StringComparison.Ordinal)
            ? All.Where(permission => permission.StartsWith(entry[..^1], StringComparison.Ordinal))
            : [entry];
}
