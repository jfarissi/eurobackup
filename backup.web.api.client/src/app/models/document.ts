export interface Document {
  id: number;
  typeDocument: 'Facture' | 'BonLivraison' | string;
  numero?: string | null;
  client?: string | null;
  supplier?: string | null;
  dateDocument?: string | null; // ISO string
  originalFileName: string;
  filePath: string;
  contentText: string;
  dateAdded: string; // ISO string
}


