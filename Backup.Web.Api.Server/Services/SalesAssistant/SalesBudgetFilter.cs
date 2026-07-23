using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public static class SalesBudgetFilter
    {
        public static string? Apply(
            List<StoreChatProductSuggestionDto> products,
            StoreChatSession session,
            ProductSearchFilter meta)
        {
            if (session.BudgetMax is not > 0 || products.Count == 0)
                return null;

            var budget = session.BudgetMax.Value;
            meta.MaxUnitPrice = budget;
            var over = products.Where(p => p.Price.HasValue && p.Price.Value > budget).ToList();
            var within = products.Where(p => !p.Price.HasValue || p.Price.Value <= budget).ToList();

            if (within.Count == 0)
                return $"Alerte budget : aucune référence unitaire ≤ {budget:N2} €. Les propositions affichées dépassent le budget unitaire.";

            if (over.Count > 0)
            {
                products.Clear();
                products.AddRange(within);
                return $"Alerte budget : {over.Count} référence(s) exclue(s) car prix unitaire > {budget:N2} €.";
            }

            return null;
        }
    }
}
