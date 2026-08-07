using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Modules
{
    /// <summary>
    /// Seed modules par société : toujours <c>core</c> ;
    /// <c>erp_catalog_sync</c> si EnableErpCatalogSync ;
    /// modules additionnels via TenancySeed:DefaultModules (csv).
    /// </summary>
    public sealed class ModuleSeedService
    {
        private readonly IStorageBroker storage;
        private readonly IModuleService modules;
        private readonly IConfiguration configuration;
        private readonly ILogger<ModuleSeedService> logger;

        public ModuleSeedService(
            IStorageBroker storage,
            IModuleService modules,
            IConfiguration configuration,
            ILogger<ModuleSeedService> logger)
        {
            this.storage = storage;
            this.modules = modules;
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task EnsureDefaultsAsync()
        {
            var companies = await this.storage.SelectAllCompanies().AsNoTracking().ToListAsync();
            if (companies.Count == 0) return;

            var extra = (this.configuration["TenancySeed:DefaultModules"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToLowerInvariant())
                .Where(c => c is not ModuleCodes.Core)
                .Distinct()
                .ToList();

            foreach (var company in companies)
            {
                await this.modules.EnsureModuleAsync(company.Id, ModuleCodes.Core);

                if (company.EnableErpCatalogSync)
                    await this.modules.EnsureModuleAsync(company.Id, ModuleCodes.ErpCatalogSync);

                foreach (var code in extra)
                    await this.modules.EnsureModuleAsync(company.Id, code);
            }

            this.logger.LogInformation(
                "Modules seed: {CompanyCount} société(s), extras=[{Extras}]",
                companies.Count,
                string.Join(',', extra));
        }
    }
}
