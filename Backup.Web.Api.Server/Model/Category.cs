using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Category
{
    public int Id { get; set; }

    public string? Titel { get; set; }

    public string? Description { get; set; }
}
