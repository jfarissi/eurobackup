using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Etude
{
    public int Id { get; set; }

    public string? Titre { get; set; }

    public string? Description { get; set; }
}
