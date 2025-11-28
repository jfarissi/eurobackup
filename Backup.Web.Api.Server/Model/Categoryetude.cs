using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Categoryetude
{
    public int Id { get; set; }

    public string? Titre { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<Souscategory> Souscategories { get; set; } = new List<Souscategory>();
}
