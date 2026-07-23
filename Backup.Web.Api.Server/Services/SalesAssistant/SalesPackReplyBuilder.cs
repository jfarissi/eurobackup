using System;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public static class SalesPackReplyBuilder
    {
        public static string Build(SalesPackDto pack, StoreChatSession session)
        {
            var lines = string.Join("\n", pack.Lines.Select(l =>
                l.ProductName != null
                    ? $"• {l.Label} : {l.ProductName} × {l.SuggestedQuantity:0.##}" + (l.UnitPrice is > 0 ? $" ({l.UnitPrice:N2} €)" : "")
                    : $"• {l.Label} : à choisir (qté ~{l.SuggestedQuantity:0.##})"));
            var total = pack.EstimatedTotal is > 0 ? $"\nTotal estimé : {pack.EstimatedTotal:N2} €." : "";
            var budget = string.IsNullOrWhiteSpace(pack.BudgetNote) ? "" : "\n" + pack.BudgetNote;
            var skill = string.Equals(session.SkillLevel, "Pro", StringComparison.OrdinalIgnoreCase)
                ? "\nValidez le pack puis devis PDF."
                : "\nVous pouvez ajuster les quantités puis ajouter au panier.";

            return $"{pack.Title} — {pack.Lines.Count} lignes :\n{lines}{total}{budget}{skill}";
        }
    }
}
