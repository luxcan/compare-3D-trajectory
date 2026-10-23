using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>Reads every worksheet of an .xlsx or .xls workbook into rows of cells.</summary>
public static class ExcelTableReader
{
    /// <summary>
    /// Reads a workbook. It is opened for shared reading, so a workbook that is still open in Excel
    /// can be read.
    /// </summary>
    public static IReadOnlyList<RawTable> ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Read(stream);
    }

    /// <summary>One table per worksheet, named after the sheet, holding only its non-empty rows.</summary>
    public static IReadOnlyList<RawTable> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // The legacy .xls format stores text in Windows code pages, which .NET only knows once this
        // provider is registered. Registering the same provider again is a no-op.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
        var tables = new List<RawTable>();
        do
        {
            var rows = new List<TableRow>();
            int rowNumber = 0;
            while (reader.Read())
            {
                rowNumber++;
                var cells = new string[reader.FieldCount];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = FormatCell(reader.GetValue(i));
                }

                int used = Array.FindLastIndex(cells, cell => cell.Length > 0) + 1;
                if (used > 0)
                {
                    rows.Add(new TableRow(rowNumber, cells[..used]));
                }
            }

            tables.Add(new RawTable(reader.Name, rows) { IsWorksheet = true });
        }
        while (reader.NextResult());

        return tables;
    }

    /// <summary>
    /// Numbers are written back in their shortest round-trip form, so turning a cell into text and
    /// parsing it again gives back exactly the double Excel stored.
    /// </summary>
    private static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        string text => text.Trim(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()?.Trim() ?? string.Empty,
    };
}
