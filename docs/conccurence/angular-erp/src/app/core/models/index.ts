// ============================================================
// src/app/core/models/index.ts
// Modèles partagés entre tous les modules
// ============================================================

export interface ErpCompanyModule {
  id: string;
  companyId: string;
  moduleCode: string;
  moduleName: string;
  isActive: boolean;
  configJson?: string;
  activatedAt: string;
  expiresAt?: string;
}

export interface Product {
  id: number;
  erpProductId: string;
  name?: string;
  reference?: string;
  ean?: string;
  brand?: string;
  priceHT?: number;
  stockQuantity?: number;
  weight?: number;
  height?: number;
  width?: number;
  depth?: number;
  images?: ProductImage[];
  vehicles?: VehicleCompatibility[];
  oemNumbers?: OemCrossRef[];
}

export interface ProductImage {
  url: string;
  isMain: boolean;
  sortOrder: number;
}

export interface VehicleCompatibility {
  make: string;
  model: string;
  yearFrom?: number;
  yearTo?: number;
  engineCode?: string;
}

export interface OemCrossRef {
  oemNumber: string;
  brand?: string;
  isOriginal: boolean;
}

export interface SyncRequest {
  syncType: 'oem' | 'vehicle' | 'full';
  oemNumber?: string;
  vehicleId?: number;
  maxPages?: number;
}

export interface SyncResult {
  jobId: string;
  status: string;
  productsCreated: number;
  productsUpdated: number;
  imagesAdded: number;
  vehiclesAdded: number;
  errorsCount: number;
}

export interface AutoPartsModuleConfig {
  apiSource: 'rapidapi' | 'tecdoc' | 'epicor';
  apiKey?: string;
  syncFrequency: 'hourly' | 'daily' | 'weekly';
  defaultVat: number;
  defaultLanguage: string;
  includeOemCrossRefs: boolean;
  includeVehicleCompatibility: boolean;
}
