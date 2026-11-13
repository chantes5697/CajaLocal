namespace CajaLocal.Models;

public sealed class AppSettings
{
    public string StoreName { get; set; } = "MI NEGOCIO";
    public string StoreAddress { get; set; } = "";
    public string PrinterName { get; set; } = "";
    public bool AutoPrint { get; set; } = true;
}
