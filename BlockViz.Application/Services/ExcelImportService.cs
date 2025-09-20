using BlockViz.Domain.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System;
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
    public sealed class ExcelImportService : IExcelImportService
    {
        public IReadOnlyList<Block> Load(string path)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                MissingFieldFound = null,
                HeaderValidated = null,
                PrepareHeaderForMatch = args => (args.Header ?? string.Empty).Trim()
            };

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, config);

            csv.Context.RegisterClassMap<BlockMap>();
            var records = csv.GetRecords<Block>().ToList();
            return records;
        }

        /// <summary>
        /// 구규격(예: Length/Breadth/Depth)과 신규 규격(예: w/h/depth) 동시 지원.
        /// 🔴 핵심: 엑셀 값 매핑을 정확히 고정
        ///    - w      → Length(가로, X)
        ///    - h      → Breadth(세로, Y)
        ///    - depth  → Height(높이, Z)
        /// </summary>
        private sealed class BlockMap : ClassMap<Block>
        {
            public BlockMap()
            {
                // 이름/ID
                Map(m => m.Name).Convert(a =>
                {
                    var row = a.Row;
                    return FirstNonEmpty(row, "BlockName", "name");
                });
                Map(m => m.BlockID).Convert(a => GetInt(a.Row, "BlockIDNumber", "id"));

                // 날짜
                Map(m => m.Start).Convert(a => ParseYyyyMMdd(a.Row, "StartDate", "startdate"));
                Map(m => m.End).Convert(a => ParseYyyyMMdd(a.Row, "EndDate", "enddate"));
                Map(m => m.Due).Convert(a => TryParseYyyyMMddNullable(a.Row, "DueDate"));

                Map(m => m.ProcessingTime).Convert(a => GetInt(a.Row, "ProcessingTime"));

                // 작업장
                Map(m => m.DeployWorkplace).Convert(a => GetInt(a.Row, "DeployWorkplace", "workspace"));

                // ── 크기(핵심 수정) ─────────────────────────────────────
                // 우선순위: 신규 헤더(w/h/depth) → 구규격(Length/Breadth/Depth)
                Map(m => m.Length).Convert(a => // X(가로)
                {
                    var r = a.Row;
                    return HasHeaders(r, "w") ? GetDouble(r, "w")
                                              : GetDouble(r, "Length");
                });
                Map(m => m.Breadth).Convert(a => // Y(세로)
                {
                    var r = a.Row;
                    return HasHeaders(r, "h") ? GetDouble(r, "h")
                                              : GetDouble(r, "Breadth");
                });
                Map(m => m.Height).Convert(a => // Z(높이)
                {
                    var r = a.Row;
                    return HasHeaders(r, "depth", "Depth") ? GetDouble(r, "depth", "Depth")
                                                           : GetDouble(r, "Height"); // 혹시 Height 헤더를 쓰는 구규격 대비
                });

                Map(m => m.NumberOfBlocks).Convert(a => GetInt(a.Row, "NumberOfBlocks"));

                // 좌표(센터)
                Map(m => m.X).Convert(a => GetDouble(a.Row, "center_x", "BlockxCoord", "x"));
                Map(m => m.Y).Convert(a => GetDouble(a.Row, "center_y", "BlockyCoord", "y"));

                // 방향(있으면)
                Map(m => m.Direction).Convert(a => GetInt(a.Row, "o", "BlockDirection"));
            }

            // ── 헤더/파싱 헬퍼 ──────────────────────────────────────
            private static bool HasHeaders(CsvHelper.IReaderRow row, params string[] names)
            {
                var headers = row?.Context?.Reader?.HeaderRecord;
                if (headers == null || headers.Length == 0) return false;

                foreach (var n in names)
                {
                    var target = (n ?? string.Empty).Trim();
                    if (headers.Any(h => string.Equals((h ?? string.Empty).Trim(), target, StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
                return false;
            }

            private static bool TryGetFieldIgnoreCase(CsvHelper.IReaderRow row, string header, out string value)
            {
                value = string.Empty;

                if (row.TryGetField(header, out string s1))
                {
                    value = s1;
                    return true;
                }

                var headers = row?.Context?.Reader?.HeaderRecord;
                if (headers == null || headers.Length == 0) return false;

                var target = (header ?? string.Empty).Trim();
                for (int i = 0; i < headers.Length; i++)
                {
                    var h = (headers[i] ?? string.Empty).Trim();
                    if (string.Equals(h, target, StringComparison.OrdinalIgnoreCase))
                    {
                        if (row.TryGetField(i, out string s2))
                        {
                            value = s2;
                            return true;
                        }
                    }
                }
                return false;
            }

            private static bool TryGetAnyField(CsvHelper.IReaderRow row, out string value, params string[] names)
            {
                foreach (var n in names)
                {
                    if (TryGetFieldIgnoreCase(row, n, out var s) && !string.IsNullOrWhiteSpace(s))
                    {
                        value = s.Trim();
                        return true;
                    }
                }
                value = string.Empty;
                return false;
            }

            private static string FirstNonEmpty(CsvHelper.IReaderRow row, params string[] names)
            {
                return TryGetAnyField(row, out var s, names) ? s : string.Empty;
            }

            private static int GetInt(CsvHelper.IReaderRow row, params string[] names)
            {
                if (TryGetAnyField(row, out var s, names) &&
                    int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v;

                if (int.TryParse(FirstNonEmpty(row, names), out var v2)) return v2;
                return 0;
            }

            private static double GetDouble(CsvHelper.IReaderRow row, params string[] names)
            {
                if (TryGetAnyField(row, out var s, names))
                {
                    if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var dv))
                        return dv;
                    if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out dv))
                        return dv;
                }
                return 0.0;
            }

            private static DateTime ParseYyyyMMdd(CsvHelper.IReaderRow row, params string[] names)
            {
                var d = TryParseYyyyMMddNullable(row, names);
                if (d.HasValue) return d.Value;
                return DateTime.Today;
            }

            private static DateTime? TryParseYyyyMMddNullable(CsvHelper.IReaderRow row, params string[] names)
            {
                if (!TryGetAnyField(row, out var s, names)) return null;
                if (string.IsNullOrWhiteSpace(s)) return null;

                s = s.Trim();

                if (long.TryParse(s, out var asNum))
                    s = asNum.ToString(CultureInfo.InvariantCulture);

                if (DateTime.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt;

                return null;
            }
        }
    }
}
