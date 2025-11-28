using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Souscategory
{
    public int Id { get; set; }

    public int CategoryEtudeId { get; set; }

    public string? Titre { get; set; }

    public string? Description { get; set; }

    public virtual Categoryetude CategoryEtude { get; set; } = null!;

    public virtual ICollection<Survey> Surveys { get; set; } = new List<Survey>();
}
