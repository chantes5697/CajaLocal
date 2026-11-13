using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CajaLocal.Models;

namespace CajaLocal.Views;

public partial class ProductsView : UserControl
{
    private readonly ObservableCollection<Product> _products = [];

    public ProductsView()
    {
        InitializeComponent();
        foreach (var product in App.Services.Storage.LoadProducts().OrderBy(x => x.Name))
            _products.Add(product);
        ProductsGrid.ItemsSource = _products;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Escribe el nombre del producto.", "Producto", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        if (!decimal.TryParse(PriceBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var price)
            && !decimal.TryParse(PriceBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
        {
            MessageBox.Show("Escribe un precio válido.", "Producto", MessageBoxButton.OK, MessageBoxImage.Warning);
            PriceBox.Focus();
            return;
        }
        if (price < 0)
        {
            MessageBox.Show("El precio no puede ser negativo.", "Producto", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ProductsGrid.SelectedItem is Product selected)
        {
            selected.Sku = SkuBox.Text.Trim();
            selected.Name = name;
            selected.Price = price;
        }
        else
        {
            _products.Add(new Product
            {
                Sku = SkuBox.Text.Trim(),
                Name = name,
                Price = price,
                IsActive = true
            });
        }

        Persist();
        ClearForm();
        MessageBox.Show("Producto guardado.", "Productos", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not Product selected)
        {
            MessageBox.Show("Selecciona un producto de la lista.", "Productos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show($"¿Eliminar '{selected.Name}' del catálogo?", "Confirmar eliminación",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        _products.Remove(selected);
        Persist();
        ClearForm();
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not Product product) return;
        SkuBox.Text = product.Sku;
        NameBox.Text = product.Name;
        PriceBox.Text = product.Price.ToString("0.00", CultureInfo.CurrentCulture);
    }

    private void ClearForm()
    {
        ProductsGrid.SelectedItem = null;
        SkuBox.Clear();
        NameBox.Clear();
        PriceBox.Clear();
        NameBox.Focus();
    }

    private void Persist()
    {
        App.Services.Storage.SaveProducts(_products);
        ProductsGrid.Items.Refresh();
    }
}
