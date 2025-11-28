using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Jobseeker
{
    public int Id { get; set; }

    public string? IdentityId { get; set; }

    public string? Location { get; set; }

    public virtual Aspnetuser? Identity { get; set; }
}
