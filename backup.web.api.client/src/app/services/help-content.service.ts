import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppI18nService } from './app-i18n.service';
import { HelpApiService, HelpContentDto } from './help-api.service';

export interface HelpRuleLine {
  id: string;
  text: string;
}

export interface HelpArticle {
  key: string;
  title: string;
  n1: string;
  n2: string[];
  rules: HelpRuleLine[];
  example: string;
  guideSteps: string[];
  version: string;
  source: 'i18n' | 'cms';
}

/** Catalogue indexé pour la recherche F1 (clés sans préfixe help.). */
export const HELP_CATALOG: string[] = [
  'sales.tabs', 'sales.quote', 'sales.order', 'sales.invoice', 'sales.customer',
  'sales.payment', 'sales.creditNote', 'sales.deliveryNote', 'sales.return',
  'sales.proforma', 'sales.deposit', 'sales.applyDeposit', 'sales.pilotage', 'sales.trash',
  'purchases.tabs', 'purchases.rfq', 'purchases.purchaseOrder', 'purchases.receipts',
  'purchases.supplierInvoice', 'purchases.supplierCreditNote', 'purchases.supplier',
  'purchases.supplierReturn', 'purchases.parsedDocuments',
  'purchases.comptabiliserFromDoc', 'purchases.newSupplierInvoice',
  'purchases.linkDocument', 'purchases.newPurchaseOrder',
  'purchases.matchOrder', 'purchases.receiveDelivery', 'purchases.comptabiliserDoc',
  'purchases.comptabiliserLot',
  'upload.tabs', 'upload.newDocument',
  'compare.tabs', 'compare.association',
  'stock.tabs', 'stock.adjust',
  'accounting.tabs', 'accounting.newEntry',
  'cash.tabs', 'cash.open', 'cash.close', 'cash.newOp',
  'erpProducts.tabs', 'erpChanges.tabs', 'createProduct',
  'admin.tabs', 'admin.tenant', 'admin.company', 'admin.roles', 'admin.user',
  'admin.resetPassword', 'admin.assign', 'admin.helpCms',
  'numbering.tabs',
  'field.sales.customer', 'field.sales.expiration', 'field.sales.invoiceDate',
  'field.sales.lineQty', 'field.sales.linePrice', 'field.sales.lineVat',
  'field.purchases.supplier', 'field.purchases.purchaseOrder', 'field.purchases.defaultVat',
  'field.upload.supplier', 'field.cash.openingBalance', 'field.stock.quantity',
  'field.accounting.account', 'field.numbering.format'
];

const FEEDBACK_KEY = 'backup_help_feedback_v1';

@Injectable({ providedIn: 'root' })
export class HelpContentService {
  private readonly centerOpenSubject = new BehaviorSubject<boolean>(false);
  readonly centerOpen$ = this.centerOpenSubject.asObservable();

  private readonly searchQuerySubject = new BehaviorSubject<string>('');
  readonly searchQuery$ = this.searchQuerySubject.asObservable();

  /** Contenu CMS publié indexé par helpKey (langue courante). */
  private cmsByKey = new Map<string, HelpContentDto>();

  constructor(
    private readonly i18n: AppI18nService,
    private readonly api: HelpApiService
  ) {
    this.i18n.lang$.subscribe(() => this.reloadPublished());
    this.reloadPublished();
  }

  reloadPublished(): void {
    this.api.getPublished(this.i18n.lang).subscribe({
      next: items => {
        this.cmsByKey.clear();
        for (const item of items || []) {
          this.cmsByKey.set(item.helpKey, item);
        }
      },
      error: () => {
        /* i18n fallback only */
      }
    });
  }

  openCenter(query = ''): void {
    this.searchQuerySubject.next(query);
    this.centerOpenSubject.next(true);
    this.track('__center__', 'center');
  }

  closeCenter(): void {
    this.centerOpenSubject.next(false);
  }

  toggleCenter(): void {
    if (this.centerOpenSubject.value) this.closeCenter();
    else this.openCenter();
  }

  setSearchQuery(q: string): void {
    this.searchQuerySubject.next(q);
  }

  resolve(helpKey: string, status?: string | null): HelpArticle {
    const cms = this.cmsByKey.get(helpKey);
    if (cms) {
      const n2 = this.splitParagraphs(cms.body || '');
      const statusText = status ? this.pick(`help.${helpKey}.status.${status}`) : '';
      if (statusText) n2.unshift(statusText);
      return {
        key: helpKey,
        title: cms.title || '',
        n1: cms.n1 || (n2[0] ? (n2[0].length > 120 ? n2[0].slice(0, 117) + '…' : n2[0]) : ''),
        n2,
        rules: this.parseRules(cms.rules || ''),
        example: cms.example || '',
        guideSteps: this.splitParagraphs(cms.guide || ''),
        version: cms.version || 'v1.0.0',
        source: 'cms'
      };
    }

    const base = `help.${helpKey}`;
    const title = this.pick(`${base}.title`);
    let n1 = this.pick(`${base}.n1`);
    const bodyRaw = this.pick(`${base}.body`) || this.pick(`${base}.n2`);
    const n2 = this.splitParagraphs(bodyRaw);

    if (!n1 && n2.length) {
      n1 = n2[0].length > 120 ? n2[0].slice(0, 117) + '…' : n2[0];
    }

    const statusKey = status ? `${base}.status.${status}` : '';
    const statusText = statusKey ? this.pick(statusKey) : '';
    if (statusText) n2.unshift(statusText);

    return {
      key: helpKey,
      title,
      n1,
      n2,
      rules: this.parseRules(this.pick(`${base}.rules`)),
      example: this.pick(`${base}.example`),
      guideSteps: this.splitParagraphs(this.pick(`${base}.guide`)),
      version: this.pick(`${base}.version`) || 'v1.0.0',
      source: 'i18n'
    };
  }

  search(query: string): HelpArticle[] {
    const q = (query || '').trim().toLowerCase();
    const keys = new Set([...HELP_CATALOG, ...this.cmsByKey.keys()]);
    const articles = [...keys].map(k => this.resolve(k));
    if (!q) return articles;
    return articles.filter(a => {
      const hay = [
        a.key, a.title, a.n1, ...a.n2, a.example,
        ...a.rules.map(r => `${r.id} ${r.text}`),
        ...a.guideSteps
      ].join(' ').toLowerCase();
      return hay.includes(q);
    });
  }

  searchGlossary(query: string): { code: string; label: string }[] {
    const codes = ['BL', 'BLC', 'BRC', 'BRF', 'DPF', 'CDF', 'AF', 'FAC', 'FF', 'HT', 'TTC', 'TVA', 'OCR'];
    const q = (query || '').trim().toLowerCase();
    return codes
      .map(code => {
        const key = `glossary.${code}`;
        const label = this.i18n.t(key);
        return { code, label: label === key ? code : label };
      })
      .filter(e => !q || e.code.toLowerCase().includes(q) || e.label.toLowerCase().includes(q));
  }

  getFeedback(helpKey: string): 'up' | 'down' | null {
    const map = this.readFeedback();
    return map[helpKey] ?? null;
  }

  setFeedback(helpKey: string, value: 'up' | 'down', reason?: string): void {
    const map = this.readFeedback();
    map[helpKey] = value;
    try {
      localStorage.setItem(FEEDBACK_KEY, JSON.stringify(map));
    } catch { /* ignore */ }
    this.api.sendFeedback(helpKey, value, reason).subscribe({ error: () => undefined });
  }

  track(helpKey: string, action: string): void {
    this.api.track(helpKey, action).subscribe({ error: () => undefined });
  }

  private pick(key: string): string {
    const v = this.i18n.t(key);
    return v === key ? '' : v;
  }

  private splitParagraphs(raw: string): string[] {
    if (!raw) return [];
    return raw.split(/\n+/).map(p => p.trim()).filter(Boolean);
  }

  private parseRules(raw: string): HelpRuleLine[] {
    if (!raw) return [];
    return raw.split(/\n+/).map(line => {
      const trimmed = line.trim();
      if (!trimmed) return null;
      const m = trimmed.match(/^(RG-[\w-]+)\s*[|:—-]\s*(.+)$/i);
      if (m) return { id: m[1].toUpperCase(), text: m[2].trim() };
      return { id: '', text: trimmed };
    }).filter((r): r is HelpRuleLine => !!r);
  }

  private readFeedback(): Record<string, 'up' | 'down'> {
    try {
      const raw = localStorage.getItem(FEEDBACK_KEY);
      if (!raw) return {};
      return JSON.parse(raw) as Record<string, 'up' | 'down'>;
    } catch {
      return {};
    }
  }
}
