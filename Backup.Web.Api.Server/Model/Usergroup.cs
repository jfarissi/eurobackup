using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Usergroup
{
    public int Id { get; set; }

    public string? UserName { get; set; }

    public int GroupId { get; set; }
}
