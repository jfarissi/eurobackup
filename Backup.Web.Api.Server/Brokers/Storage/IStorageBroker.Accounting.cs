using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        // Lignes d'écritures (contrôles Phase 1 : compte utilisé, lettrage)
        IQueryable<AccountingEntryLine> SelectAllAccountingEntryLines();

        // Phase 3 : mise à jour d'une ligne (lettrage comptable).
        ValueTask<AccountingEntryLine> UpdateAccountingEntryLineAsync(AccountingEntryLine line);

        // Plan comptable (Phase 1)
        ValueTask<ChartOfAccount> InsertChartOfAccountAsync(ChartOfAccount account);
        IQueryable<ChartOfAccount> SelectAllChartOfAccounts();
        ValueTask<ChartOfAccount?> SelectChartOfAccountByIdAsync(int id);
        ValueTask<ChartOfAccount> UpdateChartOfAccountAsync(ChartOfAccount account);
        ValueTask DeleteChartOfAccountAsync(ChartOfAccount account);

        // Journaux comptables
        ValueTask<Journal> InsertJournalAsync(Journal journal);
        IQueryable<Journal> SelectAllJournals();
        ValueTask<Journal?> SelectJournalByIdAsync(int id);
        ValueTask<Journal> UpdateJournalAsync(Journal journal);
        ValueTask DeleteJournalAsync(Journal journal);

        // Exercices & périodes fiscales
        ValueTask<FiscalYear> InsertFiscalYearAsync(FiscalYear fiscalYear);
        IQueryable<FiscalYear> SelectAllFiscalYears();
        ValueTask<FiscalYear?> SelectFiscalYearByIdAsync(int id);
        ValueTask<FiscalYear> UpdateFiscalYearAsync(FiscalYear fiscalYear);
        IQueryable<FiscalPeriod> SelectAllFiscalPeriods();
        ValueTask<FiscalPeriod?> SelectFiscalPeriodByIdAsync(int id);
        ValueTask<FiscalPeriod> UpdateFiscalPeriodAsync(FiscalPeriod period);

        // Paramètres comptables par société
        ValueTask<CompanyAccountingSettings> InsertCompanyAccountingSettingsAsync(CompanyAccountingSettings settings);
        IQueryable<CompanyAccountingSettings> SelectAllCompanyAccountingSettings();
        ValueTask<CompanyAccountingSettings> UpdateCompanyAccountingSettingsAsync(CompanyAccountingSettings settings);

        // Mapping TVA par taux (Phase 2)
        ValueTask<CompanyVatRateAccount> InsertCompanyVatRateAccountAsync(CompanyVatRateAccount mapping);
        IQueryable<CompanyVatRateAccount> SelectAllCompanyVatRateAccounts();
        ValueTask<CompanyVatRateAccount?> SelectCompanyVatRateAccountByIdAsync(int id);
        ValueTask<CompanyVatRateAccount> UpdateCompanyVatRateAccountAsync(CompanyVatRateAccount mapping);
        ValueTask DeleteCompanyVatRateAccountAsync(CompanyVatRateAccount mapping);

        // Déclarations TVA
        ValueTask<VatDeclaration> InsertVatDeclarationAsync(VatDeclaration declaration);
        IQueryable<VatDeclaration> SelectAllVatDeclarations();
        ValueTask DeleteVatDeclarationAsync(VatDeclaration declaration);

        // Rapprochements bancaires
        ValueTask<BankReconciliation> InsertBankReconciliationAsync(BankReconciliation reconciliation);
        IQueryable<BankReconciliation> SelectAllBankReconciliations();
        ValueTask<BankReconciliation> UpdateBankReconciliationAsync(BankReconciliation reconciliation);

        // Immobilisations & paie
        ValueTask<FixedAsset> InsertFixedAssetAsync(FixedAsset asset);
        IQueryable<FixedAsset> SelectAllFixedAssets();
        ValueTask<FixedAsset> UpdateFixedAssetAsync(FixedAsset asset);

        ValueTask<Employee> InsertEmployeeAsync(Employee employee);
        IQueryable<Employee> SelectAllEmployees();
        ValueTask<Employee> UpdateEmployeeAsync(Employee employee);

        ValueTask<Payslip> InsertPayslipAsync(Payslip payslip);
        IQueryable<Payslip> SelectAllPayslips();
        ValueTask<Payslip> UpdatePayslipAsync(Payslip payslip);

        ValueTask<AccountingFirm> InsertAccountingFirmAsync(AccountingFirm firm);
        IQueryable<AccountingFirm> SelectAllAccountingFirms();
        ValueTask<AccountingFirm> UpdateAccountingFirmAsync(AccountingFirm firm);

        ValueTask<AccountingAnnotation> InsertAccountingAnnotationAsync(AccountingAnnotation annotation);
        IQueryable<AccountingAnnotation> SelectAllAccountingAnnotations();
        ValueTask<AccountingAnnotation> UpdateAccountingAnnotationAsync(AccountingAnnotation annotation);
    }
}
