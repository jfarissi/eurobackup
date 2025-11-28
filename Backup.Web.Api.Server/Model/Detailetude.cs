using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Detailetude
{
    public int Id { get; set; }

    public int? Peopel { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int? Statut { get; set; }

    public int? Typepaiement { get; set; }

    public int? IdClient { get; set; }

    public int? IdEtude { get; set; }

    public int? IdSurvey { get; set; }

    public string? MenageIndividu { get; set; }

    public string? Sex { get; set; }

    public string? Region { get; set; }

    public string? Category { get; set; }

    public string? Marques { get; set; }

    public string? CritereImage { get; set; }

    public string? CritereMarche { get; set; }

    public string? TxtSurvey { get; set; }

    public virtual ICollection<Respondentpoint> Respondentpoints { get; set; } = new List<Respondentpoint>();

    public virtual ICollection<Surveyresponse> Surveyresponses { get; set; } = new List<Surveyresponse>();
}
