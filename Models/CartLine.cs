using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CajaLocal.Models;

public sealed class CartLine : INotifyPropertyChanged
{
    private decimal _quantity = 1;

    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            var safeValue = value <= 0 ? 1 : value;
            if (_quantity == safeValue) return;
            _quantity = safeValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Total));
        }
    }

    public decimal Total => Quantity * UnitPrice;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
