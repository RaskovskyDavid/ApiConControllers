using ApiConControllers.Entities;
using ApiConControllers.Models;
using ApiConControllers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ApiConControllers.Controllers;

// Segundo módulo con permisos propios (Permissions.Orders.*). Para añadirlo no hizo falta tocar el
// handler, el proveedor de políticas ni el almacén: solo la clase Orders de Permissions.cs, su nombre
// en Modules, este controller y la siembra en appsettings.json.
[ApiController]
[Route("[controller]")]
public class OrdersController(OrderStore store) : ControllerBase
{
    /// <summary>Lista los pedidos. Requiere Permissions.Orders.View.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Orders.View)]
    public ActionResult<IEnumerable<Order>> GetAll() => Ok(store.GetAll());

    /// <summary>Crea un pedido. Requiere Permissions.Orders.Create.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    public ActionResult<Order> Create(SaveOrderRequest request)
    {
        var order = store.Add(request);
        return Created($"/Orders/{order.Id}", order);
    }

    /// <summary>Modifica un pedido. Requiere Permissions.Orders.Edit.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.Orders.Edit)]
    public ActionResult<Order> Update(int id, SaveOrderRequest request)
    {
        var order = store.Update(id, request);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Elimina un pedido. Requiere Permissions.Orders.Delete.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Orders.Delete)]
    public IActionResult Delete(int id) => store.Delete(id) ? NoContent() : NotFound();
}
