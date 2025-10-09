using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VirtualDataGrid.Core
{
    /// <summary>
    /// Representasi satu baris data dalam sistem grid internal.
    /// 
    /// FITUR UTAMA:
    /// - Menyimpan metadata baris: Index, ID, Versi, dan data asli
    /// - Menggunakan BufferHandle untuk management memory yang aman
    /// - Data cell disimpan di ArrayPool untuk efisiensi memory
    /// - Expose data cell sebagai read-only untuk keamanan
    /// 
    /// KONSEP PENTING:
    /// - STRUCT INI IMMUTABLE (tidak bisa diubah setelah dibuat)
    /// - Memory management menggunakan reference counting
    /// - Setiap pemakaian harus panggil RetainHandle(), setelah selesai ReleaseHandle()
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct InternalRow : IEquatable<InternalRow>
    {
        /// <summary>
        /// Index baris dalam data source (bisa berubah saat filter/sort)
        /// </summary>
        public long Index { get; }

        /// <summary>
        /// ID unik baris (tidak berubah selama baris exist)
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// Versi baris untuk optimistic concurrency control
        /// Berubah setiap kali baris di-update
        /// </summary>
        public long RowVersion { get; }

        /// <summary>
        /// Data object asli yang di-wrap oleh baris ini
        /// Contoh: Person, Order, Product, dll
        /// </summary>
        public object OriginalItem { get; }

        /// <summary>
        /// Handle ke buffer memory yang menyimpan nilai cell
        /// Menggunakan reference counting untuk lifecycle management
        /// </summary>
        private readonly BufferHandle<CellValue>? _handle;

        /// <summary>
        /// Data cell dalam bentuk read-only
        /// Aman untuk diakses dari multiple thread
        /// 
        /// CONTOH PENGGUNAAN:
        /// var nilai = row.Cells.Span[0]; // Ambil cell pertama
        /// foreach (var cell in row.Cells.Span) // Loop semua cell
        /// </summary>
        public ReadOnlyMemory<CellValue> Cells => _handle?.Memory ?? ReadOnlyMemory<CellValue>.Empty;

        /// <summary>
        /// Buat baris internal baru
        /// </summary>
        /// <param name="index">Posisi baris dalam tampilan</param>
        /// <param name="id">ID unik baris</param>
        /// <param name="rowVersion">Versi baris untuk tracking perubahan</param>
        /// <param name="originalItem">Object data asli</param>
        /// <param name="handle">Handle ke buffer memory cell values</param>
        /// <exception cref="ArgumentNullException">Jika originalItem atau handle null</exception>
        public InternalRow(int index, long id, long rowVersion, object originalItem, BufferHandle<CellValue> handle)
        {
            if (originalItem == null)
                throw new ArgumentNullException(nameof(originalItem), "Data asli tidak boleh null");
            if (handle == null)
                throw new ArgumentNullException(nameof(handle), "Buffer handle tidak boleh null");

            Index = index;
            Id = id;
            RowVersion = rowVersion;
            OriginalItem = originalItem;
            _handle = handle;

            // Auto retain saat baris dibuat
            _handle.Retain();
        }

        /// <summary>
        /// Ambil nilai cell berdasarkan index kolom
        /// 
        /// PERFORMANCE:
        /// - Method di-inline oleh compiler untuk akses cepat
        /// - Tidak ada bounds checking (gunakan dengan hati-hati)
        /// 
        /// CONTOH:
        /// var cellValue = row.GetValue(2); // Ambil cell kolom ke-3
        /// </summary>
        /// <param name="colIndex">Index kolom (0-based)</param>
        /// <returns>Nilai cell atau default value jika index invalid</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CellValue GetValue(int colIndex)
        {
            // Note: Uncomment line below untuk bounds checking yang safe
            // if ((uint)colIndex >= (uint)Cells.Length) return default;

            return Cells.Span[colIndex];
        }

        /// <summary>
        /// Ambil data object asli dengan type yang diinginkan
        /// 
        /// CONTOH:
        /// var person = row.GetOriginalItem<Person>();
        /// if (person != null) { ... }
        /// </summary>
        /// <typeparam name="T">Tipe data yang diharapkan</typeparam>
        /// <returns>Object casted ke T, atau null jika type tidak match</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetOriginalItem<T>() where T : class => OriginalItem as T;

        /// <summary>
        /// TAMBAH reference count buffer (WAJIB dipanggil saat baris dipinjam)
        /// 
        /// ATURAN PEMAKAIAN:
        /// - Panggil ini SEBELUM menyimpan reference ke baris
        /// - Pasangkan dengan ReleaseHandle() SETELAH selesai
        /// - Gunakan pattern: Retain → Process → Release
        /// 
        /// CONTOH:
        /// row.RetainHandle();
        /// try {
        ///     // Process baris...
        /// } finally {
        ///     row.ReleaseHandle();
        /// }
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RetainHandle() => _handle?.Retain();

        /// <summary>
        /// KURANGI reference count buffer (WAJIB dipanggil setelah selesai)
        /// 
        /// PERINGATAN:
        /// - Jangan lupa panggil method ini setelah RetainHandle()
        /// - Lupa panggil = MEMORY LEAK (buffer tidak pernah di-return)
        /// - Double release = EXCEPTION
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseHandle() => _handle?.Release();

        #region Equality Implementation
        /// <summary>
        /// Bandingkan dua baris berdasarkan ID dan Versi
        /// Dua baris dianggap sama jika ID dan RowVersion sama
        /// </summary>
        public bool Equals(InternalRow other) => Id == other.Id && RowVersion == other.RowVersion;

        /// <summary>
        /// Bandingkan dengan object lain
        /// </summary>
        public override bool Equals(object? obj) => obj is InternalRow other && Equals(other);

        /// <summary>
        /// Generate hash code berdasarkan ID dan Versi
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(Id, RowVersion);

        public static bool operator ==(InternalRow left, InternalRow right) => left.Equals(right);
        public static bool operator !=(InternalRow left, InternalRow right) => !left.Equals(right);
        #endregion
    }
}