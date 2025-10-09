using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualDataGrid.Core
{
    /// <summary>
    /// Minimal interface setiap entity yang akan dipakai di grid.
    /// Id harus unik per row; RowVersion berguna untuk tracking perubahan.
    /// </summary>
    public interface IEntity
    {
        int Id { get; set; }
        long RowVersion { get; set; }
    }
}
