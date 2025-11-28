using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Efmigrationshistory
{
    public string MigrationId { get; set; } = null!;

    public string ProductVersion { get; set; } = null!;
}
