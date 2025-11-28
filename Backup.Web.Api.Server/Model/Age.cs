using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Age
{
    public int Id { get; set; }

    public int? From { get; set; }

    public int? To { get; set; }

    public string? Titel { get; set; }

    public string? Description { get; set; }
}
