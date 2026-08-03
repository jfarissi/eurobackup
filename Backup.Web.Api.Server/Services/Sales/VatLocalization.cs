using System;

namespace Backup.Web.Api.Server.Services.Sales
{
    /// <summary>RG-FC7 : taux de TVA par défaut selon le pays du client (moteur simplifié).</summary>
    public static class VatLocalization
    {
        public const decimal DefaultRate = 21m;

        public static decimal DefaultRateForCountry(string? country)
        {
            var c = (country ?? "").Trim().ToUpperInvariant();
            return c switch
            {
                "BE" or "BE-BE" or "BELGIQUE" or "BELGIUM" => 21m,
                "FR" or "FRANCE" => 20m,
                "NL" or "PAYS-BAS" or "NETHERLANDS" or "HOLLAND" => 21m,
                "DE" or "ALLEMAGNE" or "GERMANY" => 19m,
                "LU" or "LUXEMBOURG" => 17m,
                _ => DefaultRate
            };
        }
    }
}
