using System.IO;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using CajaLocal.Models;

namespace CajaLocal.Services;

/// <summary>
/// Creates a basic .xlsx workbook using only .NET's built-in ZIP and XML APIs.
/// Excel does not need to be installed on the POS computer.
/// </summary>
public sealed class ExcelExportService
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypeNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    public void Export(IReadOnlyCollection<Sale> sales, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("No se pudo determinar la carpeta del archivo Excel.");
        Directory.CreateDirectory(directory);

        var tempPath = destinationPath + ".tmp";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        try
        {
            using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                AddXml(archive, "[Content_Types].xml", BuildContentTypes());
                AddXml(archive, "_rels/.rels", BuildRootRelationships());
                AddXml(archive, "xl/workbook.xml", BuildWorkbook());
                AddXml(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
                AddXml(archive, "xl/worksheets/sheet1.xml", BuildSalesSheet(sales));
                AddXml(archive, "xl/worksheets/sheet2.xml", BuildDetailsSheet(sales));
            }

            // Replacing the workbook can fail while the user has it open in Excel.
            File.Move(tempPath, destinationPath, true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* keep the original exception */ }
            }
            throw;
        }
    }

    private static XDocument BuildContentTypes()
        => new(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(ContentTypeNs + "Types",
                new XElement(ContentTypeNs + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypeNs + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypeNs + "Override",
                    new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ContentTypeNs + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ContentTypeNs + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet2.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));

    private static XDocument BuildRootRelationships()
        => new(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(PackageRelNs + "Relationships",
                new XElement(PackageRelNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml"))));

    private static XDocument BuildWorkbook()
        => new(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(MainNs + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", RelNs),
                new XElement(MainNs + "sheets",
                    new XElement(MainNs + "sheet",
                        new XAttribute("name", "Ventas"),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(RelNs + "id", "rId1")),
                    new XElement(MainNs + "sheet",
                        new XAttribute("name", "Detalle"),
                        new XAttribute("sheetId", "2"),
                        new XAttribute(RelNs + "id", "rId2")))));

    private static XDocument BuildWorkbookRelationships()
        => new(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(PackageRelNs + "Relationships",
                new XElement(PackageRelNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(PackageRelNs + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet2.xml"))));

    private static XDocument BuildSalesSheet(IEnumerable<Sale> sales)
    {
        var rows = new List<object?[]>
        {
            new object?[] { "Folio", "Fecha", "Cantidad de artículos", "Subtotal", "Total", "Forma de pago", "Efectivo recibido", "Cambio" }
        };

        foreach (var sale in sales.OrderBy(x => x.Date))
        {
            rows.Add(new object?[]
            {
                sale.TicketNumber,
                sale.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                sale.QuantitySold,
                sale.Subtotal,
                sale.Total,
                sale.PaymentMethod,
                sale.CashReceived,
                sale.Change
            });
        }

        return BuildWorksheet(rows);
    }

    private static XDocument BuildDetailsSheet(IEnumerable<Sale> sales)
    {
        var rows = new List<object?[]>
        {
            new object?[] { "Folio", "Fecha", "Código", "Producto", "Cantidad", "Precio unitario", "Importe" }
        };

        foreach (var sale in sales.OrderBy(x => x.Date))
        {
            foreach (var line in sale.Items)
            {
                rows.Add(new object?[]
                {
                    sale.TicketNumber,
                    sale.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    line.Sku,
                    line.ProductName,
                    line.Quantity,
                    line.UnitPrice,
                    line.Total
                });
            }
        }

        return BuildWorksheet(rows);
    }

    private static XDocument BuildWorksheet(IEnumerable<object?[]> rows)
    {
        var sheetData = new XElement(MainNs + "sheetData");
        var rowNumber = 1;

        foreach (var row in rows)
        {
            var rowElement = new XElement(MainNs + "row", new XAttribute("r", rowNumber));
            for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
            {
                rowElement.Add(BuildCell(row[columnIndex], GetColumnName(columnIndex + 1) + rowNumber));
            }
            sheetData.Add(rowElement);
            rowNumber++;
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(MainNs + "worksheet", sheetData));
    }

    private static XElement BuildCell(object? value, string reference)
    {
        if (value is null)
            return new XElement(MainNs + "c", new XAttribute("r", reference));

        if (value is decimal decimalValue)
            return NumericCell(reference, decimalValue.ToString(CultureInfo.InvariantCulture));
        if (value is int intValue)
            return NumericCell(reference, intValue.ToString(CultureInfo.InvariantCulture));
        if (value is long longValue)
            return NumericCell(reference, longValue.ToString(CultureInfo.InvariantCulture));

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return new XElement(MainNs + "c",
            new XAttribute("r", reference),
            new XAttribute("t", "inlineStr"),
            new XElement(MainNs + "is",
                new XElement(MainNs + "t",
                    new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
    }

    private static XElement NumericCell(string reference, string value)
        => new(MainNs + "c",
            new XAttribute("r", reference),
            new XElement(MainNs + "v", value));

    private static string GetColumnName(int number)
    {
        var name = "";
        while (number > 0)
        {
            var remainder = (number - 1) % 26;
            name = (char)('A' + remainder) + name;
            number = (number - 1) / 26;
        }
        return name;
    }

    private static void AddXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        document.Save(stream, SaveOptions.DisableFormatting);
    }
}
