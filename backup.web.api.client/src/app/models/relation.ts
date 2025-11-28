export interface DocumentRelation {
  id: number;
  invoiceId: number;
  deliveryId: number;
  statutComparaison?: string | null;
  resultatComparaison?: string | null;
  dateLiaison?: string | null;
  creePar?: string | null;
}


