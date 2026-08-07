using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Modules
{
    public interface IModuleService
    {
        Task<bool> HasModuleAsync(string companyId, string moduleCode, CancellationToken ct = default);
        Task<IReadOnlyList<CompanyModule>> GetActiveModulesAsync(string companyId, CancellationToken ct = default);
        Task<CompanyModule?> GetModuleAsync(string companyId, string moduleCode, CancellationToken ct = default);
        Task<CompanyModule> EnsureModuleAsync(
            string companyId,
            string moduleCode,
            string? configJson = null,
            bool activate = true,
            CancellationToken ct = default);
    }

    public sealed class ModuleService : IModuleService
    {
        private readonly IStorageBroker storage;
        private readonly ILogger<ModuleService> logger;

        public ModuleService(IStorageBroker storage, ILogger<ModuleService> logger)
        {
            this.storage = storage;
            this.logger = logger;
        }

        public async Task<bool> HasModuleAsync(string companyId, string moduleCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(companyId) || string.IsNullOrWhiteSpace(moduleCode))
                return false;

            var now = DateTime.UtcNow;
            return await this.storage.SelectAllCompanyModules()
                .AsNoTracking()
                .AnyAsync(m =>
                    m.CompanyId == companyId &&
                    m.ModuleCode == moduleCode &&
                    m.IsActive &&
                    (m.ExpiresAt == null || m.ExpiresAt > now), ct);
        }

        public async Task<IReadOnlyList<CompanyModule>> GetActiveModulesAsync(string companyId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(companyId))
                return Array.Empty<CompanyModule>();

            var now = DateTime.UtcNow;
            return await this.storage.SelectAllCompanyModules()
                .AsNoTracking()
                .Where(m =>
                    m.CompanyId == companyId &&
                    m.IsActive &&
                    (m.ExpiresAt == null || m.ExpiresAt > now))
                .OrderBy(m => m.ModuleCode)
                .ToListAsync(ct);
        }

        public async Task<CompanyModule?> GetModuleAsync(string companyId, string moduleCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(companyId) || string.IsNullOrWhiteSpace(moduleCode))
                return null;

            return await this.storage.SelectAllCompanyModules()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.ModuleCode == moduleCode, ct);
        }

        public async Task<CompanyModule> EnsureModuleAsync(
            string companyId,
            string moduleCode,
            string? configJson = null,
            bool activate = true,
            CancellationToken ct = default)
        {
            var code = moduleCode.Trim().ToLowerInvariant();
            var existing = await this.storage.SelectAllCompanyModules()
                .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.ModuleCode == code, ct);

            if (existing != null)
            {
                var changed = false;
                if (activate && !existing.IsActive)
                {
                    existing.IsActive = true;
                    existing.ActivatedAt = DateTime.UtcNow;
                    changed = true;
                }
                if (configJson != null && !string.Equals(existing.ConfigJson, configJson, StringComparison.Ordinal))
                {
                    existing.ConfigJson = configJson;
                    changed = true;
                }
                if (changed)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    return await this.storage.UpdateCompanyModuleAsync(existing);
                }
                return existing;
            }

            var created = new CompanyModule
            {
                CompanyId = companyId,
                ModuleCode = code,
                ModuleName = ModuleCodes.DisplayName(code),
                IsActive = activate,
                ConfigJson = configJson,
                ActivatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            var inserted = await this.storage.InsertCompanyModuleAsync(created);
            this.logger.LogInformation("Module {Module} activé pour société {CompanyId}", code, companyId);
            return inserted;
        }
    }
}
