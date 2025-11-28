export interface StockItem {
  id: number;
  productKey: string;
  quantityOnHand: number;
  lastUpdated: string; // ISO
}

export interface CompareAndStockResponse {
  success: boolean;
}


