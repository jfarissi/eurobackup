using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Surveyresponse
{
    public int Id { get; set; }

    public int DetailetudeId { get; set; }

    public int Respondentid { get; set; }

    public DateTime? Updated { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Detailetude Detailetude { get; set; } = null!;

    public virtual Respondent Respondent { get; set; } = null!;

    public virtual ICollection<Response> Responses { get; set; } = new List<Response>();
}
