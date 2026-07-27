using System.Collections.Generic;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    /// <summary>Une étape du parcours guidé (famille catalogue).</summary>
    public sealed class ProjectGuideStep
    {
        public required string Id { get; init; }
        public required string Label { get; init; }
        public string AisleHint { get; init; } = "";
        /// <summary>Marqueurs présents dans le panier = étape faite.</summary>
        public string[] CartMarkers { get; init; } = [];
        /// <summary>Mots dans le message utilisateur qui ciblent cette étape.</summary>
        public string[] LookMarkers { get; init; } = [];
        /// <summary>Hints recherche catalogue pour cette étape.</summary>
        public string[] TypeHints { get; init; } = [];
    }

    public interface IProjectGuide
    {
        string DomainId { get; }
        string Title { get; }
        IReadOnlyList<ProjectGuideStep> Families { get; }
        /// <summary>Nombre de premières familles = « base chantier ».</summary>
        int BaseFamilyCount { get; }

        ProjectGuideStep ResolveNext(StoreChatSession session, string? userText, ProductSearchFilter? meta = null);
        bool IsComplete(StoreChatSession session);
        bool IsBaseComplete(StoreChatSession session);
        bool CartHasStep(StoreChatSession session, ProjectGuideStep step);
        string BuildChecklist(StoreChatSession session, ProjectGuideStep focus);
        string FocusLabel(ProjectGuideStep step);
    }

    public interface IProjectGuideRegistry
    {
        bool TryGet(string? domainId, out IProjectGuide? guide);
        IProjectGuide? Get(string? domainId);
    }
}
