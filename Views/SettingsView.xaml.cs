using System.Windows;
using System.Windows.Controls;
using CajaLocal.Models;

namespace CajaLocal.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        StoreNameBox.Text = App.Services.Settings.StoreName;
        StoreAddressBox.Text = App.Services.Settings.StoreAddress;
        AutoPrintCheckBox.IsChecked = App.Services.Settings.AutoPrint;
        DataPathText.Text = App.Services.Storage.DataDirectory;
        LoadPrinters();
    }

    private void LoadPrinters()
    {
        try
        {
            var configuredName = App.Services.Settings.PrinterName;
            var printers = App.Services.Printer.GetInstalledPrinters();
            PrinterComboBox.ItemsSource = printers;
            if (!string.IsNullOrWhiteSpace(configuredName) && printers.Contains(configuredName))
                PrinterComboBox.SelectedItem = configuredName;
            else if (printers.Count > 0)
                PrinterComboBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudieron consultar las impresoras instaladas en Windows.\n\n" + ex.Message,
                "Impresoras", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private AppSettings ReadForm()
        => new()
        {
            StoreName = string.IsNullOrWhiteSpace(StoreNameBox.Text) ? "MI NEGOCIO" : StoreNameBox.Text.Trim(),
            StoreAddress = StoreAddressBox.Text.Trim(),
            PrinterName = PrinterComboBox.SelectedItem?.ToString() ?? "",
            AutoPrint = AutoPrintCheckBox.IsChecked == true
        };

    private void RefreshPrinters_Click(object sender, RoutedEventArgs e) => LoadPrinters();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        App.Services.SaveSettings(ReadForm());
        MessageBox.Show("Configuración guardada.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void TestPrint_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadForm();
        if (string.IsNullOrWhiteSpace(settings.PrinterName))
        {
            MessageBox.Show("Conecta e instala la BIXOLON en Windows, y selecciónala aquí.",
                "Impresora no seleccionada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            App.Services.Printer.PrintTestReceipt(settings.PrinterName, settings);
            MessageBox.Show("Se envió el ticket de prueba a la impresora.", "Prueba de impresión",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo imprimir el ticket de prueba.\n\n" + ex.Message,
                "Error de impresión", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
