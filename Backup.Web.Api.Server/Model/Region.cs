using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Region
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Pays { get; set; }

    public string? Code { get; set; }

    public string? Image { get; set; }
}
