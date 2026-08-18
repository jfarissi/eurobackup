/** Miroir de Backup.Web.Api.Server.Models.Entities.Accounting.ChartOfAccount. */
export interface ChartOfAccount {
  id: number;
  accountNumber: string;
  label: string;
  labelArabic?: string | null;
  /** Classe comptable (1 à 8). */
  accountClass: number;
  /** Actif / Passif / Charge / Produit / CapitauxPropres. */
  accountType: string;
  isLettrable: boolean;
  isBilan: boolean;
  isResultat: boolean;
  parentId?: number | null;
}

export interface ChartOfAccountForm {
  accountNumber: string;
  label: string;
  labelArabic?: string | null;
  accountClass: number;
  accountType: string;
  isLettrable: boolean;
  isBilan: boolean;
  isResultat: boolean;
  parentId?: number | null;
}

/** Plan groupé par classe (GET api/chart-of-accounts/tree). */
export interface ChartOfAccountClassGroup {
  accountClass: number;
  accounts: ChartOfAccount[];
}

/** Miroir de Backup.Web.Api.Server.Models.Entities.Accounting.Journal. */
export interface Journal {
  id: number;
  /** ACH, VEN, BAN, CAIS, OD, AN. */
  code: string;
  label: string;
  counterpartAccountCode?: string | null;
}

export interface JournalForm {
  code: string;
  label: string;
  counterpartAccountCode?: string | null;
}

/** Miroir de Backup.Web.Api.Server.Models.Entities.Accounting.FiscalPeriod. */
export interface FiscalPeriod {
  id: number;
  fiscalYearId: number;
  year: number;
  /** Mois calendaire (1 à 12). */
  month: number;
  isLocked: boolean;
  isVatDeclared: boolean;
  isBankReconciled: boolean;
}

/** Miroir de Backup.Web.Api.Server.Models.Entities.Accounting.FiscalYear. */
export interface FiscalYear {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  /** Open / Closed. */
  status: string;
  periods: FiscalPeriod[];
}

export interface OpenFiscalYearRequest {
  startDate: string;
  endDate: string;
  name?: string | null;
}

/** Phase 3 — statuts du cycle de vie d'une écriture comptable. */
export type AccountingEntryStatus = 'Draft' | 'Posted' | 'Validated' | 'Reversed';

/** Miroir de LettrageService.LettrageLineDto (GET api/lettrage/unlettered). */
export interface LettrageLine {
  lineId: number;
  accountingEntryId: number;
  entryNumber: string;
  entryDate: string;
  description: string;
  journalType: string;
  lineNumber: number;
  accountCode: string;
  accountLabel: string;
  debit: number;
  credit: number;
}

/** Miroir de LettrageService.LettrageGroupDto (GET api/lettrage/groups). */
export interface LettrageGroup {
  code: string;
  date?: string | null;
  accountCode: string;
  lineCount: number;
  totalDebit: number;
  totalCredit: number;
}

/** Miroir de LettrageService.LettrageAccountSummaryDto (POST api/lettrage/automatic). */
export interface LettrageAccountSummary {
  accountCode: string;
  groupsCreated: number;
  codes: string[];
}

/** Miroir de AccountingReportsService.BalanceRowDto. */
export interface BalanceRow {
  accountCode: string;
  accountLabel: string;
  openingDebit: number;
  openingCredit: number;
  periodDebit: number;
  periodCredit: number;
  closingDebit: number;
  closingCredit: number;
}

/** Miroir de AccountingReportsService.BalanceReportDto (GET api/accounting-reports/balance). */
export interface BalanceReport {
  from?: string | null;
  to?: string | null;
  rows: BalanceRow[];
  totalOpeningDebit: number;
  totalOpeningCredit: number;
  totalPeriodDebit: number;
  totalPeriodCredit: number;
  totalClosingDebit: number;
  totalClosingCredit: number;
}

/** Miroir de AccountingReportsService.LedgerMovementDto. */
export interface LedgerMovement {
  entryDate: string;
  entryNumber: string;
  journalType: string;
  description: string;
  lineNumber: number;
  debit: number;
  credit: number;
  runningBalance: number;
}

/** Miroir de AccountingReportsService.LedgerReportDto (GET api/accounting-reports/general-ledger). */
export interface LedgerReport {
  accountCode: string;
  accountLabel: string;
  from?: string | null;
  to?: string | null;
  openingDebit: number;
  openingCredit: number;
  openingBalance: number;
  movements: LedgerMovement[];
  periodDebit: number;
  periodCredit: number;
  closingDebit: number;
  closingCredit: number;
  closingBalance: number;
}

/** Miroir de VatDeclarationService.VatRateRowDto. */
export interface VatRateRow {
  rate: number;
  collectedBase: number;
  collectedVat: number;
  deductibleBase: number;
  deductibleVat: number;
}

/** Miroir de VatDeclarationService.VatDeclarationDto (GET api/vat-declarations). */
export interface VatDeclaration {
  id?: number | null;
  year: number;
  month: number;
  from: string;
  to: string;
  status: 'Draft' | 'Declared';
  fiscalPeriodId?: number | null;
  periodVatDeclared: boolean;
  rates: VatRateRow[];
  totalCollected: number;
  totalDeductible: number;
  previousCredit: number;
  netToPay: number;
  declaredAt?: string | null;
  declaredBy?: string | null;
  alerts: string[];
}

/** Miroir de BankReconciliationService.StatementLineDto. */
export interface BankStatementLine {
  id: number;
  operationDate: string;
  label: string;
  reference?: string | null;
  debit: number;
  credit: number;
  runningBalance?: number | null;
  isMatched: boolean;
  matchMethod?: string | null;
  accountingEntryId?: number | null;
  accountingEntryLineId?: number | null;
  entryNumber?: string | null;
}

/** Miroir de BankReconciliationService.LedgerCandidateDto. */
export interface BankLedgerCandidate {
  lineId: number;
  entryId: number;
  entryNumber: string;
  entryDate: string;
  description: string;
  debit: number;
  credit: number;
}

/** Miroir de BankReconciliationService.ReconciliationDto (GET api/bank-reconciliations). */
export interface BankReconciliation {
  id: number;
  accountCode: string;
  fileName?: string | null;
  statementDate: string;
  fromDate: string;
  toDate: string;
  statementBalance: number;
  bookBalance: number;
  difference: number;
  status: 'Open' | 'Balanced';
  lineCount: number;
  matchedCount: number;
  completedAt?: string | null;
  completedBy?: string | null;
  lines: BankStatementLine[];
  unmatchedLedger: BankLedgerCandidate[];
}

/** Miroir de BankReconciliationService.MatchResultDto. */
export interface BankMatchResult {
  matched: number;
  remaining: number;
  reconciliation?: BankReconciliation | null;
}

/** Miroir de FixedAssetService.ScheduleLineDto. */
export interface DepreciationScheduleLine {
  id: number;
  year: number;
  month: number;
  charge: number;
  accumulated: number;
  netBookValue: number;
  isPosted: boolean;
  accountingEntryId?: number | null;
}

/** Miroir de FixedAssetService.AssetDto. */
export interface FixedAsset {
  id: number;
  code: string;
  designation: string;
  assetAccountCode: string;
  depreciationAccountCode: string;
  expenseAccountCode: string;
  acquisitionDate: string;
  serviceDate: string;
  originValue: number;
  residualValue: number;
  durationMonths: number;
  mode: string;
  decliningRate?: number | null;
  accumulatedDepreciation: number;
  netBookValue: number;
  isActive: boolean;
  disposalDate?: string | null;
  schedule: DepreciationScheduleLine[];
}

export interface FixedAssetForm {
  code?: string | null;
  designation: string;
  assetAccountCode?: string | null;
  depreciationAccountCode?: string | null;
  expenseAccountCode?: string | null;
  acquisitionDate: string;
  serviceDate: string;
  originValue: number;
  residualValue: number;
  durationMonths: number;
  mode: string;
}

export interface FixedAssetPostResult {
  postedLines: number;
  accountingEntryId?: number | null;
  entryNumber?: string | null;
}

/** Miroir de PayrollService.EmployeeDto. */
export interface PayrollEmployee {
  id: number;
  lastName: string;
  firstName: string;
  cnssNumber?: string | null;
  baseSalary: number;
  overtime: number;
  bonuses: number;
  benefitsInKind: number;
  hireDate: string;
  exitDate?: string | null;
  isActive: boolean;
}

export interface PayrollEmployeeForm {
  lastName: string;
  firstName: string;
  cnssNumber?: string | null;
  baseSalary: number;
  overtime: number;
  bonuses: number;
  benefitsInKind: number;
  hireDate: string;
  exitDate?: string | null;
  isActive: boolean;
}

/** Miroir de PayrollService.PayslipDto. */
export interface Payslip {
  id: number;
  employeeId: number;
  employeeName: string;
  cnssNumber?: string | null;
  year: number;
  month: number;
  baseSalary: number;
  overtime: number;
  bonuses: number;
  benefitsInKind: number;
  gross: number;
  cnssEmployee: number;
  cnssEmployer: number;
  amoEmployee: number;
  amoEmployer: number;
  igr: number;
  net: number;
  isPosted: boolean;
  accountingEntryId?: number | null;
  isExportedCnss: boolean;
}

/** Miroir de PayrollService.PeriodSummaryDto. */
export interface PayrollPeriodSummary {
  year: number;
  month: number;
  payslipCount: number;
  totalGross: number;
  totalNet: number;
  totalCnss: number;
  totalIgr: number;
  allPosted: boolean;
  payslips: Payslip[];
}

export interface PayrollPostResult {
  postedCount: number;
  accountingEntryId?: number | null;
  entryNumber?: string | null;
}
