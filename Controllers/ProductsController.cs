using ApiConControllers.Entities;
using ApiConControllers.Models;
using ApiConControllers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ApiConControllers.Controllers;

// En el artículo cada endpoint de minimal API llama a RequireAuthorization(permiso). Con controllers
// es [Authorize(Policy = permiso)]: el string del permiso ES el nombre de la política, y no hay ningún
// AddPolicy en ninguna parte: PermissionPolicyProvider construye cada una la primera vez que se pide.
[ApiController]
[Route("[controller]")]
public class ProductsController(ProductStore store) : ControllerBase
{
    /// <summary>Lista los productos. Requiere Permissions.Products.View.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Products.View)]
    public ActionResult<IEnumerable<Product>> GetAll() => Ok(store.GetAll());

    /// <summary>Crea un producto. Requiere Permissions.Products.Create.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Products.Create)]
    public ActionResult<Product> Create(SaveProductRequest request)
    {
        var product = store.Add(request);
        return Created($"/Products/{product.Id}", product);
    }

    /// <summary>Modifica un producto. Requiere Permissions.Products.Edit.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.Products.Edit)]
    public ActionResult<Product> Update(int id, SaveProductRequest request)
    {
        var product = store.Update(id, request);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Elimina un producto. Requiere Permissions.Products.Delete.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Products.Delete)]
    public IActionResult Delete(int id) => store.Delete(id) ? NoContent() : NotFound();
}
