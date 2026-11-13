using CajaLocal.Models;

namespace CajaLocal.Services;

public sealed class AppServices
{
    public LocalStorageService Storage { get; } = new();
    public ExcelExportService Excel { get; } = new();
    public PrinterService Printer { get; } = new();
    public AppSettings Settings { get; private set; } = new();

    public void Initialize()
    {
        Storage.Initialize();
        Settings = Storage.LoadSettings();
    }

    public string? SaveSale(Sale sale)
    {
        // JSON is the local recovery copy. The Excel workbook is regenerated after every sale.
        Storage.AddSale(sale);
        try
        {
            Excel.Export(Storage.LoadSales(), Storage.ExcelPath);
            return null;
        }
        catch (Exception ex)
        {
            return $"La venta quedó guardada en el respaldo local, pero no se pudo actualizar Excel. " +
                   $"Cierra Ventas.xlsx si está abierto y usa 'Actualizar Excel'. Detalle: {ex.Message}";
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        Settings = settings;
        Storage.SaveSettings(settings);
    }

    public void RebuildExcel()
        => Excel.Export(Storage.LoadSales(), Storage.ExcelPath);
}
