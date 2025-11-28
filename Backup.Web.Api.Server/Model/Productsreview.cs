using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Productsreview
{
    public int Id { get; set; }

    public string? ReviewerComments { get; set; }

    public string? ReviewerName { get; set; }

    public int ReviewerRating { get; set; }

    public int ProductId { get; set; }

    public int? ProductId1 { get; set; }

    public virtual Product? ProductId1Navigation { get; set; }
}
