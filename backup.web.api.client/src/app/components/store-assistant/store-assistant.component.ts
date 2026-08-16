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
import { AssistantI18nService, AssistantLang } from '../../services/assistant-i18n.service';
import { StoreChatService } from '../../services/store-chat.service';
import { CompanyService } from '../../services/company.service';
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

  messages: StoreChatBubble[] = [];

  newMessage = '';
  isRecording = false;
  isTyping = false;
  isGeneratingQuote = false;
  isPlacingOrder = false;
  activeProjectDomainLabel: string | null = null;
  activeProjectDomainId: string | null = null;
  guideComplete = false;
  /** @deprecated alias de guideComplete (compat réponses API). */
  wallGuideComplete = false;
  salesProjectTitle: string | null = null;
  salesProjectId: string | null = null;
  skillLevel: string | null = null;
  budgetMax: number | null = null;
  showNewProjectPrompt = false;

  get salesProjectIdShort(): string {
    return this.salesProjectId ? this.salesProjectId.slice(0, 8) : '';
  }

  get isWallProject(): boolean {
    return this.activeProjectDomainId === 'wall_construction';
  }

  /** Domaine avec parcours guidé actif (registre serveur). */
  get hasActiveProjectGuide(): boolean {
    return !!this.activeProjectDomainId;
  }

  /** Afficher « Étape suivante » tant que le parcours guidé n'est pas terminé. */
  get showProjectNextStep(): boolean {
    return this.hasActiveProjectGuide && !this.guideComplete;
  }

  /** @deprecated alias — templates / i18n. */
  get showWallNextStep(): boolean {
    return this.showProjectNextStep;
  }

  /** CTA Suivant sous une liste produits (guide → étape, sinon plus de produits). */
  get showNextCta(): boolean {
    return this.showProjectNextStep || this.lastBotHasProducts;
  }

  get lastBotHasProducts(): boolean {
    for (let i = this.messages.length - 1; i >= 0; i--) {
      const m = this.messages[i];
      if (m.sender !== 'bot') continue;
      return !!(m.productSuggestions && m.productSuggestions.length > 0);
    }
    return false;
  }

  cartPanelOpen = false;
  readonly productTablePageSize = 3;

  private routeSub?: Subscription;
  private pollSub?: Subscription;
  private recognition: any = null;
  private handledPaymentOrderId: string | null = null;

  constructor(
    private chat: StoreChatService,
    private route: ActivatedRoute,
    private router: Router,
    private ngZone: NgZone,
    public i18n: AssistantI18nService,
    public company: CompanyService
  ) {
    this.messages = [{
      text: this.i18n.t('welcome'),
      sender: 'bot',
      timestamp: new Date()
    }];
  }

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

  get showAutoPartsHint(): boolean {
    return this.company.hasAutoParts;
  }

  sendAutoPartsDemo(): void {
    if (this.isTyping) return;
    this.newMessage = this.i18n.t('autoPartsDemoPrompt');
    this.send();
  }

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || this.isTyping) return;

    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || '');
      const base64 = result.includes(',') ? result.split(',')[1] : result;
      const caption = this.newMessage.trim() || file.name;
      this.newMessage = '';
      this.pushUser(`📷 ${caption}`);
      this.callApi({
        text: caption,
        imageBase64: base64,
        imageFileName: file.name,
        imageCaption: caption,
        interactionType: 'text'
      });
      input.value = '';
    };
    reader.readAsDataURL(file);
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
    return p.price.toLocaleString(this.i18n.numberLocale(), { style: 'currency', currency: 'EUR' });
  }

  onProductImageError(event: Event): void {
    const img = event.target as HTMLImageElement | null;
    if (!img) return;
    img.style.display = 'none';
    const parent = img.parentElement;
    if (parent && !parent.querySelector('.product-thumb.placeholder')) {
      const ph = document.createElement('div');
      ph.className = 'product-thumb placeholder';
      ph.textContent = '—';
      parent.appendChild(ph);
    }
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

  get cartItemCount(): number {
    return this.cartLines.length;
  }

  get cartLines(): Array<{
    productId: string;
    name: string;
    quantity: number;
    price?: number | null;
    removing?: boolean;
  }> {
    const map = new Map<string, {
      productId: string;
      name: string;
      quantity: number;
      price?: number | null;
      removing?: boolean;
    }>();

    for (const msg of this.messages) {
      for (const p of msg.productSuggestions ?? []) {
        if (p.lineCartState !== 'added' && p.lineCartState !== 'adding') continue;
        const existing = map.get(p.productId);
        const qty = this.getLineQty(p);
        if (!existing || qty >= existing.quantity) {
          map.set(p.productId, {
            productId: p.productId,
            name: p.name,
            quantity: qty,
            price: p.price,
            removing: p.lineCartState === 'adding'
          });
        }
      }
    }

    return [...map.values()];
  }

  toggleCartPanel(): void {
    this.cartPanelOpen = !this.cartPanelOpen;
  }

  formatCartLinePrice(line: { quantity: number; price?: number | null }): string {
    if (line.price == null) return '';
    const total = line.price * line.quantity;
    return total.toLocaleString(this.i18n.numberLocale(), { style: 'currency', currency: 'EUR' });
  }

  toggleCartLineFromList(p: StoreChatProductSuggestion): void {
    if (this.isAddingProduct(p)) return;
    if (this.isProductInCart(p)) {
      this.removeProductFromCart(p);
      return;
    }
    this.setProductCartState(p.productId, 'adding');
    this.callApi({
      text: `Ajouter ${p.name}`,
      clientIntent: 'AddToCartFromList',
      targetProductId: p.productId,
      targetQuantity: this.getLineQty(p)
    }, () => {
      this.setProductCartState(p.productId, 'added');
      this.cartPanelOpen = true;
    }, () => {
      this.setProductCartState(p.productId, 'error');
    });
  }

  removeProductFromCart(p: StoreChatProductSuggestion): void {
    if (this.isAddingProduct(p)) return;
    this.setProductCartState(p.productId, 'adding');
    this.callApi({
      text: `Retirer ${p.name}`,
      clientIntent: 'RemoveFromCartFromList',
      targetProductId: p.productId
    }, () => {
      this.setProductCartState(p.productId, 'idle');
    }, () => {
      this.setProductCartState(p.productId, 'added');
    });
  }

  removeCartLine(line: { productId: string; name: string }): void {
    const stub: StoreChatProductSuggestion = {
      productId: line.productId,
      name: line.name,
      lineCartState: 'added'
    };
    this.removeProductFromCart(stub);
  }

  hasTableCartSelection(): boolean {
    return this.cartItemCount > 0;
  }

  requestQuote(): void {
    if (this.isGeneratingQuote || this.isPlacingOrder) return;
    this.isGeneratingQuote = true;
    this.callApi({
      text: this.i18n.t('quote'),
      clientIntent: 'CreateQuoteFromTableSelection',
      tableCartLines: this.selectedCartLines()
    }, undefined, undefined, () => { this.isGeneratingQuote = false; });
  }

  requestOrder(): void {
    if (this.isGeneratingQuote || this.isPlacingOrder) return;
    this.isPlacingOrder = true;
    this.callApi({
      text: this.i18n.t('order'),
      clientIntent: 'CreateOrderFromTableSelection',
      tableCartLines: this.selectedCartLines()
    }, undefined, undefined, () => { this.isPlacingOrder = false; });
  }

  requestWallNextStep(): void {
    this.requestProjectNextStep();
  }

  requestProjectNextStep(): void {
    if (this.isTyping || this.isGeneratingQuote || this.isPlacingOrder) return;
    const text = this.i18n.t('nextStep');
    this.pushUser(text);
    this.callApi({ text, clientIntent: 'ProjectNextStep' });
  }

  /** Un seul bouton Suivant : parcours guidé si domaine actif, sinon plus de produits. */
  requestNext(): void {
    if (this.isTyping || this.isGeneratingQuote || this.isPlacingOrder) return;
    if (this.showProjectNextStep) {
      this.requestProjectNextStep();
      return;
    }
    const text = this.i18n.t('next');
    this.pushUser(text);
    this.callApi({ text, clientIntent: 'MoreProducts' });
  }

  requestCartReview(): void {
    if (this.isTyping || this.isGeneratingQuote || this.isPlacingOrder) return;
    const text = this.i18n.t('reviewCart');
    this.pushUser(text);
    this.callApi({ text, clientIntent: 'ReviewCart' });
  }

  downloadQuotePdf(msg: StoreChatBubble): void {
    const pdf = msg.quotePdf;
    if (!pdf?.pdfBase64) return;
    this.downloadBase64Pdf(pdf.pdfBase64, pdf.fileName || 'document.pdf');
  }

  /** Blob + object URL — évite le blocage Chrome « téléchargement non sécurisé » des data: URLs. */
  private downloadBase64Pdf(base64: string, fileName: string): void {
    try {
      const binary = atob(base64);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
      }
      const blob = new Blob([bytes], { type: 'application/pdf' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.rel = 'noopener';
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      this.messages.push({
        text: this.i18n.t('error'),
        sender: 'bot',
        timestamp: new Date()
      });
    }
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
      text: this.i18n.t('newProject') + '. ' + this.i18n.t('welcome'),
      sender: 'bot',
      timestamp: new Date()
    }];
    this.activeProjectDomainLabel = null;
    this.activeProjectDomainId = null;
    this.guideComplete = false;
    this.wallGuideComplete = false;
    this.salesProjectTitle = null;
    this.salesProjectId = null;
    this.skillLevel = null;
    this.budgetMax = null;
    this.callApi({ text: this.i18n.t('newProject'), clientIntent: 'NewProject' });
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

  private setProductCartState(
    productId: string,
    state: StoreChatProductSuggestion['lineCartState']
  ): void {
    for (const msg of this.messages) {
      for (const p of msg.productSuggestions ?? []) {
        if (p.productId === productId) {
          p.lineCartState = state;
        }
      }
    }
  }

  private callApi(
    payload: {
      text?: string;
      clientIntent?: string;
      targetProductId?: string;
      targetQuantity?: number;
      tableCartLines?: { productId: string; quantity: number }[];
      interactionType?: 'text' | 'voice';
      imageBase64?: string;
      imageFileName?: string;
      imageCaption?: string;
    },
    onOk?: () => void,
    onErr?: () => void,
    onFinally?: () => void
  ): void {
    this.isTyping = true;
    this.chat.sendMessage({
      sender: 'user',
      language: this.i18n.lang,
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
          text: this.i18n.t('error'),
          sender: 'bot',
          timestamp: new Date()
        });
        onErr?.();
        onFinally?.();
      }
    });
  }

  setLanguage(lang: AssistantLang): void {
    if (this.i18n.lang === lang) return;
    this.i18n.setLang(lang);
    if (this.recognition) {
      this.recognition.lang = this.i18n.speechLocale();
    }

    // Accueil seul → le remplacer. Sinon → message bot confirmant la langue
    // (les prochaines réponses API suivront `language` envoyé à chaque tour).
    if (this.messages.length === 1 && this.messages[0].sender === 'bot') {
      this.messages[0] = {
        text: this.i18n.t('welcome'),
        sender: 'bot',
        timestamp: new Date()
      };
    } else {
      this.messages.push({
        text: this.langSwitchedMessage(lang),
        sender: 'bot',
        timestamp: new Date()
      });
      this.scrollToBottom();
    }
  }

  private langSwitchedMessage(_lang: AssistantLang): string {
    return this.i18n.t('langSwitched');
  }

  private applyBotResponse(res: StoreChatResponse): void {
    if (res.activeProjectDomainId) {
      this.activeProjectDomainId = res.activeProjectDomainId;
    }
    if (typeof res.guideComplete === 'boolean') {
      this.guideComplete = res.guideComplete;
      this.wallGuideComplete = res.guideComplete;
    } else if (typeof res.wallGuideComplete === 'boolean') {
      this.guideComplete = res.wallGuideComplete;
      this.wallGuideComplete = res.wallGuideComplete;
    }

    if (res.salesProjectTitle) {
      this.salesProjectTitle = res.salesProjectTitle;
      this.activeProjectDomainLabel = res.salesProjectTitle;
    } else if (res.activeProjectDomainLabel) {
      this.activeProjectDomainLabel = res.activeProjectDomainLabel;
    }

    if (res.salesProjectId) {
      this.salesProjectId = res.salesProjectId;
    }

    if (res.skillLevel) {
      this.skillLevel = res.skillLevel;
    }
    if (res.budgetMax != null) {
      this.budgetMax = res.budgetMax;
    }

    const products = (res.products
      ?? (res.actionType === 'PRODUCT_LIST' || res.actionType === 'PACK' || res.actionType === 'COMPARE'
        || res.actionType === 'CART_COMPLEMENTS' || res.actionType === 'SEMANTIC'
        ? (res.actionData as StoreChatProductSuggestion[])
        : null)
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
    this.recognition.lang = this.i18n.speechLocale();
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

  private clearLocalCartAfterPayment(): void {
    for (const msg of this.messages) {
      for (const p of msg.productSuggestions ?? []) {
        p.lineCartState = 'idle';
      }
    }
    this.cartPanelOpen = false;
    this.saveChatToStorage();
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
          this.clearLocalCartAfterPayment();
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
