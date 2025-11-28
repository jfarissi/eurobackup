using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Ville
{
    public int Id { get; set; }

    public decimal? Lang { get; set; }

    public decimal? Lat { get; set; }

    public string? NameAr { get; set; }

    public string? NameFr { get; set; }

    public string? Color { get; set; }
}
