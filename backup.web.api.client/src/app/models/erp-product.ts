export interface ErpProductSummary {
  id: number;
  erpProductId: string;
  name?: string | null;
  reference?: string | null;
  ean?: string | null;
  brand?: string | null;
  unitPrice?: number | null;
  stockQuantity?: number | null;
}

export interface ErpProductChange {
  id: number;
  erpProductId: number;
  changeType: string;
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
  detectedAt: string;
  syncJobId?: string | null;
  isRead: boolean;
  product?: ErpProductSummary | null;
}

export interface ErpChangesPage {
  total: number;
  page: number;
  pageSize: number;
  items: ErpProductChange[];
}

export interface ErpSyncLog {
  id: number;
  jobId: string;
  status: string;
  startedAt: string;
  completedAt?: string | null;
  totalProducts: number;
  updatedProducts: number;
  newProducts: number;
  failedProducts: number;
  totalChanges: number;
  errorMessage?: string | null;
  details?: string | null;
}

export interface ErpSyncLogsPage {
  total: number;
  page: number;
  pageSize: number;
  items: ErpSyncLog[];
}

export interface ErpChangesQuery {
  unreadOnly?: boolean;
  changeType?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface ExcelImportResult {
  filesScanned: number;
  rowsRead: number;
  created: number;
  updated: number;
  skipped: number;
  errors: string[];
}
