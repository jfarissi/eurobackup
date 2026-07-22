using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public sealed class SalesPromoLineDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? RegularPrice { get; set; }
        public decimal? PromoPrice { get; set; }
        public decimal? Savings { get; set; }
    }

    public sealed class SalesSavingsDto
    {
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public decimal? PriceA { get; set; }
        public decimal? PriceB { get; set; }
        public decimal? SavedAmount { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public interface ISalesConfidenceEngine
    {
        string? DetectStyle(string text, StoreChatSession session);
        string BuildTips(StoreChatSession session, IReadOnlyList<StoreChatProductSuggestionDto> products);
        SalesSavingsDto? BuildSavings(IReadOnlyList<StoreChatProductSuggestionDto> products);
        string BuildAdvisorReply(StoreChatSession session);
        string? StyleAdvice(StoreChatSession session);
    }

    public class SalesConfidenceEngine : ISalesConfidenceEngine
    {
        public string? DetectStyle(string text, StoreChatSession session)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            string? style = null;
            if (ContainsAny(lower, "scandinave", "nordique", "clair et bois"))
                style = "scandinave";
            else if (ContainsAny(lower, "moderne", "minimal", "contemporain"))
                style = "moderne";
            else if (ContainsAny(lower, "industriel", "beton", "béton", "loft"))
                style = "industriel";
            else if (ContainsAny(lower, "classique", "traditionnel", "campagne"))
                style = "classique";
            else if (ContainsAny(lower, "style ", "déco", "deco", "ambiance"))
                style = "personnalisé";

            if (style != null)
                session.PreferredStyle = style;
            return style;
        }

        public string? StyleAdvice(StoreChatSession session)
        {
            if (string.IsNullOrWhiteSpace(session.PreferredStyle))
                return null;

            return session.PreferredStyle switch
            {
                "scandinave" => "Style scandinave : tons clairs, bois, joints discrets, peinture mate.",
                "moderne" => "Style moderne : lignes nettes, contrastes, grands formats carrelage.",
                "industriel" => "Style industriel : gris béton, métal, joints plus marqués.",
                "classique" => "Style classique : formats moyens, teintes chaudes, finitions soignées.",
                _ => $"Style « {session.PreferredStyle} » pris en compte pour les prochaines suggestions."
            };
        }

        public string BuildTips(StoreChatSession session, IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            var tips = new List<string>();
            var domain = session.ActiveProjectDomainId ?? "";
            var hay = string.Join(' ', products.Select(p => $"{p.Name} {p.Category}")).ToLowerInvariant();

            if (domain == "wall_construction" || ContainsAny(hay, "ciment", "mortier", "brique", "bloc"))
            {
                tips.Add("Prévoir +10 % de perte (casse / découpes) sur briques/blocs.");
                tips.Add("Humidifiez légèrement les blocs par temps chaud avant pose.");
            }

            if (domain == "painting" || ContainsAny(hay, "peinture", "verf"))
            {
                tips.Add("Sous-couche recommandée sur fond poreux ou contraste fort.");
                tips.Add("Comptez ~1 L pour 8–10 m² selon le pouvoir couvrant.");
            }

            if (domain == "tiling" || ContainsAny(hay, "carrel", "tegel"))
            {
                tips.Add("Gardez 10 % de carreaux en réserve pour les chutes.");
                tips.Add("Choisissez colle et joint adaptés (intérieur / extérieur / humidité).");
            }

            if (tips.Count == 0)
                tips.Add("Astuce : validez d'abord le produit principal, puis les compléments (colle, outils, protection).");

            return "Astuces chantier :\n• " + string.Join("\n• ", tips.Take(3));
        }

        public SalesSavingsDto? BuildSavings(IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            var priced = products
                .Where(p => p.Price.HasValue && p.Price.Value > 0)
                .OrderBy(p => p.Price)
                .Take(2)
                .ToList();
            if (priced.Count < 2)
                return null;

            var a = priced[0];
            var b = priced[1];
            var saved = (b.Price ?? 0) - (a.Price ?? 0);
            return new SalesSavingsDto
            {
                OptionA = a.Name,
                OptionB = b.Name,
                PriceA = a.Price,
                PriceB = b.Price,
                SavedAmount = saved > 0 ? Math.Round(saved, 2) : 0,
                Summary = saved > 0
                    ? $"Économie A vs B : {saved:N2} € en choisissant « {a.Name} » ({a.Price:N2} €) plutôt que « {b.Name} » ({b.Price:N2} €)."
                    : $"Les deux options sont au même prix catalogue ({a.Price:N2} €)."
            };
        }

        public string BuildAdvisorReply(StoreChatSession session)
        {
            session.AdvisorMode = true;
            var q = string.IsNullOrWhiteSpace(session.ActiveProjectDomainId)
                ? "Quel type de travaux ? (mur, peinture, salle de bain, électricité…)"
                : "Intérieur ou extérieur ? Et plutôt budget serré ou qualité premium ?";

            return "Mode conseiller — pas de catalogue forcé.\n"
                   + "On avance avec une seule question : " + q;
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    public interface ISalesPromoService
    {
        Task<List<SalesPromoLineDto>> GetPromosForCartAsync(
            IReadOnlyList<StoreChatCartItem> cart,
            CancellationToken ct = default);
    }

    public class SalesPromoService : ISalesPromoService
    {
        private readonly IStorageBroker _storage;

        public SalesPromoService(IStorageBroker storage)
        {
            _storage = storage;
        }

        public async Task<List<SalesPromoLineDto>> GetPromosForCartAsync(
            IReadOnlyList<StoreChatCartItem> cart,
            CancellationToken ct = default)
        {
            if (cart.Count == 0)
                return new List<SalesPromoLineDto>();

            var ids = cart.Select(c => c.ErpProductId).Distinct().ToList();
            var now = DateTime.UtcNow;
            var products = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id) && p.PromoActive && p.PromoPrice != null)
                .ToListAsync(ct);

            var list = new List<SalesPromoLineDto>();
            foreach (var p in products)
            {
                if (p.PromoStartDate is DateTime start && start > now)
                    continue;
                if (p.PromoEndDate is DateTime end && end < now)
                    continue;

                var regular = p.UnitPrice ?? p.PriceHT ?? 0;
                var promo = p.PromoPrice ?? regular;
                if (regular > 0 && promo >= regular)
                    continue;

                list.Add(new SalesPromoLineDto
                {
                    ProductId = p.Id.ToString(CultureInfo.InvariantCulture),
                    Name = p.Name ?? p.Name2 ?? $"#{p.Id}",
                    RegularPrice = regular > 0 ? regular : null,
                    PromoPrice = promo,
                    Savings = regular > promo ? Math.Round(regular - promo, 2) : null
                });
            }

            return list;
        }
    }
}
