using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Message
{
    public int Id { get; set; }

    public string? AddedBy { get; set; }

    public string? Message1 { get; set; }

    public string GroupId { get; set; } = null!;

    public string? DateMessage { get; set; }
}
