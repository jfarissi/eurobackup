export interface StockItem {
  id: number;
  productKey: string;
  quantityOnHand: number;
  lastUpdated: string; // ISO date string
  lastDeliveryId?: number | null; // ID du dernier BL qui a mis à jour ce produit
  supplier?: string | null; // Fournisseur
  description?: string | null; // Libellé du produit
  unit?: string | null; // Unité (ST, KG, PC, etc.)
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

