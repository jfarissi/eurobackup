using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    public sealed class ProjectGuideRegistry : IProjectGuideRegistry
    {
        public static ProjectGuideRegistry Default { get; } = CreateDefault();

        private readonly Dictionary<string, IProjectGuide> _byDomain;

        public ProjectGuideRegistry(IEnumerable<IProjectGuide> guides)
        {
            _byDomain = new Dictionary<string, IProjectGuide>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in guides)
                _byDomain[g.DomainId] = g;
        }

        public static ProjectGuideRegistry CreateDefault() => new(
        [
            WallProjectGuide.Instance,
            PaintProjectGuide.Instance,
            TilingProjectGuide.Instance,
            GardenProjectGuide.Cleaning,
            GardenProjectGuide.Landscaping,
            GardenProjectGuide.Maintenance,
            ElectricalProjectGuide.Instance,
            RoofingProjectGuide.Instance,
            PlumbingProjectGuide.Instance
        ]);

        public bool TryGet(string? domainId, out IProjectGuide? guide)
        {
            guide = null;
            if (string.IsNullOrWhiteSpace(domainId))
                return false;
            return _byDomain.TryGetValue(domainId.Trim(), out guide);
        }

        public IProjectGuide? Get(string? domainId) =>
            TryGet(domainId, out var g) ? g : null;
    }
}
