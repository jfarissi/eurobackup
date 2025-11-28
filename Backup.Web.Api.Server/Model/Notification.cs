using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Notification
{
    public uint Id { get; set; }

    public string? Title { get; set; }

    public string? Txtnotif { get; set; }

    public short? IdLocation { get; set; }

    public DateTime? DateDebut { get; set; }

    public DateTime? DateFin { get; set; }
}
