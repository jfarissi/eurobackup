export interface ComparisonLine {
  product: string;
  productKey: string; // Clé pour identifier le produit (ProductCode, EAN, ou nom normalisé)
  documentLineId?: number | null; // ID de la ligne du BL pour les ajustements
  invoiceQty: number;
  deliveryQty: number;
  actualQuantity?: number | null; // Quantité réelle saisie par le vérificateur
  isValidated: boolean; // Indique si l'ajustement a été validé
  stockUpdated: boolean; // Indique si le stock a déjà été mis à jour pour ce BL
  diff: number;
  status: 'OK' | 'Manquant' | 'Surplus' | string;
  currentInvoiceUnitPrice: number;
  previousInvoiceUnitPrice: number;
  priceDiff: number;
  unit?: string;
  invoiceTotalValue?: number;
}

export interface ComparisonResult {
  invoiceId: number;
  deliveryId: number;
  lines: ComparisonLine[];
}

export interface InvoicePriceComparisonResult {
  invoice1Id: number;
  invoice2Id: number;
  invoice1Number?: string | null;
  invoice2Number?: string | null;
  invoice1Supplier?: string | null;
  invoice2Supplier?: string | null;
  lines: InvoicePriceComparisonLine[];
}

export interface InvoicePriceComparisonLine {
  product: string;
  invoice1UnitPrice: number;
  invoice2UnitPrice: number;
  priceDiff: number;
}


