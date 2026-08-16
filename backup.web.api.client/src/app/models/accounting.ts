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
