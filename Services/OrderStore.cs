using System.Collections.Concurrent;
using ApiConControllers.Models;

namespace ApiConControllers.Services;

// Pedidos en memoria. Existe para demostrar un SEGUNDO módulo con sus propios permisos.
public class OrderStore
{
    private readonly ConcurrentDictionary<int, Order> _orders = new();
    private int _nextId;

    public OrderStore()
    {
        Add(new SaveOrderRequest("Acme Corp", 1250.00m));
        Add(new SaveOrderRequest("Globex", 480.50m));
    }

    public IEnumerable<Order> GetAll() => _orders.Values.OrderBy(o => o.Id);

    public Order Add(SaveOrderRequest request)
    {
        var id = Interlocked.Increment(ref _nextId);
        var order = new Order(id, request.Customer, request.Total);
        _orders[id] = order;
        return order;
    }

    public Order? Update(int id, SaveOrderRequest request)
    {
        if (!_orders.ContainsKey(id))
        {
            return null;
        }

        var updated = new Order(id, request.Customer, request.Total);
        _orders[id] = updated;
        return updated;
    }

    public bool Delete(int id) => _orders.TryRemove(id, out _);
}
