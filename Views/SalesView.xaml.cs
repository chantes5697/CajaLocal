using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CajaLocal.Models;

namespace CajaLocal.Views;

public partial class SalesView : UserControl
{
    private readonly ObservableCollection<Sale> _sales = [];
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("es-MX");

    public SalesView()
    {
        InitializeComponent();
        SalesGrid.ItemsSource = _sales;
        ExcelPathText.Text = "Excel: " + App.Services.Storage.ExcelPath;
        LoadSales();
    }

    private void SalesView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) LoadSales();
    }

    private void LoadSales()
    {
        _sales.Clear();
        foreach (var sale in App.Services.Storage.LoadSales().OrderByDescending(x => x.Date))
            _sales.Add(sale);

        var today = _sales.Where(x => x.Date.Date == DateTime.Today).Sum(x => x.Total);
        var all = _sales.Sum(x => x.Total);
        TodayTotalText.Text = today.ToString("C2", MoneyCulture);
        AllTimeTotalText.Text = all.ToString("C2", MoneyCulture);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadSales();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Services.RebuildExcel();
            MessageBox.Show("El archivo de Excel se actualizó correctamente.\n\n" + App.Services.Storage.ExcelPath,
                "Excel actualizado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo actualizar el archivo Excel. Cierra el archivo si está abierto.\n\n" + ex.Message,
                "Error al exportar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Reprint_Click(object sender, RoutedEventArgs e)
    {
        if (SalesGrid.SelectedItem is not Sale sale)
        {
            MessageBox.Show("Selecciona una venta del historial.", "Imprimir copia", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            App.Services.Printer.PrintSale(App.Services.Settings.PrinterName, sale, App.Services.Settings);
            MessageBox.Show("Se envió el ticket a la impresora.", "Impresión", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo imprimir el ticket.\n\n" + ex.Message, "Impresión", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
