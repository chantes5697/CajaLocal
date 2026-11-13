using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CajaLocal.Models;

namespace CajaLocal.Views;

public partial class PosView : UserControl
{
    private readonly ObservableCollection<Product> _products = [];
    private readonly ObservableCollection<CartLine> _cart = [];
    private ICollectionView? _productsView;
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-MX");

    public PosView()
    {
        InitializeComponent();
        _productsView = CollectionViewSource.GetDefaultView(_products);
        _productsView.Filter = FilterProduct;
        ProductsGrid.ItemsSource = _productsView;
        CartGrid.ItemsSource = _cart;
        ReloadProducts();
        UpdateSummary();
    }

    private void PosView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) ReloadProducts();
    }

    private void ReloadProducts()
    {
        _products.Clear();
        foreach (var product in App.Services.Storage.LoadProducts().Where(x => x.IsActive).OrderBy(x => x.Name))
            _products.Add(product);
        _productsView?.Refresh();
    }

    private bool FilterProduct(object item)
    {
        if (item is not Product product) return false;
        var search = SearchBox?.Text?.Trim() ?? "";
        if (search.Length == 0) return true;
        return product.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || product.Sku.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => _productsView?.Refresh();

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        var term = SearchBox.Text.Trim();
        if (term.Length == 0) return;

        var exactSku = _products.FirstOrDefault(x =>
            x.Sku.Equals(term, StringComparison.CurrentCultureIgnoreCase));
        var firstMatch = exactSku ?? _productsView?.Cast<Product>().FirstOrDefault();
        if (firstMatch is null)
        {
            MessageBox.Show("No se encontró un producto con ese código o nombre.",
                "Búsqueda", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ProductsGrid.SelectedItem = firstMatch;
            AddSelectedProduct();
            SearchBox.Clear();
            SearchBox.Focus();
        }
        e.Handled = true;
    }

    private void ProductsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => AddSelectedProduct();

    private void AddProduct_Click(object sender, RoutedEventArgs e)
        => AddSelectedProduct();

    private void AddSelectedProduct()
    {
        if (ProductsGrid.SelectedItem is not Product product)
        {
            MessageBox.Show("Selecciona un producto del catálogo.", "Punto de venta", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var existing = _cart.FirstOrDefault(x => x.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += 1;
        }
        else
        {
            var line = new CartLine
            {
                ProductId = product.Id,
                Sku = product.Sku,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = 1
            };
            line.PropertyChanged += CartLine_PropertyChanged;
            _cart.Add(line);
        }
        UpdateSummary();
    }

    private void CartLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => UpdateSummary();

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if (CartGrid.SelectedItem is not CartLine selected) return;
        _cart.Remove(selected);
        UpdateSummary();
    }

    private void ClearCart_Click(object sender, RoutedEventArgs e)
    {
        if (_cart.Count == 0) return;
        var answer = MessageBox.Show("¿Vaciar la venta actual?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _cart.Clear();
        CashBox.Clear();
        UpdateSummary();
    }

    private void CashBox_TextChanged(object sender, TextChangedEventArgs e)
        => UpdateSummary();

    private decimal CurrentTotal => _cart.Sum(x => x.Total);

    private decimal CashReceived
    {
        get
        {
            if (decimal.TryParse(CashBox.Text, NumberStyles.Number, MoneyCulture, out var value)) return value;
            if (decimal.TryParse(CashBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return value;
            return 0;
        }
    }

    private void UpdateSummary()
    {
        if (TotalText is null || ChangeText is null) return;
        var total = CurrentTotal;
        TotalText.Text = total.ToString("C2", MoneyCulture);
        var change = Math.Max(0, CashReceived - total);
        ChangeText.Text = change.ToString("C2", MoneyCulture);
    }

    private void Checkout_Click(object sender, RoutedEventArgs e)
    {
        if (_cart.Count == 0)
        {
            MessageBox.Show("Agrega al menos un producto a la venta.", "Punto de venta", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var total = CurrentTotal;
        var received = CashReceived;
        if (received < total)
        {
            MessageBox.Show("El efectivo recibido debe ser igual o mayor al total.", "Punto de venta", MessageBoxButton.OK, MessageBoxImage.Warning);
            CashBox.Focus();
            return;
        }

        var now = DateTime.Now;
        var todaySales = App.Services.Storage.LoadSales().Count(x => x.Date.Date == now.Date);
        var sale = new Sale
        {
            TicketNumber = $"V-{now:yyyyMMdd}-{todaySales + 1:0000}",
            Date = now,
            PaymentMethod = "Efectivo",
            Subtotal = total,
            Total = total,
            CashReceived = received,
            Change = received - total,
            Items = _cart.Select(x => new SaleLine
            {
                ProductId = x.ProductId,
                Sku = x.Sku,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Total = x.Total
            }).ToList()
        };

        var excelWarning = App.Services.SaveSale(sale);
        string? printWarning = null;
        if (App.Services.Settings.AutoPrint)
        {
            try
            {
                App.Services.Printer.PrintSale(App.Services.Settings.PrinterName, sale, App.Services.Settings);
            }
            catch (Exception ex)
            {
                printWarning = "La venta se guardó, pero el ticket no se pudo imprimir: " + ex.Message;
            }
        }

        _cart.Clear();
        CashBox.Clear();
        UpdateSummary();

        var details = $"Venta {sale.TicketNumber} registrada.\nTotal: {sale.Total.ToString("C2", MoneyCulture)}";
        if (!string.IsNullOrWhiteSpace(excelWarning)) details += "\n\n" + excelWarning;
        if (!string.IsNullOrWhiteSpace(printWarning)) details += "\n\n" + printWarning;
        MessageBox.Show(details, "Venta completada", MessageBoxButton.OK,
            string.IsNullOrWhiteSpace(excelWarning) && string.IsNullOrWhiteSpace(printWarning)
                ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
