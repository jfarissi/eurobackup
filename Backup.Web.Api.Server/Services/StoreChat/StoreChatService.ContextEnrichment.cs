using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SalesAssistant;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public partial class StoreChatService
    {
        private async Task EnrichProjectContextAsync(StoreChatSession session, string text, CancellationToken ct)
        {
            var previousBrand = session.PreferredBrand;
            await _context.DetectBrandAsync(session, text, ct);
            if (!string.IsNullOrWhiteSpace(session.PreferredBrand)
                && !string.IsNullOrWhiteSpace(previousBrand)
                && !string.Equals(previousBrand, session.PreferredBrand, StringComparison.OrdinalIgnoreCase))
            {
                session.SearchTypeHints.Clear();
                session.PreferredWeightKg = null;
            }

            _context.DetectDomain(session, text);
            if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId))
                _workflow.ApplyTransition(session, WorkflowActions.IdentifyProject);

            if (!string.IsNullOrWhiteSpace(session.PreferredBrand) && !SalesTextGuards.IsExplicitWallIntent(text))
            {
                // Évite qu'un ancien projet « mur » pollue une recherche marque.
                if (string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                {
                    session.ActiveProjectDomainId = null;
                    session.ActiveProjectDomainLabel = null;
                }
            }

            _context.ParseWallDimensions(session, text);
            if (session.WallAreaM2 is > 0)
                _workflow.ApplyTransition(session, WorkflowActions.CalculateSurface);
            _context.CollectMaterialHints(session, text);
            _context.UpdateStickySearchFilters(session, text);
        }
    }
}
