using System.ComponentModel.DataAnnotations;
namespace ApiConControllers.Models;

public record Order(int Id, string Customer, decimal Total);

public record SaveOrderRequest([Required] string Customer, decimal Total);
