using BlockViz.Domain.Models;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BlockViz.Applications.Services
{
    public interface IExcelImportService
    {
        IReadOnlyList<Block> Load(string path);
    }

    [Export(typeof(IExcelImportService))]
    internal class ExcelImportService : IExcelImportService
    {
        public IReadOnlyList<Block> Load(string path)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, config);

            var dateTimeOptions = new TypeConverterOptions { Formats = new[] { "yyyyMMdd" } };
            csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = dateTimeOptions.Formats;

            return csv.GetRecords<Block>().ToList();
        }
    }
}