import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { MaterialModule } from '../../material.module';
import { TPipe } from '../../pipes/t.pipe';
import { AppI18nService } from '../../services/app-i18n.service';
import { PermissionService } from '../../services/permission.service';
import { CompanyService } from '../../services/company.service';
import { DocumentService } from '../../services/document.service';
import { StockService } from '../../services/stock.service';
import { BusinessService } from '../../services/business.service';
import { ErpProductService } from '../../services/erp-product.service';
import { Permissions, RoutePermissions } from '../../constants/permissions';
import { Document } from '../../models/document';
import { DocumentRelation } from '../../models/relation';
import { StockForecastLine, StockForecastResult, StockItem } from '../../models/stock-item';
import {
  AccountingEntry,
  CashSession,
  PurchaseOrder,
  Receipt,
  SalesDeliveryNote,
  SalesInvoice,
  SalesOrder,
  StockMovement,
  SupplierInvoice
} from '../../models/business';
import { ErpProductsPage } from '../../models/erp-product';

interface KpiCard {
  id: string;
  labelKey: string;
  value: string | number;
  hintKey?: string;
  hintValue?: string | number;
  icon: string;
  route: string;
  warn?: boolean;
}

interface ShortcutItem {
  path: string;
  labelKey: string;
  icon: string;
}

interface ActivityRow {
  label: string;
  meta: string;
  route: string;
}

const SHORTCUTS: ShortcutItem[] = [
  { path: '/sales', labelKey: 'nav.sales', icon: 'point_of_sale' },
  { path: '/purchases', labelKey: 'nav.purchases', icon: 'shopping_cart' },
  { path: '/cash', labelKey: 'nav.cash', icon: 'receipt_long' },
  { path: '/accounting', labelKey: 'nav.accounting', icon: 'menu_book' },
  { path: '/upload', labelKey: 'nav.upload', icon: 'cloud_upload' },
  { path: '/recherche', labelKey: 'nav.search', icon: 'search' },
  { path: '/compare', labelKey: 'nav.compare', icon: 'link' },
  { path: '/stock', labelKey: 'nav.stock', icon: 'inventory_2' },
  { path: '/erp-products', labelKey: 'nav.erpProducts', icon: 'category' },
  { path: '/erp-changes', labelKey: 'nav.erpChanges', icon: 'sync_alt' },
  { path: '/numbering', labelKey: 'nav.numbering', icon: 'tag' },
  { path: '/admin', labelKey: 'nav.admin', icon: 'admin_panel_settings' },
];

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule, TPipe],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  readonly P = Permissions;
  loading = true;
  loadError = false;
  today = new Date();
  todoExpanded = true;
  activityExpanded = true;
  /** Sous-sections dépliables (contenu listes). */
  sectionOpen: Record<string, boolean> = {
    recentDocs: true,
    unlinkedBl: true,
    lowStock: true,
    forecastRisk: true,
    movements: true,
    entries: true,
    invoices: true
  };

  kpis: KpiCard[] = [];
  recentDocs: Document[] = [];
  lowStock: StockItem[] = [];
  forecastAlerts: StockForecastLine[] = [];
  unlinkedDeliveries: Document[] = [];
  recentMovements: ActivityRow[] = [];
  recentEntries: ActivityRow[] = [];
  recentInvoices: ActivityRow[] = [];

  canUpload = false;
  canPurchases = false;
  canSales = false;

  constructor(
    public i18n: AppI18nService,
    public permissions: PermissionService,
    public company: CompanyService,
    private docs: DocumentService,
    private stock: StockService,
    private biz: BusinessService,
    private products: ErpProductService,
    private router: Router
  ) {}

  get companyName(): string {
    return this.company.activeCompanyName() || '—';
  }

  get shortcuts(): ShortcutItem[] {
    return SHORTCUTS.filter(s => {
      const perms = RoutePermissions[s.path];
      if (!perms?.length) return true;
      return this.permissions.hasAny(...perms);
    });
  }

  ngOnInit(): void {
    this.canUpload = this.permissions.has(Permissions.DocumentUpload);
    this.canPurchases = this.permissions.canAccessRoute('/purchases');
    this.canSales = this.permissions.canAccessRoute('/sales');
    this.load();
  }

  refresh(): void {
    this.load();
  }

  go(route: string): void {
    void this.router.navigateByUrl(route);
  }

  goDocsList(): void {
    void this.router.navigate(['/recherche'], { queryParams: { view: 'all' } });
  }

  openDocument(doc: Document): void {
    const q = (doc.numero || doc.originalFileName || '').trim();
    if (q) {
      void this.router.navigate(['/recherche'], { queryParams: { q } });
      return;
    }
    this.goDocsList();
  }

  toggleTodo(): void {
    this.todoExpanded = !this.todoExpanded;
  }

  toggleActivity(): void {
    this.activityExpanded = !this.activityExpanded;
  }

  isSectionOpen(key: string): boolean {
    return this.sectionOpen[key] !== false;
  }

  toggleSection(key: string): void {
    this.sectionOpen[key] = !this.isSectionOpen(key);
  }

  private load(): void {
    this.loading = true;
    this.loadError = false;

    const emptyArr = <T>() => of([] as T[]);
    const emptyNull = <T>() => of(null as T | null);

    const canDocs = this.permissions.has(Permissions.DocumentRead);
    const canLink = this.permissions.has(Permissions.DocumentLink);
    const canStock = this.permissions.has(Permissions.StockRead);
    const canProducts = this.permissions.has(Permissions.ProductRead);
    const canPo = this.permissions.has(Permissions.PurchaseOrderRead);
    const canReceipt = this.permissions.has(Permissions.ReceiptRead);
    const canSupInv = this.permissions.has(Permissions.SupplierInvoiceRead);
    const canSalesInv = this.permissions.has(Permissions.InvoiceRead);
    const canOrders = this.permissions.has(Permissions.OrderRead);
    const canDn = this.permissions.has(Permissions.DeliveryNoteRead);
    const canCash = this.permissions.hasAny(Permissions.CashRead, Permissions.CashManage);
    const canAcc = this.permissions.has(Permissions.AccountingRead);
    const canMovements = canStock; // stock movements typically need stock read

    forkJoin({
      documents: canDocs ? this.docs.list().pipe(catchError(() => emptyArr<Document>())) : emptyArr<Document>(),
      relations: canLink || canDocs
        ? this.docs.relations().pipe(catchError(() => emptyArr<DocumentRelation>()))
        : emptyArr<DocumentRelation>(),
      stock: canStock ? this.stock.getAll().pipe(catchError(() => emptyArr<StockItem>())) : emptyArr<StockItem>(),
      products: canProducts
        ? this.products.getProducts({ page: 1, pageSize: 1 }).pipe(
            catchError(() => of({ total: 0, page: 1, pageSize: 1, items: [] } as ErpProductsPage))
          )
        : of({ total: 0, page: 1, pageSize: 1, items: [] } as ErpProductsPage),
      purchaseOrders: canPo
        ? this.biz.getPurchaseOrders().pipe(catchError(() => emptyArr<PurchaseOrder>()))
        : emptyArr<PurchaseOrder>(),
      receipts: canReceipt
        ? this.biz.getReceipts().pipe(catchError(() => emptyArr<Receipt>()))
        : emptyArr<Receipt>(),
      supplierInvoices: canSupInv
        ? this.biz.getSupplierInvoices().pipe(catchError(() => emptyArr<SupplierInvoice>()))
        : emptyArr<SupplierInvoice>(),
      salesInvoices: canSalesInv
        ? this.biz.getSalesInvoices().pipe(catchError(() => emptyArr<SalesInvoice>()))
        : emptyArr<SalesInvoice>(),
      salesOrders: canOrders
        ? this.biz.getSalesOrders().pipe(catchError(() => emptyArr<SalesOrder>()))
        : emptyArr<SalesOrder>(),
      deliveryNotes: canDn
        ? this.biz.getSalesDeliveryNotes().pipe(catchError(() => emptyArr<SalesDeliveryNote>()))
        : emptyArr<SalesDeliveryNote>(),
      cashSession: canCash
        ? this.biz.getActiveCashSession().pipe(catchError(() => emptyNull<CashSession>()))
        : emptyNull<CashSession>(),
      accounting: canAcc
        ? this.biz.getAccountingEntries().pipe(catchError(() => emptyArr<AccountingEntry>()))
        : emptyArr<AccountingEntry>(),
      movements: canMovements
        ? this.biz.getStockMovements().pipe(catchError(() => emptyArr<StockMovement>()))
        : emptyArr<StockMovement>(),
      forecast: canStock
        ? this.stock.getForecast().pipe(catchError(() => emptyNull<StockForecastResult>()))
        : emptyNull<StockForecastResult>()
    }).subscribe({
      next: (data) => {
        const documents = data.documents ?? [];
        const relations = data.relations ?? [];
        const stockItems = data.stock ?? [];
        const invoices = documents.filter(d => this.isInvoice(d));
        const deliveries = documents.filter(d => this.isDelivery(d));
        const linkedDeliveryIds = new Set(relations.map(r => r.deliveryId));

        const low = stockItems
          .filter(s => (s.quantityOnHand ?? 0) <= 0)
          .sort((a, b) => (a.quantityOnHand ?? 0) - (b.quantityOnHand ?? 0))
          .slice(0, 8);

        const atRisk = (data.forecast?.items ?? [])
          .filter(l => l.risk === 'Critical' || l.risk === 'Warning' || l.risk === 'Watch')
          .slice(0, 8);
        this.forecastAlerts = atRisk;
        const forecastCount = (data.forecast?.criticalCount ?? 0) + (data.forecast?.warningCount ?? 0);

        const openPos = (data.purchaseOrders ?? []).filter(po => this.isOpenStatus(po.status));

        this.kpis = [];

        if (canDocs) {
          this.kpis.push({
            id: 'docs',
            labelKey: 'dashboard.kpi.documents',
            value: documents.length,
            hintKey: 'dashboard.kpi.documentsHint',
            hintValue: `${invoices.length} / ${deliveries.length}`,
            icon: 'folder_open',
            route: '/recherche?view=all'
          });
        }

        if (canLink) {
          this.kpis.push({
            id: 'link',
            labelKey: 'dashboard.kpi.relations',
            value: relations.length,
            hintKey: 'dashboard.kpi.relationsHint',
            icon: 'link',
            route: '/compare'
          });
        }

        if (canStock) {
          const stockWarn = forecastCount > 0 || low.length > 0;
          this.kpis.push({
            id: 'stock',
            labelKey: 'dashboard.kpi.stock',
            value: stockItems.length,
            hintKey: data.forecast ? 'dashboard.kpi.stockForecastHint' : 'dashboard.kpi.stockHint',
            hintValue: data.forecast ? forecastCount : low.length,
            icon: 'inventory_2',
            route: '/stock?tab=forecast',
            warn: stockWarn
          });
        }

        if (canProducts) {
          this.kpis.push({
            id: 'products',
            labelKey: 'dashboard.kpi.products',
            value: data.products?.total ?? 0,
            icon: 'category',
            route: '/erp-products'
          });
        }

        if (canPo || canReceipt || canSupInv) {
          this.kpis.push({
            id: 'purchases',
            labelKey: 'dashboard.kpi.purchases',
            value: openPos.length,
            hintKey: 'dashboard.kpi.purchasesHint',
            hintValue: `${(data.receipts ?? []).length} / ${(data.supplierInvoices ?? []).length}`,
            icon: 'shopping_cart',
            route: '/purchases'
          });
        }

        if (canOrders || canSalesInv || canDn) {
          this.kpis.push({
            id: 'sales',
            labelKey: 'dashboard.kpi.sales',
            value: (data.salesOrders ?? []).length,
            hintKey: 'dashboard.kpi.salesHint',
            hintValue: `${(data.salesInvoices ?? []).length} / ${(data.deliveryNotes ?? []).length}`,
            icon: 'point_of_sale',
            route: '/sales'
          });
        }

        if (canCash) {
          const active = !!data.cashSession;
          this.kpis.push({
            id: 'cash',
            labelKey: 'dashboard.kpi.cash',
            value: active ? this.i18n.t('dashboard.cash.open') : this.i18n.t('dashboard.cash.closed'),
            hintKey: active ? 'dashboard.cash.activeHint' : 'dashboard.cash.inactiveHint',
            icon: 'receipt_long',
            route: '/cash',
            warn: !active
          });
        }

        if (canAcc) {
          this.kpis.push({
            id: 'accounting',
            labelKey: 'dashboard.kpi.accounting',
            value: (data.accounting ?? []).length,
            hintKey: 'dashboard.kpi.accountingHint',
            icon: 'menu_book',
            route: '/accounting'
          });
        }

        this.recentDocs = [...documents]
          .sort((a, b) => this.dateValue(b.dateAdded) - this.dateValue(a.dateAdded))
          .slice(0, 6);

        this.unlinkedDeliveries = deliveries
          .filter(d => !linkedDeliveryIds.has(d.id))
          .slice(0, 6);

        this.lowStock = low;

        this.recentMovements = (data.movements ?? [])
          .slice(0, 6)
          .map(m => ({
            label: `${m.movementType || '—'} · ${m.productKey}`,
            meta: `${m.quantity ?? 0} · ${this.formatDate(m.createdAt)}`,
            route: '/stock'
          }));

        this.recentEntries = (data.accounting ?? [])
          .slice(0, 6)
          .map(e => ({
            label: e.entryNumber || e.description || `#${e.id}`,
            meta: this.formatDate(e.entryDate || e.createdAt),
            route: '/accounting'
          }));

        this.recentInvoices = (data.salesInvoices ?? [])
          .slice(0, 6)
          .map(inv => ({
            label: inv.invoiceNumber || `#${inv.id}`,
            meta: `${inv.status || '—'} · ${this.formatDate(inv.date || inv.createdAt)}`,
            route: '/sales'
          }));

        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.loadError = true;
      }
    });
  }

  private isInvoice(doc: Document): boolean {
    const t = (doc.typeDocument || '').toLowerCase();
    return t.includes('facture') || t.includes('invoice');
  }

  private isDelivery(doc: Document): boolean {
    const t = (doc.typeDocument || '').toLowerCase();
    return (
      t === 'bonlivraison' ||
      t === 'bl' ||
      t.includes('bonlivraison') ||
      t.includes('bon de livraison') ||
      t.includes('delivery') ||
      (t.includes('bon') && t.includes('livraison'))
    );
  }

  private isOpenStatus(status?: string | null): boolean {
    const s = (status || '').toLowerCase();
    if (!s) return true;
    return !(
      s.includes('clos') ||
      s.includes('cancel') ||
      s.includes('annul') ||
      s.includes('complet') ||
      s.includes('received') ||
      s.includes('reçu') ||
      s.includes('recu') ||
      s === 'posted'
    );
  }

  private dateValue(value?: string | Date | null): number {
    if (!value) return 0;
    const t = new Date(value).getTime();
    return Number.isFinite(t) ? t : 0;
  }

  formatDate(value?: string | Date | null): string {
    if (!value) return '—';
    const d = new Date(value);
    if (!Number.isFinite(d.getTime())) return '—';
    return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
  }

  docLabel(doc: Document): string {
    return doc.numero || doc.originalFileName || `#${doc.id}`;
  }
}
