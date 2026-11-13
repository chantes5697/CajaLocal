using System.IO;
using System.Text.Json;
using CajaLocal.Models;

namespace CajaLocal.Services;

public sealed class LocalStorageService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CajaLocal");

    private string ProductsPath => Path.Combine(DataDirectory, "productos.json");
    private string SalesPath => Path.Combine(DataDirectory, "ventas.json");
    private string SettingsPath => Path.Combine(DataDirectory, "configuracion.json");

    public string ExcelPath => Path.Combine(DataDirectory, "Ventas.xlsx");

    public void Initialize()
    {
        Directory.CreateDirectory(DataDirectory);
        if (!File.Exists(ProductsPath)) WriteJson(ProductsPath, new List<Product>());
        if (!File.Exists(SalesPath)) WriteJson(SalesPath, new List<Sale>());
        if (!File.Exists(SettingsPath)) WriteJson(SettingsPath, new AppSettings());
    }

    public List<Product> LoadProducts() => ReadJson<List<Product>>(ProductsPath) ?? [];
    public List<Sale> LoadSales() => ReadJson<List<Sale>>(SalesPath) ?? [];
    public AppSettings LoadSettings() => ReadJson<AppSettings>(SettingsPath) ?? new AppSettings();

    public void SaveProducts(IEnumerable<Product> products)
        => WriteJson(ProductsPath, products.ToList());

    public void SaveSettings(AppSettings settings)
        => WriteJson(SettingsPath, settings);

    public void AddSale(Sale sale)
    {
        var sales = LoadSales();
        sales.Add(sale);
        WriteJson(SalesPath, sales);
    }

    private T? ReadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    private void WriteJson<T>(string path, T value)
    {
        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, true);
    }
}
