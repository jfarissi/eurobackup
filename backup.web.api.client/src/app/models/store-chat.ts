export interface StoreChatProductSuggestion {
  productId: string;
  name: string;
  price?: number | null;
  brand?: string | null;
  category?: string | null;
  suggestedQuantity?: number | null;
  imageUrl?: string | null;
  tableRowKey?: string;
  lineQty?: number;
  lineCartState?: 'idle' | 'adding' | 'added' | 'error';
}

export interface StoreChatTableCartLine {
  productId: string;
  quantity: number;
}

export interface StoreChatMessageRequest {
  sessionId?: string;
  text?: string;
  sender?: 'user' | 'bot';
  interactionType?: 'text' | 'voice';
  language?: string;
  clientIntent?: string;
  targetProductId?: string;
  targetQuantity?: number;
  tableCartLines?: StoreChatTableCartLine[];
  imageBase64?: string;
  imageFileName?: string;
  imageCaption?: string;
  /** Origine du navigateur pour le retour Stripe (ex. http://localhost:4200). */
  returnBaseUrl?: string;
}

export interface StoreChatQuotePdf {
  pdfBase64: string;
  fileName: string;
  total?: number;
  source?: string | null;
  sourceLabel?: string | null;
}

export interface StoreChatPaymentLink {
  url: string;
  amount?: number | null;
  description?: string | null;
  orderId?: string | null;
  source?: string | null;
  sourceLabel?: string | null;
}

export interface StoreChatResponse {
  sessionId: string;
  replyText: string;
  hasAction: boolean;
  actionType?: string;
  actionData?: unknown;
  activeProjectDomainId?: string | null;
  activeProjectDomainLabel?: string | null;
  salesProjectId?: string | null;
  salesProjectTitle?: string | null;
  searchFilter?: {
    brand?: string | null;
    categories?: string[];
    weightKg?: number | null;
    maxUnitPrice?: number | null;
    skillLevel?: string | null;
    outcome?: string;
  } | null;
  budgetAlert?: string | null;
  skillLevel?: string | null;
  budgetMax?: number | null;
  pack?: {
    packType: string;
    title: string;
    lines: Array<{
      code: string;
      label: string;
      productId?: string | null;
      productName?: string | null;
      unitPrice?: number | null;
      suggestedQuantity: number;
      status: string;
    }>;
    estimatedTotal?: number | null;
    budgetNote?: string | null;
  } | null;
  compareRows?: Array<{
    productId: string;
    name: string;
    brand?: string | null;
    category?: string | null;
    price?: number | null;
    weightHint?: string | null;
  }> | null;
  recommendations?: Array<{ code: string; label: string; reason: string }> | null;
  products?: StoreChatProductSuggestion[];
  quotePdf?: StoreChatQuotePdf;
  paymentLink?: StoreChatPaymentLink;
}

export interface StoreChatPaymentResult {
  status: string;
  orderId?: string | null;
  invoiceNumber?: string | null;
  invoicePdf?: StoreChatQuotePdf | null;
  suggestNewProject?: boolean;
  source?: string | null;
  sourceLabel?: string | null;
}

export interface StoreChatBubble {
  text: string;
  sender: 'user' | 'bot';
  timestamp: Date;
  actionType?: string;
  productSuggestions?: StoreChatProductSuggestion[];
  quotePdf?: StoreChatQuotePdf;
  paymentLink?: StoreChatPaymentLink;
  productListPage?: number;
}
