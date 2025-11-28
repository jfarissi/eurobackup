export interface DocumentLine {
  id: number;
  documentId: number;
  lineNumber: number;
  product: string;
  productCode?: string | null;
  quantity: number;
  unit?: string | null;
  unitPrice: number;
  totalValue: number;
}


