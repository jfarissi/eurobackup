using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Produit
{
    public int IdProduits { get; set; }

    public string NomProduits { get; set; } = null!;

    public int IdFamille { get; set; }

    public int IdMarque { get; set; }

    public int IdUnite { get; set; }

    public string? DescriptionProduits { get; set; }

    public string? CodeArticle { get; set; }

    public int QuantiteStockProduits { get; set; }

    public decimal PrixAchatProduits { get; set; }

    public decimal PrixVentesProduits { get; set; }
}
