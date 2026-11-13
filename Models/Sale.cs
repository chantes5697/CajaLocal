using System.Text.Json.Serialization;

namespace CajaLocal.Models;

public sealed class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketNumber { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public string PaymentMethod { get; set; } = "Efectivo";
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public decimal CashReceived { get; set; }
    public decimal Change { get; set; }
    public List<SaleLine> Items { get; set; } = [];

    [JsonIgnore]
    public decimal QuantitySold => Items.Sum(x => x.Quantity);
}
