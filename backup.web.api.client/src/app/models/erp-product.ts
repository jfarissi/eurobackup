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

export interface ErpProduct {
  id: number;
  erpProductId: string;
  name?: string | null;
  name2?: string | null;
  reference?: string | null;
  ean?: string | null;
  brand?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  comment?: string | null;
  link?: string | null;
  picName?: string | null;
  priceHT?: number | null;
  unitPrice?: number | null;
  cPrice?: number | null;
  rPrice?: number | null;
  vatIncluded?: boolean;
  typeVatPerc?: number | null;
  discountPerc?: number | null;
  discountPrice?: number | null;
  stockQuantity?: number | null;
  stockDate?: string | null;
  weight?: number | null;
  height?: number | null;
  width?: number | null;
  depth?: number | null;
  mainTypeName?: string | null;
  mainSubTypeName?: string | null;
  typeName?: string | null;
  subTypeName?: string | null;
  promoActive?: boolean;
  promoPrice?: number | null;
  archived?: boolean | null;
  dataSource?: string | null;
  sourceFile?: string | null;
  fromExcel?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
  lastSyncAt?: string | null;
}

export interface ErpProductsPage {
  total: number;
  page: number;
  pageSize: number;
  items: ErpProduct[];
}

export interface ErpProductsQuery {
  page?: number;
  pageSize?: number;
  brand?: string;
  q?: string;
  fromExcel?: boolean;
  dataSource?: string;
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
