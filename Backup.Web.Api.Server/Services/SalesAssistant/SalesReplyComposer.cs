using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesReplyComposer
    {
        /// <summary>
        /// Reformule uniquement à partir des faits validés. Null si LLM indispo / échec.
        /// </summary>
        Task<string?> ComposeAsync(SalesReplyFacts facts, CancellationToken ct = default);
    }

    /// <summary>
    /// LLM = voix uniquement. Aucune décision métier, aucun inventaire hors JSON.
    /// </summary>
    public sealed class SalesReplyComposer : ISalesReplyComposer
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        private readonly IStoreChatAiClient _ai;
        private readonly StoreChatOptions _store;
        private readonly ILogger<SalesReplyComposer> _logger;

        public SalesReplyComposer(
            IStoreChatAiClient ai,
            IOptions<StoreChatOptions> store,
            ILogger<SalesReplyComposer> logger)
        {
            _ai = ai;
            _store = store.Value ?? new StoreChatOptions();
            _logger = logger;
        }

        public async Task<string?> ComposeAsync(SalesReplyFacts facts, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(facts, JsonOptions);
            var brand = string.IsNullOrWhiteSpace(_store.BrandName) ? "magasin" : _store.BrandName;
            var langName = SalesLocale.LanguageName(facts.Language);
            var system =
                $"Tu es la voix de l'assistant vendeur {brand}. "
                + "Tu reformules UNIQUEMENT le JSON de faits métier fourni par le système C#. "
                + "Tu n'as AUCUN outil. Tu ne décides PAS (pas de recherche, panier, devis, commande, paiement). "
                + "Interdit d'inventer un produit, un prix, une marque, un id ou une gamme absente de answerFacts.topProducts. "
                + "Si answerFacts.hasMatches est false, dis clairement l'absence — ne propose rien d'inventé. "
                + $"Réponds en {langName}, 2 à 4 phrases max, une seule question de suivi si suggestedFollowUp est fourni. "
                + "N'ajoute pas de liste à puces de produits (l'UI affiche déjà le tableau).";

            var user =
                "Faits métier validés (JSON). Reformule pour le client magasin, sans rien inventer :\n"
                + json;

            try
            {
                var reply = await _ai.CompleteSystemUserAsync(system, user, ct);
                if (string.IsNullOrWhiteSpace(reply))
                    return null;

                if (LooksUnsafe(reply, facts))
                {
                    _logger.LogInformation("ReplyComposer discarded unsafe LLM output");
                    return null;
                }

                return reply.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ReplyComposer failed");
                return null;
            }
        }

        /// <summary>
        /// Filet : si le LLM invente des noms absents des faits, on jette la phrase.
        /// </summary>
        private static bool LooksUnsafe(string reply, SalesReplyFacts facts)
        {
            if (reply.Length > 600)
                return true;

            // Patterns typiques d'invention de liste.
            if (reply.Contains("1.", StringComparison.Ordinal)
                && reply.Contains("2.", StringComparison.Ordinal)
                && facts.Answer.TopProducts.Count == 0)
                return true;

            return false;
        }
    }
}
