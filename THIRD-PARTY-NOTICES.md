# Avisos de terceros

Este proyecto se distribuye bajo la licencia MIT (ver [LICENSE](LICENSE)). Parte de su código de autorización por permisos está **adaptado** de un proyecto de terceros, también con licencia MIT, cuyo aviso de copyright debe conservarse.

## codewithmukesh: Permission-Based Authorization in ASP.NET Core

- **Artículo:** <https://codewithmukesh.com/blog/permission-based-authorization-in-aspnet-core/>
- **Código de referencia:** <https://github.com/codewithmukesh/dotnet-webapi-zero-to-hero-course/tree/main/modules/05-api-security/permission-based-authorization-in-aspnet-core>
- **Autor:** Mukesh Murugan

El patrón (permisos como claims de los roles, un único requisito y handler, un proveedor de políticas dinámico, un almacén de permisos con caché y el interruptor `Token` / `Lookup`) y varios fragmentos de código se tomaron de esa referencia y se **adaptaron de minimal API a controllers**. Los archivos afectados son:

| Archivo de este proyecto | Relación con la referencia |
| --- | --- |
| `Entities/Permissions.cs` | Adaptado (constantes, prefijo, `ForModule`); se añadieron módulos y comodines |
| `Authorization/PermissionRequirement.cs` | Adaptado |
| `Authorization/PermissionAuthorizationHandler.cs` | Adaptado |
| `Authorization/PermissionPolicyProvider.cs` | Adaptado |
| `Authorization/PermissionStore.cs` | Adaptado |
| `Auth/PermissionSettings.cs` | Adaptado |
| `Auth/TokenService.cs` | Adaptado (inclusión de permisos y `BuildToken`) |
| `Controllers/RolePermissionsController.cs` | Adaptado de los endpoints de administración |
| `Controllers/DiagnosticsController.cs` | Adaptado de los endpoints de diagnóstico |
| `Controllers/ProductsController.cs`, `Services/ProductStore.cs` | Adaptados |

El resto del proyecto (refresh tokens, claims de usuario, seed por configuración, módulo `Orders`, documentación) es propio.

### Licencia del código de referencia

```
MIT License

Copyright (c) 2026 Mukesh Murugan

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Documentación de Microsoft

Las guías de estudio se contrastaron con la documentación de Microsoft Learn (ASP.NET Core, Entity Framework Core e Identity), citada con enlaces en cada guía. No se copió código de esas páginas; se usan como referencia.
