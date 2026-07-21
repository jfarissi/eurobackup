import { CommonModule } from '@angular/common';
import { Component, NgZone, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import {
  StoreChatBubble,
  StoreChatProductSuggestion,
  StoreChatResponse
} from '../../models/store-chat';
import { StoreChatService } from '../../services/store-chat.service';
import { Subscription, timer } from 'rxjs';

@Component({
  selector: 'app-store-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule],
  templateUrl: './store-assistant.component.html',
  styleUrls: ['./store-assistant.component.css']
})
export class StoreAssistantComponent implements OnInit, OnDestroy {
  private static readonly CHAT_STORAGE_KEY = 'store_assistant_messages';

  messages: StoreChatBubble[] = [
    {
      text: 'Bonjour ! Je suis l’assistant magasin. Demandez un produit, une marque ou un projet (peinture, électricité…).',
      sender: 'bot',
      timestamp: new Date()
    }
  ];

  newMessage = '';
  isRecording = false;
  isTyping = false;
  isGeneratingQuote = false;
  isPlacingOrder = false;
  activeProjectDomainLabel: string | null = null;
  showNewProjectPrompt = false;
  readonly productTablePageSize = 5;

  private routeSub?: Subscription;
  private pollSub?: Subscription;
  private recognition: any = null;
  private handledPaymentOrderId: string | null = null;

  constructor(
    private chat: StoreChatService,
    private route: ActivatedRoute,
    private router: Router,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {
    this.restoreChatFromStorage();
    this.routeSub = this.route.queryParamMap.subscribe(params => {
      const payment = params.get('payment');
      const orderId = params.get('orderId');
      const sessionId = params.get('session_id');
      if (payment === 'success' && orderId) {
        this.handlePaymentReturn(orderId, sessionId);
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.pollSub?.unsubscribe();
    this.stopSpeech(false);
  }

  send(): void {
    const text = this.newMessage.trim();
    if (!text || this.isTyping) return;
    this.newMessage = '';
    this.pushUser(text);
    this.callApi({ text, interactionType: 'text' });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  toggleMic(): void {
    if (this.isRecording) {
      this.stopSpeech(true);
      return;
    }
    this.startSpeech();
  }

  getPagedProductRows(msg: StoreChatBubble): StoreChatProductSuggestion[] {
    const all = msg.productSuggestions ?? [];
    const page = msg.productListPage ?? 0;
    const start = page * this.productTablePageSize;
    return all.slice(start, start + this.productTablePageSize);
  }

  showProductPagination(msg: StoreChatBubble): boolean {
    return (msg.productSuggestions?.length ?? 0) > this.productTablePageSize;
  }

  productListRangeLabel(msg: StoreChatBubble): string {
    const total = msg.productSuggestions?.length ?? 0;
    const page = msg.productListPage ?? 0;
    const start = page * this.productTablePageSize + 1;
    const end = Math.min(total, (page + 1) * this.productTablePageSize);
    return `${start}-${end} / ${total}`;
  }

  canPrevProductPage(msg: StoreChatBubble): boolean {
    return (msg.productListPage ?? 0) > 0;
  }

  canNextProductPage(msg: StoreChatBubble): boolean {
    const total = msg.productSuggestions?.length ?? 0;
    return ((msg.productListPage ?? 0) + 1) * this.productTablePageSize < total;
  }

  prevProductPage(msg: StoreChatBubble): void {
    if (!this.canPrevProductPage(msg)) return;
    msg.productListPage = (msg.productListPage ?? 0) - 1;
  }

  nextProductPage(msg: StoreChatBubble): void {
    if (!this.canNextProductPage(msg)) return;
    msg.productListPage = (msg.productListPage ?? 0) + 1;
  }

  trackProductRow(_: number, p: StoreChatProductSuggestion): string {
    return p.tableRowKey || p.productId;
  }

  formatPrice(p: StoreChatProductSuggestion): string {
    if (p.price == null) return '—';
    return p.price.toLocaleString('fr-BE', { style: 'currency', currency: 'EUR' });
  }

  getLineQty(p: StoreChatProductSuggestion): number {
    return p.lineQty ?? p.suggestedQuantity ?? 1;
  }

  setLineQty(p: StoreChatProductSuggestion, value: number | string): void {
    const n = typeof value === 'string' ? Number(value) : value;
    p.lineQty = !n || n < 1 ? 1 : Math.floor(n);
  }

  normalizeLineQty(p: StoreChatProductSuggestion): void {
    p.lineQty = Math.max(1, Math.floor(this.getLineQty(p)));
  }

  incLineQty(p: StoreChatProductSuggestion): void {
    p.lineQty = this.getLineQty(p) + 1;
  }

  decLineQty(p: StoreChatProductSuggestion): void {
    p.lineQty = Math.max(1, this.getLineQty(p) - 1);
  }

  isProductInCart(p: StoreChatProductSuggestion): boolean {
    return p.lineCartState === 'added';
  }

  isAddingProduct(p: StoreChatProductSuggestion): boolean {
    return p.lineCartState === 'adding';
  }

  toggleCartLineFromList(p: StoreChatProductSuggestion): void {
    if (this.isAddingProduct(p)) return;
    const inCart = this.isProductInCart(p);
    p.lineCartState = 'adding';
    this.callApi({
      text: inCart ? `Retirer ${p.name}` : `Ajouter ${p.name}`,
      clientIntent: inCart ? 'RemoveFromCartFromList' : 'AddToCartFromList',
      targetProductId: p.productId,
      targetQuantity: this.getLineQty(p)
    }, () => {
      p.lineCartState = inCart ? 'idle' : 'added';
    }, () => {
      p.lineCartState = 'error';
    });
  }

  hasTableCartSelection(): boolean {
    return this.messages.some(m =>
      (m.productSuggestions ?? []).some(p => p.lineCartState === 'added'));
  }

  requestQuote(): void {
    if (this.isGeneratingQuote || this.isPlacingOrder) return;
    this.isGeneratingQuote = true;
    this.callApi({
      text: 'Demander un devis',
      clientIntent: 'CreateQuoteFromTableSelection',
      tableCartLines: this.selectedCartLines()
    }, undefined, undefined, () => { this.isGeneratingQuote = false; });
  }

  requestOrder(): void {
    if (this.isGeneratingQuote || this.isPlacingOrder) return;
    this.isPlacingOrder = true;
    this.callApi({
      text: 'Commander',
      clientIntent: 'CreateOrderFromTableSelection',
      tableCartLines: this.selectedCartLines()
    }, undefined, undefined, () => { this.isPlacingOrder = false; });
  }

  downloadQuotePdf(msg: StoreChatBubble): void {
    const pdf = msg.quotePdf;
    if (!pdf?.pdfBase64) return;
    const link = document.createElement('a');
    link.href = `data:application/pdf;base64,${pdf.pdfBase64}`;
    link.download = pdf.fileName || 'document.pdf';
    link.click();
  }

  startPayment(msg: StoreChatBubble): void {
    const url = msg.paymentLink?.url?.trim();
    if (!url) return;
    this.saveChatToStorage();
    window.location.href = url;
  }

  startNewProject(): void {
    this.showNewProjectPrompt = false;
    this.messages = [{
      text: 'Nouveau projet. Que souhaitez-vous faire ?',
      sender: 'bot',
      timestamp: new Date()
    }];
    this.activeProjectDomainLabel = null;
    this.callApi({ text: 'Nouveau projet', clientIntent: 'NewProject' });
  }

  private selectedCartLines() {
    const map = new Map<string, number>();
    for (const msg of this.messages) {
      for (const p of msg.productSuggestions ?? []) {
        if (p.lineCartState === 'added') {
          map.set(p.productId, this.getLineQty(p));
        }
      }
    }
    return [...map.entries()].map(([productId, quantity]) => ({ productId, quantity }));
  }

  private callApi(
    payload: {
      text?: string;
      clientIntent?: string;
      targetProductId?: string;
      targetQuantity?: number;
      tableCartLines?: { productId: string; quantity: number }[];
      interactionType?: 'text' | 'voice';
    },
    onOk?: () => void,
    onErr?: () => void,
    onFinally?: () => void
  ): void {
    this.isTyping = true;
    this.chat.sendMessage({
      sender: 'user',
      language: 'fr',
      ...payload
    }).subscribe({
      next: (res) => {
        this.isTyping = false;
        this.applyBotResponse(res);
        onOk?.();
        onFinally?.();
        this.scrollToBottom();
      },
      error: () => {
        this.isTyping = false;
        this.messages.push({
          text: 'Désolé, une erreur est survenue. Réessayez.',
          sender: 'bot',
          timestamp: new Date()
        });
        onErr?.();
        onFinally?.();
      }
    });
  }

  private applyBotResponse(res: StoreChatResponse): void {
    if (res.activeProjectDomainLabel) {
      this.activeProjectDomainLabel = res.activeProjectDomainLabel;
    }

    const products = (res.products
      ?? (res.actionType === 'PRODUCT_LIST' ? (res.actionData as StoreChatProductSuggestion[]) : null)
      ?? [])
      .map((p, index) => ({
        ...p,
        tableRowKey: `${Date.now()}-${index}-${p.productId}`,
        lineQty: p.suggestedQuantity ?? 1,
        lineCartState: 'idle' as const
      }));

    this.messages.push({
      text: res.replyText,
      sender: 'bot',
      timestamp: new Date(),
      actionType: res.actionType,
      productSuggestions: products.length ? products : undefined,
      productListPage: 0,
      quotePdf: res.quotePdf,
      paymentLink: res.paymentLink
    });
  }

  private pushUser(text: string): void {
    this.messages.push({ text, sender: 'user', timestamp: new Date() });
    this.scrollToBottom();
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = document.querySelector('.assistant-messages');
      if (el) el.scrollTop = el.scrollHeight;
    }, 50);
  }

  private startSpeech(): void {
    const SpeechRecognitionCtor =
      (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    if (!SpeechRecognitionCtor) {
      this.messages.push({
        text: 'La reconnaissance vocale n’est pas disponible sur ce navigateur.',
        sender: 'bot',
        timestamp: new Date()
      });
      return;
    }

    this.recognition = new SpeechRecognitionCtor();
    this.recognition.lang = 'fr-FR';
    this.recognition.interimResults = true;
    this.recognition.continuous = false;
    this.isRecording = true;

    this.recognition.onresult = (event: any) => {
      let transcript = '';
      for (let i = event.resultIndex; i < event.results.length; i++) {
        transcript += event.results[i][0].transcript;
      }
      this.ngZone.run(() => {
        this.newMessage = transcript.trim();
      });
    };

    this.recognition.onend = () => {
      this.ngZone.run(() => {
        this.isRecording = false;
        if (this.newMessage.trim()) {
          this.send();
        }
      });
    };

    this.recognition.onerror = () => {
      this.ngZone.run(() => { this.isRecording = false; });
    };

    this.recognition.start();
  }

  private stopSpeech(sendAfter: boolean): void {
    try {
      this.recognition?.stop?.();
    } catch { /* ignore */ }
    this.isRecording = false;
    if (sendAfter && this.newMessage.trim()) {
      this.send();
    }
  }

  private handlePaymentReturn(orderId: string, sessionId: string | null): void {
    if (this.handledPaymentOrderId === orderId) return;
    this.handledPaymentOrderId = orderId;
    this.clearPaymentQueryParams();
    this.confirmAndShowInvoice(orderId, sessionId);
  }

  private clearPaymentQueryParams(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { payment: null, orderId: null, session_id: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private confirmAndShowInvoice(orderId: string, sessionId: string | null, attempt = 0): void {
    this.chat.confirmPayment(orderId, sessionId).subscribe({
      next: (result) => {
        if (result.status === 'paid' && result.invoicePdf?.pdfBase64) {
          this.showNewProjectPrompt = result.suggestNewProject !== false;
          this.messages.push({
            text: `Paiement confirmé${result.invoiceNumber ? ` — facture ${result.invoiceNumber}` : ''}.`,
            sender: 'bot',
            timestamp: new Date(),
            actionType: 'INVOICE_PDF',
            quotePdf: result.invoicePdf
          });
          this.scrollToBottom();
          return;
        }
        if (result.status === 'pending' && attempt < 15) {
          this.pollSub?.unsubscribe();
          this.pollSub = timer(2000).subscribe(() =>
            this.confirmAndShowInvoice(orderId, sessionId, attempt + 1));
        }
      },
      error: () => {
        if (attempt < 10) {
          this.pollSub?.unsubscribe();
          this.pollSub = timer(2000).subscribe(() =>
            this.confirmAndShowInvoice(orderId, sessionId, attempt + 1));
        }
      }
    });
  }

  private saveChatToStorage(): void {
    try {
      sessionStorage.setItem(StoreAssistantComponent.CHAT_STORAGE_KEY, JSON.stringify(this.messages));
    } catch { /* ignore */ }
  }

  private restoreChatFromStorage(): void {
    try {
      const raw = sessionStorage.getItem(StoreAssistantComponent.CHAT_STORAGE_KEY);
      if (!raw) return;
      const parsed = JSON.parse(raw) as StoreChatBubble[];
      if (Array.isArray(parsed) && parsed.length > 0) {
        this.messages = parsed.map(m => ({
          ...m,
          timestamp: m.timestamp ? new Date(m.timestamp as unknown as string) : new Date()
        }));
      }
      sessionStorage.removeItem(StoreAssistantComponent.CHAT_STORAGE_KEY);
    } catch { /* ignore */ }
  }
}
