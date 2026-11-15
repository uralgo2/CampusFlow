namespace CampusFlow.Application.Services;

/// <summary>Готовый CSV-файл для скачивания.</summary>
/// <param name="FileName">Имя файла.</param>
/// <param name="Content">Содержимое CSV (с BOM для корректной кодировки в Excel).</param>
public sealed record CsvExport(string FileName, string Content);
