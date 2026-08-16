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
  brandId?: number | null;
  categoryId?: number | null;
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
  /** IDs ERP (JSON camelCase: mainTypeID). */
  mainTypeID?: string | null;
  typeID?: string | null;
  subTypeID?: string | null;
  promoActive?: boolean;
  promoPrice?: number | null;
  archived?: boolean | null;
  dataSource?: string | null;
  sourceFile?: string | null;
  fromExcel?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
  createdBy?: string | null;
  updatedBy?: string | null;
  lastSyncAt?: string | null;
  isDropship?: boolean;
  dropshipSupplierId?: number | null;
  /** Fitment véhicule (liste catalogue, renseigné si filtre véhicule actif). */
  vehicleMake?: string | null;
  vehicleModel?: string | null;
  vehicleTypeName?: string | null;
  vehicleYearFrom?: number | null;
  vehicleYearTo?: number | null;
  vehicleEngineCode?: string | null;
  vehicleKType?: string | null;
  vehicleFuelType?: string | null;
}

export interface ErpProductsPage {
  total: number;
  page: number;
  pageSize: number;
  items: ErpProduct[];
}

/** Fitment véhicule lié à un produit (ErpProductVehicles). */
export interface ErpProductVehicle {
  id: string;
  make: string;
  model: string;
  typeName?: string | null;
  yearFrom?: number | null;
  yearTo?: number | null;
  engineCode?: string | null;
  kType?: string | null;
  bodyType?: string | null;
  fuelType?: string | null;
  driveType?: string | null;
  transmission?: string | null;
  powerKW?: number | null;
  powerHP?: number | null;
  ccm?: number | null;
  cylinders?: number | null;
  valves?: number | null;
}

export interface ErpProductsQuery {
  page?: number;
  pageSize?: number;
  brand?: string;
  q?: string;
  fromExcel?: boolean;
  dataSource?: string;
  mainTypeId?: string;
  typeId?: string;
  subTypeId?: string;
  /** Filtre marques du fournisseur (Brand LIKE token dérivé du nom). */
  supplierId?: number;
  /** Filtre compatibilité véhicule (attribut vehicle_compat / ErpProductVehicles). */
  vehicleBrand?: string;
  vehicleModel?: string;
  vehicleYear?: number | string;
  /** Carburant véhicule (Diesel, Petrol/Essence…). */
  vehicleFuel?: string;
  vehicleBody?: string;
  vehicleDrive?: string;
  vehicleTransmission?: string;
  vehicleEngine?: string;
  /** K-Type TecDoc / vehicleId catalogue. */
  vehicleKType?: string;
}

export interface ErpVehicleFacets {
  fuels: string[];
  bodyTypes: string[];
  driveTypes: string[];
  transmissions: string[];
}

/** Cross-référence OEM liée à un produit. */
export interface ErpProductOem {
  id: string;
  oemNumber: string;
  brand?: string | null;
  isOriginal: boolean;
}

export interface OemSearchHit {
  productId: number;
  erpProductId: string;
  name?: string | null;
  reference?: string | null;
  brand?: string | null;
  unitPrice?: number | null;
  stockQuantity?: number | null;
  matchedOem: string;
  oemBrand?: string | null;
  isOriginal: boolean;
}

export interface OemSearchPage {
  total: number;
  page: number;
  pageSize: number;
  query: string;
  items: OemSearchHit[];
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
  processedProducts?: number;
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

export type ErpChangeValueMode = '' | 'both' | 'cleared' | 'added';

export interface ErpChangesQuery {
  unreadOnly?: boolean;
  changeType?: string;
  /** both = Avant+Après renseignés ; cleared = Après vide ; added = Avant vide */
  valueMode?: ErpChangeValueMode;
  q?: string;
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

export interface CarApiImportResult {
  partsTotal: number;
  partsCreated: number;
  partsUpdated: number;
  partsSkipped: number;
  variantsCreated: number;
  vehicleBrandsTotal: number;
  vehicleBrandsCreated: number;
  vehicleBrandsSkipped: number;
  categoriesCreated: number;
  frenchNamesUpdated: number;
  vehicleAttributeEnsured: boolean;
  errors: string[];
}

export interface ErpBrand {
  id: number;
  name: string;
  slug: string;
  logoUrl?: string | null;
  websiteUrl?: string | null;
  description?: string | null;
  isActive: boolean;
}

export interface ErpCategory {
  id: number;
  erpExternalId: string;
  level: string;
  nameNl: string;
  nameFr: string;
  nameEn: string;
  slugNl?: string;
  slugFr?: string;
  slugEn?: string;
  parentId?: number | null;
  sortOrder: number;
  isActive: boolean;
}

export interface ErpCatalogSyncFilter {
  mainTypeId?: string;
  typeId?: string;
  subTypeId?: string;
  brand?: string;
}

export interface CreateErpProductRequest {
  name?: string;
  reference?: string;
  ean?: string;
  purchasePrice?: number;
  unitPrice?: number;
  vatPercent?: number;
  brandId?: number;
  brandName?: string;
  categoryId?: number;
  supplierName?: string;
  isDropship?: boolean;
  dropshipSupplierId?: number | null;
}

export interface CreateErpProductResult {
  product: ErpProduct;
  created: boolean;
  message?: string;
}

export interface SuggestBrandResult {
  token: string | null;
  brands: Array<{ id: number; name: string; slug?: string; isActive?: boolean }>;
  suggestedBrandId: number | null;
}
