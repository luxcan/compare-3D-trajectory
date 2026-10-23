using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Compare3DTrajectory.Core.Tests.Loading;

/// <summary>
/// Writes a minimal but genuine .xlsx workbook: the five parts the format requires, text as inline
/// strings and numbers in their round-trip form.
/// </summary>
/// <remarks>
/// Written by hand so the tests need no Excel-writing package. NPOI 2.8 moved to a licence that must
/// be accepted before it builds, and 2.7 pulls in an ImageSharp with open security advisories. The
/// legacy .xls fixture is a file Excel saved itself, kept under Fixtures.
/// </remarks>
internal static class ExcelFixture
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    /// <summary>
    /// One sheet per entry. Each row is a list of cells: a string is written as text, a double as a
    /// number, and null leaves the cell blank. A null row leaves the whole row blank.
    /// </summary>
    public static void Write(string path, params (string Sheet, object?[]?[] Rows)[] sheets)
    {
        using FileStream stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        Add(zip, "[Content_Types].xml", new XElement(
            ContentTypes + "Types",
            new XElement(ContentTypes + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(ContentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
            sheets.Select((_, i) => new XElement(
                ContentTypes + "Override",
                new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));

        Add(zip, "_rels/.rels", new XElement(
            PackageRelationships + "Relationships",
            new XElement(
                PackageRelationships + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                new XAttribute("Target", "xl/workbook.xml"))));

        Add(zip, "xl/workbook.xml", new XElement(
            Main + "workbook",
            new XAttribute(XNamespace.Xmlns + "r", Relationships),
            new XElement(Main + "sheets", sheets.Select((sheet, i) => new XElement(
                Main + "sheet",
                new XAttribute("name", sheet.Sheet),
                new XAttribute("sheetId", i + 1),
                new XAttribute(Relationships + "id", $"rId{i + 1}"))))));

        Add(zip, "xl/_rels/workbook.xml.rels", new XElement(
            PackageRelationships + "Relationships",
            sheets.Select((_, i) => new XElement(
                PackageRelationships + "Relationship",
                new XAttribute("Id", $"rId{i + 1}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                new XAttribute("Target", $"worksheets/sheet{i + 1}.xml")))));

        for (int i = 0; i < sheets.Length; i++)
        {
            Add(zip, $"xl/worksheets/sheet{i + 1}.xml", Worksheet(sheets[i].Rows));
        }
    }

    private static XElement Worksheet(object?[]?[] rows) => new(
        Main + "worksheet",
        new XElement(Main + "sheetData", rows
            .Select((cells, r) => (Cells: cells, Number: r + 1))
            .Where(row => row.Cells is not null)
            .Select(row => new XElement(
                Main + "row",
                new XAttribute("r", row.Number),
                row.Cells!.Select((value, c) => Cell(value, $"{ColumnName(c)}{row.Number}"))))));

    private static XElement? Cell(object? value, string reference) => value switch
    {
        null => null,
        string text => new XElement(
            Main + "c",
            new XAttribute("r", reference),
            new XAttribute("t", "inlineStr"),
            new XElement(Main + "is", new XElement(Main + "t", text))),
        double number => new XElement(
            Main + "c",
            new XAttribute("r", reference),
            new XElement(Main + "v", number.ToString("R", CultureInfo.InvariantCulture))),
        _ => throw new ArgumentException($"Unsupported cell value {value}.", nameof(value)),
    };

    private static string ColumnName(int index)
    {
        string name = string.Empty;
        for (int n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + ((n - 1) % 26)) + name;
        }

        return name;
    }

    private static void Add(ZipArchive zip, string name, XElement root)
    {
        using Stream entry = zip.CreateEntry(name).Open();
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(entry);
    }
}
