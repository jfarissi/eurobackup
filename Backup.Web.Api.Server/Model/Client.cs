using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Client
{
    public int Id { get; set; }

    public string CliRaisonSociale { get; set; } = null!;

    public string CliName { get; set; } = null!;

    public string CliTel { get; set; } = null!;

    public string CliFax { get; set; } = null!;

    public string CliEmail { get; set; } = null!;

    public int IdFamille { get; set; }
}
