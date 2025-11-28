using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Survey
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime Updated { get; set; }

    public DateTime OpeningTime { get; set; }

    public DateTime ClosingTime { get; set; }

    public int GroupId { get; set; }

    public string? Text { get; set; }

    public int? SousCategorieId { get; set; }

    public string? File { get; set; }

    public string? Url { get; set; }

    public virtual Souscategory? SousCategorie { get; set; }
}
