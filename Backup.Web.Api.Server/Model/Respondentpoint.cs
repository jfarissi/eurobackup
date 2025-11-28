using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Respondentpoint
{
    public int Id { get; set; }

    public int? Points { get; set; }

    public string? Recharge { get; set; }

    public int Respondentid { get; set; }

    public int Detailetudeid { get; set; }

    public virtual Detailetude Detailetude { get; set; } = null!;

    public virtual Respondent Respondent { get; set; } = null!;
}
