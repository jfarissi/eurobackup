using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class User
{
    public int Uid { get; set; }

    public string UniqueId { get; set; } = null!;

    public string Firstname { get; set; } = null!;

    public string Lastname { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string EncryptedPassword { get; set; } = null!;

    public string Salt { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public int GroupId { get; set; }

    public int UserRoleId { get; set; }
}
