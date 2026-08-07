using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class CarApiImportResult
    {
        public int PartsTotal { get; set; }
        public int PartsCreated { get; set; }
        public int PartsUpdated { get; set; }
        public int PartsSkipped { get; set; }
        public int VariantsCreated { get; set; }
        public int VehicleBrandsTotal { get; set; }
        public int VehicleBrandsCreated { get; set; }
        public int VehicleBrandsSkipped { get; set; }
        public int CategoriesCreated { get; set; }
        public int FrenchNamesUpdated { get; set; }
        public bool VehicleAttributeEnsured { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
