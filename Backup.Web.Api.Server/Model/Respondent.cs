using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Respondent
{
    public int Id { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public DateTime? Created { get; set; }

    public string? Sex { get; set; }

    public string? Tel { get; set; }

    public string? Url { get; set; }

    public virtual ICollection<Respondentpoint> Respondentpoints { get; set; } = new List<Respondentpoint>();

    public virtual ICollection<Response> Responses { get; set; } = new List<Response>();

    public virtual ICollection<Surveyresponse> Surveyresponses { get; set; } = new List<Surveyresponse>();
}
