using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CajaLocal.Models;

public sealed class Product : INotifyPropertyChanged
{
    private string _sku = "";
    private string _name = "";
    private decimal _price;
    private bool _isActive = true;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Sku
    {
        get => _sku;
        set { _sku = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public decimal Price
    {
        get => _price;
        set { _price = value; OnPropertyChanged(); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
