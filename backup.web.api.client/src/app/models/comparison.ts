export interface ComparisonLine {
  product: string;
  invoiceQty: number;
  deliveryQty: number;
  diff: number;
  status: 'OK' | 'Manquant' | 'Surplus' | string;
  currentInvoiceUnitPrice: number;
  previousInvoiceUnitPrice: number;
  priceDiff: number;
}

export interface ComparisonResult {
  invoiceId: number;
  deliveryId: number;
  lines: ComparisonLine[];
}

export interface InvoicePriceComparisonResult {
  invoice1Id: number;
  invoice2Id: number;
  lines: InvoicePriceComparisonLine[];
}

export interface InvoicePriceComparisonLine {
  product: string;
  invoice1UnitPrice: number;
  invoice2UnitPrice: number;
  priceDiff: number;
}


