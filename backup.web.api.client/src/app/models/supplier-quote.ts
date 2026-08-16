export interface SupplierQuoteOffer {
  supplierId: number;
  supplierName: string;
  feedCode: string;
  supplierSku?: string | null;
  buyPrice: number;
  stockQty: number;
  leadDays: number;
  available: boolean;
  source: string;
  quotedAt: string;
  isBest: boolean;
}

export interface SupplierQuotesResult {
  productId: number;
  bestSupplierId?: number | null;
  scoreReason?: string | null;
  quotedAt: string;
  offers: SupplierQuoteOffer[];
}
