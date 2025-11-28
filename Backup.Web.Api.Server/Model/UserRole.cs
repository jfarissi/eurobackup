using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class UserRole
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;
}
