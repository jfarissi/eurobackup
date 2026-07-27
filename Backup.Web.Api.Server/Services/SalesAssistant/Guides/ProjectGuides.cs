using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    /// <summary>Accès statique au registre (tests + code legacy sans DI).</summary>
    public static class ProjectGuides
    {
        public static IProjectGuideRegistry Registry { get; set; } = ProjectGuideRegistry.Default;

        public static bool TryGet(string? domainId, out IProjectGuide? guide) =>
            Registry.TryGet(domainId, out guide);

        public static bool TryGet(StoreChatSession session, out IProjectGuide? guide) =>
            TryGet(session.ActiveProjectDomainId, out guide);

        public static bool HasGuide(StoreChatSession session) =>
            TryGet(session, out _);

        public static bool IsComplete(StoreChatSession session) =>
            TryGet(session, out var guide) && guide!.IsComplete(session);

        public static void ApplyCompleteFlags(StoreChatResponseDto response, StoreChatSession session)
        {
            response.SuppressProjectGuide = session.SuppressProjectGuide || SalesMission.IsSimpleSku(session);
            if (response.SuppressProjectGuide)
            {
                // Mission SKU : pas de parcours UI (même si le domaine a un guide).
                response.GuideComplete = true;
                response.WallGuideComplete = true;
                return;
            }

            var complete = IsComplete(session);
            response.GuideComplete = complete;
            // Compat clients / scénarios mur : même valeur (parcours actif terminé).
            response.WallGuideComplete = complete;
        }
    }
}
