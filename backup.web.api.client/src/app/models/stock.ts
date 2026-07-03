export interface StockItem {
  id: number;
  productKey: string;
  quantityOnHand: number;
  lastUpdated: string; // ISO
}

export interface CompareAndStockResponse {
  success: boolean;
}

export interface BatchCompareAndStockResponse {
  invoiceId: number;
  totalDeliveries: number;
  updatedDeliveries: number;
  skippedDeliveries: number;
  updatedDeliveryIds: number[];
  skippedDeliveryIds: number[];
}


