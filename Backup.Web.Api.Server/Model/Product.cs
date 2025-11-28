using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Product
{
    public int Id { get; set; }

    public string? CodeArticle { get; set; }

    public string? DescriptionProduits { get; set; }

    public int IdFamille { get; set; }

    public int IdMarque { get; set; }

    public int IdUnite { get; set; }

    public string? NomProduits { get; set; }

    public int PrixAchatProduits { get; set; }

    public int PrixVentesProduits { get; set; }

    public int QuantiteStockProduits { get; set; }

    public virtual ICollection<Productsreview> Productsreviews { get; set; } = new List<Productsreview>();
}
