using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ExcelImportResult
    {
        public int FilesScanned { get; set; }
        public int RowsRead { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
