using System.Collections.Generic;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    /// <summary>Adaptateur du parcours mur existant vers IProjectGuide.</summary>
    public sealed class WallProjectGuide : IProjectGuide
    {
        public static readonly WallProjectGuide Instance = new();

        private static readonly ProjectGuideStep Structure = new()
        {
            Id = "structure",
            Label = "Structure (briques / blocs)",
            AisleHint = "Stenen etc. / Snelbouwstenen…",
            TypeHints = ["brique", "blok", "snelbouw"]
        };
        private static readonly ProjectGuideStep Binder = new()
        {
            Id = "binder",
            Label = "Ciment / mortier",
            AisleHint = "Cement en Mortels",
            TypeHints = ["ciment", "cement", "mortier"]
        };
        private static readonly ProjectGuideStep Reinforcement = new()
        {
            Id = "reinforcement",
            Label = "Treillis / ferraillage",
            AisleHint = "Zind & Grid · Net, IJzer en Toebehoren",
            TypeHints = ["treillis", "murfor"]
        };
        private static readonly ProjectGuideStep Tools = new()
        {
            Id = "tools",
            Label = "Outillage pose",
            AisleHint = "Truelle, auge, niveau, gants…",
            TypeHints = ["truelle", "troffel", "niveau"]
        };

        public string DomainId => "wall_construction";
        public string Title => "Parcours chantier mur";
        public IReadOnlyList<ProjectGuideStep> Families { get; } = [Structure, Binder, Reinforcement, Tools];
        public int BaseFamilyCount => 2;

        public ProjectGuideStep ResolveNext(StoreChatSession session, string? userText, ProductSearchFilter? meta = null)
        {
            var family = SalesProjectGuide.ResolveWallFamily(session, userText, meta);
            return ToStep(family);
        }

        public bool IsComplete(StoreChatSession session) =>
            SalesProjectGuide.IsWallGuideComplete(session);

        public bool IsBaseComplete(StoreChatSession session)
        {
            var cart = SalesProjectGuide.CartOnlyHay(session);
            return SalesProjectGuide.HasStructure(cart) && SalesProjectGuide.HasBinder(cart);
        }

        public bool CartHasStep(StoreChatSession session, ProjectGuideStep step)
        {
            var cart = SalesProjectGuide.CartOnlyHay(session);
            return step.Id switch
            {
                "structure" => SalesProjectGuide.HasStructure(cart),
                "binder" => SalesProjectGuide.HasBinder(cart),
                "reinforcement" => SalesProjectGuide.HasReinforcement(cart),
                "tools" => SalesProjectGuide.HasTools(cart),
                _ => false
            };
        }

        public string BuildChecklist(StoreChatSession session, ProjectGuideStep focus)
        {
            var family = focus.Id switch
            {
                "binder" => WallGuideFamily.Binder,
                "reinforcement" => WallGuideFamily.Reinforcement,
                "tools" => WallGuideFamily.Tools,
                _ => WallGuideFamily.Structure
            };
            return SalesProjectGuide.BuildWallChecklist(session, family);
        }

        public string FocusLabel(ProjectGuideStep step) => step.Label;

        public static ProjectGuideStep ToStep(WallGuideFamily family) => family switch
        {
            WallGuideFamily.Binder => Binder,
            WallGuideFamily.Reinforcement => Reinforcement,
            WallGuideFamily.Tools => Tools,
            _ => Structure
        };

        public static WallGuideFamily ToWallFamily(ProjectGuideStep step) => step.Id switch
        {
            "binder" => WallGuideFamily.Binder,
            "reinforcement" => WallGuideFamily.Reinforcement,
            "tools" => WallGuideFamily.Tools,
            _ => WallGuideFamily.Structure
        };
    }
}
