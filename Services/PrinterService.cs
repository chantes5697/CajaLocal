using System.ComponentModel;
using System.Printing;
using System.Runtime.InteropServices;
using System.Text;
using CajaLocal.Models;

namespace CajaLocal.Services;

/// <summary>
/// Sends ESC/POS data directly to an installed Windows printer queue (RAW spool data).
/// The BIXOLON Windows driver must already be installed and the printer must be in ESC/POS mode.
/// </summary>
public sealed class PrinterService
{
    private const int CodePageWindows1252 = 16;

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        using var server = new LocalPrintServer();
        var names = new List<string>();
        foreach (var queue in server.GetPrintQueues())
        {
            try { names.Add(queue.Name); }
            finally { queue.Dispose(); }
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
    }

    public void PrintSale(string printerName, Sale sale, AppSettings settings)
    {
        SendRaw(printerName, BuildSaleReceipt(sale, settings));
    }

    public void PrintTestReceipt(string printerName, AppSettings settings)
    {
        var sb = StartReceipt(settings.StoreName);
        Center(sb, "PRUEBA DE IMPRESION");
        Left(sb);
        Line(sb, "BIXOLON - Caja Local");
        Line(sb, "Si puedes leer este texto,");
        Line(sb, "la conexion de impresion funciona.");
        Line(sb, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        Line(sb, "------------------------------------------");
        Center(sb, "*** FIN DE PRUEBA ***");
        FeedAndCut(sb);
        SendRaw(printerName, ToPrinterBytes(sb.ToString()));
    }

    private static byte[] BuildSaleReceipt(Sale sale, AppSettings settings)
    {
        var sb = StartReceipt(settings.StoreName);
        Center(sb, "TICKET DE VENTA");
        Left(sb);
        if (!string.IsNullOrWhiteSpace(settings.StoreAddress))
            Line(sb, settings.StoreAddress, wrap: true);

        Line(sb, "Fecha: " + sale.Date.ToString("dd/MM/yyyy HH:mm:ss"));
        Line(sb, "Folio: " + sale.TicketNumber);
        Line(sb, "------------------------------------------");

        foreach (var item in sale.Items)
        {
            Line(sb, $"{item.Quantity:0.##} x {item.ProductName}", wrap: true);
            Line(sb, $"  ${item.UnitPrice:N2} c/u       IMP: ${item.Total:N2}");
        }

        Line(sb, "------------------------------------------");
        Line(sb, $"SUBTOTAL:              ${sale.Subtotal:N2}");
        Bold(sb, true);
        Line(sb, $"TOTAL:                 ${sale.Total:N2}");
        Bold(sb, false);
        Line(sb, "Pago: " + sale.PaymentMethod);
        Line(sb, $"Recibido:              ${sale.CashReceived:N2}");
        Line(sb, $"Cambio:                ${sale.Change:N2}");
        Line(sb, "------------------------------------------");
        Center(sb, "Gracias por su compra");
        Center(sb, "Conserve su ticket");
        FeedAndCut(sb);
        return ToPrinterBytes(sb.ToString());
    }

    private static StringBuilder StartReceipt(string storeName)
    {
        var sb = new StringBuilder();
        sb.Append('\x1B').Append('@');                         // Initialize printer
        sb.Append('\x1B').Append('t').Append((char)CodePageWindows1252); // Windows-1252 / Latin I
        sb.Append('\x1B').Append('a').Append((char)1);         // Center alignment
        sb.Append('\x1B').Append('E').Append((char)1);         // Emphasized text
        Line(sb, Clean(storeName).ToUpperInvariant(), wrap: true);
        Bold(sb, false);
        return sb;
    }

    private static void Center(StringBuilder sb, string text)
    {
        sb.Append('\x1B').Append('a').Append((char)1);
        Line(sb, text, wrap: true);
    }

    private static void Left(StringBuilder sb)
        => sb.Append('\x1B').Append('a').Append((char)0);

    private static void Bold(StringBuilder sb, bool enabled)
        => sb.Append('\x1B').Append('E').Append((char)(enabled ? 1 : 0));

    private static void Line(StringBuilder sb, string text, bool wrap = false)
    {
        var clean = Clean(text);
        if (wrap)
        {
            foreach (var part in Wrap(clean, 42)) sb.Append(part).Append('\n');
        }
        else
        {
            sb.Append(clean).Append('\n');
        }
    }

    private static string Wrap(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var output = new List<string>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                output.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) output.Add(current.ToString());
        return output.Count == 0 ? "" : string.Join("\n", output);
    }

    private static string Clean(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static void FeedAndCut(StringBuilder sb)
    {
        sb.Append("\n\n\n");
        sb.Append('\x1D').Append('V').Append((char)0); // Full cut (GS V 0)
    }

    private static byte[] ToPrinterBytes(string text)
    {
        // Latin-1 byte values match the BIXOLON printer's ESC t 16 (Windows-1252) table
        // for the Spanish characters commonly used on receipts.
        return Encoding.Latin1.GetBytes(text);
    }

    private static void SendRaw(string printerName, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException("Selecciona la impresora BIXOLON en Configuración.");

        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo abrir la impresora.");

        var documentStarted = false;
        var pageStarted = false;
        try
        {
            var documentInfo = new DocInfo1
            {
                DocumentName = "Caja Local - Ticket",
                OutputFile = null,
                DataType = "RAW"
            };

            if (StartDocPrinter(handle, 1, ref documentInfo) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo iniciar el trabajo de impresión.");
            documentStarted = true;

            if (!StartPagePrinter(handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo iniciar la página de impresión.");
            pageStarted = true;

            if (!WritePrinter(handle, bytes, bytes.Length, out var written) || written != bytes.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se enviaron todos los datos a la impresora.");

            if (!EndPagePrinter(handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo cerrar la página de impresión.");
            pageStarted = false;

            if (!EndDocPrinter(handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo cerrar el trabajo de impresión.");
            documentStarted = false;
        }
        finally
        {
            if (pageStarted) EndPagePrinter(handle);
            if (documentStarted) EndDocPrinter(handle);
            ClosePrinter(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo1
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? DocumentName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DataType;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printerHandle, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr printerHandle, int level, ref DocInfo1 documentInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WritePrinter(IntPtr printerHandle, byte[] buffer, int count, out int written);

    [DllImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndDocPrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClosePrinter(IntPtr printerHandle);
}
