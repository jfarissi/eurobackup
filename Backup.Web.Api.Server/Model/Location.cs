using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Location
{
    public ushort Id { get; set; }

    public ushort IdClient { get; set; }

    public string LocName { get; set; } = null!;

    public decimal LocLongitude { get; set; }

    public decimal LocLatitude { get; set; }

    public short Radius { get; set; }

    public string? Ville { get; set; }
}
