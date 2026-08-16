export interface StockItem {
  id: number;
  productKey: string;
  quantityOnHand: number;
  reservedQuantity?: number;
  minStock?: number;
  /** CMUP / CMP — coût moyen unitaire pondéré */
  averageCost?: number;
  lastUpdated: string; // ISO date string
  lastDeliveryId?: number | null; // ID du dernier BL qui a mis à jour ce produit
  supplier?: string | null; // Fournisseur
  description?: string | null; // Libellé du produit
  unit?: string | null; // Unité (ST, KG, PC, etc.)
}

export type StockForecastRisk = 'Critical' | 'Warning' | 'Watch' | 'Ok';
export type StockForecastTrend = 'Up' | 'Down' | 'Stable';

export interface StockForecastLine {
  stockItemId: number;
  productKey: string;
  description?: string | null;
  supplier?: string | null;
  quantityOnHand: number;
  reservedQuantity: number;
  available: number;
  minStock: number;
  qtyOutLookback: number;
  avgDailyOut: number;
  daysOfCover: number | null;
  dynamicMin: number;
  suggestedQty: number;
  risk: StockForecastRisk;
  trend: StockForecastTrend;
  stockoutAt?: string | null;
}

export interface StockForecastResult {
  lookbackDays: number;
  horizonDays: number;
  criticalCount: number;
  warningCount: number;
  watchCount: number;
  items: StockForecastLine[];
}

export interface StockUpdate {
  id: number;
  productKey: string;
  quantityDelta: number;
  quantityAfter: number;
  deliveryId: number;
  invoiceId?: number | null;
  updatedAt: string; // ISO date string
}

