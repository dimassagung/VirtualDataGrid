using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualDataGrid.Core
{
    /// <summary>
    /// High-performance cell wrapper.
    /// - Numeric/bool/date disimpan langsung (no boxing).
    /// - String via StringPool (hemat duplikasi).
    /// - Ada fallback object (_ref) kalau kolom custom (misal Image).
    /// </summary>
    public readonly struct CellValue
    {
        private readonly object? _ref; // fallback untuk custom types

        public readonly double NumericValue;
        public readonly bool IsNumeric;

        public readonly int StringId;
        public readonly bool IsString;

        public readonly bool BoolValue;
        public readonly bool IsBool;

        public readonly DateTime DateValue;
        public readonly bool IsDate;

        public static readonly CellValue Empty = new();


        // Constructor untuk setiap jenis data
        public CellValue(double numeric) { this = default; NumericValue = numeric; IsNumeric = true; }
        public CellValue(bool b) { this = default; BoolValue = b; IsBool = true; }
        public CellValue(DateTime dt) { this = default; DateValue = dt; IsDate = true; }
        public CellValue(int stringId) { this = default; StringId = stringId; IsString = true; }
        public CellValue(object custom) { this = default; _ref = custom; }

        // ---- Factory helpers ----
        // Methods untuk ambil nilai (dengan type safety)
        public static CellValue FromDouble(double d) => new(d);
        public static CellValue FromBool(bool b) => new(b);
        public static CellValue FromString(string s, StringPool pool) => new(pool.GetId(s));
        public static CellValue FromString(string s) => new(s);
        public static CellValue FromDateTime(DateTime dt) => new(dt);
        public static CellValue FromObject(object o) => new(o);

        // ---- Accessors ----
        public override string ToString()
        {
            if (IsNumeric) return NumericValue.ToString();
            if (IsBool) return BoolValue ? "True" : "False";
            if (IsString) return $"#StrId:{StringId}";
            if (IsDate) return DateValue.ToShortDateString();
            return _ref?.ToString() ?? string.Empty;
        }

        public object? AsObject(StringPool? pool = null)
        {
            if (IsNumeric) return NumericValue;
            if (IsBool) return BoolValue;
            if (IsString) return pool?.GetString(StringId) ?? $"[str:{StringId}]";
            if (IsDate) return DateValue;
            return _ref;
        }


        // Implicit conversions
        public static implicit operator string(CellValue cv) => cv._ref?.ToString() ?? string.Empty;
        public static implicit operator double(CellValue cv)
            => cv._ref is IConvertible c ? c.ToDouble(null) : 0;
        public static implicit operator int(CellValue cv)
            => cv._ref is IConvertible c ? c.ToInt32(null) : 0;
    }
}
