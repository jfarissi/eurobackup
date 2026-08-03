import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { BusinessService } from '../../services/business.service';
import { DocumentService } from '../../services/document.service';
import { Document } from '../../models/document';
import {
  PurchaseOrder,
  ReceiveDeliveryResult,
  Receipt,
  Supplier,
  SupplierInvoice,
  SupplierInvoicePurchaseOrderMatchResult,
  SupplierRfq,
  SupplierRfqLine,
  SupplierReturn,
  SupplierReturnLine,
  SupplierCreditNote
} from '../../models/business';
import { downloadBlob } from '../../utils/download-blob.util';
import { Observable, from, of } from 'rxjs';
import { concatMap, catchError, toArray } from 'rxjs/operators';
import { ProductLineRefComponent } from '../shared/product-line-ref/product-line-ref.component';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { DocumentRelation } from '../../models/relation';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { FieldHelpComponent } from '../shared/field-help/field-help.component';

/** Lot facture + BL(s) pour comptabilisation groupée. */
export interface ParsedDocumentGroup {
  id: string;
  kind: 'linked' | 'suggested' | 'orphan-invoice' | 'orphan-delivery';
  invoice: Document | null;
  deliveries: Document[];
  supplierName: string;
}

@Component({
  selector: 'app-purchases',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule, ProductLineRefComponent, TPipe, FormHelpComponent, FieldHelpComponent],
  templateUrl: './purchases.component.html',
  styleUrls: ['./purchases.component.css']
})
export class PurchasesComponent implements OnInit {
  /**
   * Flux ligne 1: 0 DPF, 1 CDF, 2 Réceptions, 3 Factures F, 4 AF, 5 Fournisseurs
   * Annexes ligne 2: 6 BRF, 7 Docs parsés
   */
  selectedTab = 0;
  loading = false;
  saving = false;
  searchQuery = '';
  actionMessage = '';

  suppliers: Supplier[] = [];
  purchaseOrders: PurchaseOrder[] = [];
  supplierInvoices: SupplierInvoice[] = [];
  invoiceDocuments: Document[] = [];
  deliveryDocuments: Document[] = [];
  documentRelations: DocumentRelation[] = [];
  receipts: Receipt[] = [];

  // DPF / BRF / AF (P4)
  supplierRfqs: SupplierRfq[] = [];
  supplierReturns: SupplierReturn[] = [];
  supplierCreditNotes: SupplierCreditNote[] = [];

  showRfqModal = false;
  newRfq: SupplierRfq = this.createEmptyRfq();

  showSupplierReturnModal = false;
  newSupplierReturn: SupplierReturn = this.createEmptySupplierReturn();

  rfqError = '';
  supplierReturnError = '';
  creditNoteError = '';

  showCreateFromDocumentModal = false;
  showManualInvoiceModal = false;
  showManualPurchaseOrderModal = false;
  showLinkDocumentModal = false;
  showReceiveDeliveryModal = false;
  showComptabiliserModal = false;
  selectedDocumentToComptabiliser: Document | null = null;
  /** Lot FAC + BL(s) en cours de comptabilisation. */
  selectedGroupToComptabiliser: ParsedDocumentGroup | null = null;
  selectedGroupDeliveryIds: number[] = [];
  showMatchPurchaseOrderModal = false;
  showSupplierModal = false;
  editingSupplierId: number | null = null;
  openSupplierFrom: 'invoice' | 'order' | 'document' | null = null;
  selectedSupplierId: number | null = null;
  selectedPurchaseOrderId: number | null = null;
  selectedDocumentId: number | null = null;
  defaultVatRate = 21;
  createError = '';
  linkError = '';
  selectedInvoiceToLink: SupplierInvoice | null = null;
  selectedPurchaseOrderToReceive: PurchaseOrder | null = null;
  selectedInvoiceToMatch: SupplierInvoice | null = null;
  purchaseOrderMatchPreview: SupplierInvoicePurchaseOrderMatchResult | null = null;
  highlightedSupplierInvoiceId: number | null = null;
  highlightMessage = '';

  newInvoice: SupplierInvoice = this.createEmptyInvoice();
  newPurchaseOrder: PurchaseOrder = this.createEmptyPurchaseOrder();
  newSupplier: Partial<Supplier> = this.createEmptySupplier();

  expandedRowKey: string | null = null;
  detailLoading = false;
  pdfDownloading = false;
  detailKind: 'PurchaseOrder' | 'SupplierInvoice' | 'Receipt' | null = null;
  detailPurchaseOrder: PurchaseOrder | null = null;
  detailSupplierInvoice: SupplierInvoice | null = null;
  detailReceipt: Receipt | null = null;

  readonly P = Permissions;

  constructor(
    private businessService: BusinessService,
    private documentService: DocumentService,
    private route: ActivatedRoute,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(params => {
      const supplierInvoiceId = Number(params.get('supplierInvoiceId') || 0);
      const autoCreated = params.get('autoCreated') === '1';

      this.highlightedSupplierInvoiceId = supplierInvoiceId > 0 ? supplierInvoiceId : null;
      this.highlightMessage = autoCreated && this.highlightedSupplierInvoiceId
        ? this.i18n.t('purchases.autoCreated', { id: this.highlightedSupplierInvoiceId })
        : '';

      if (this.highlightedSupplierInvoiceId) {
        this.selectedTab = 3;
      }
    });

    this.loadAllData();
  }

  get createButtonLabel(): string {
    switch (this.selectedTab) {
      case 1: return this.i18n.t('purchases.btn.newOrder');
      case 5: return this.i18n.t('purchases.btn.newSupplier');
      default: return this.i18n.t('purchases.btn.newInvoice');
    }
  }

  /** Aide F1 / panneau ? selon l’onglet actif. */
  get activeTabHelpKey(): string {
    const keys = [
      'purchases.rfq',
      'purchases.purchaseOrder',
      'purchases.receipts',
      'purchases.supplierInvoice',
      'purchases.supplierCreditNote',
      'purchases.supplier',
      'purchases.supplierReturn',
      'purchases.parsedDocuments'
    ];
    return keys[this.selectedTab] || 'purchases.tabs';
  }

  get activeTabHelpAbbrs(): string[] {
    switch (this.selectedTab) {
      case 0: return ['DPF', 'CDF'];
      case 1: return ['CDF', 'BL', 'HT', 'TVA'];
      case 2: return ['BL', 'CDF'];
      case 3: return ['FF', 'FAC', 'HT', 'TVA', 'OCR'];
      case 4: return ['AF', 'FF', 'BRF'];
      case 6: return ['BRF', 'AF', 'BL'];
      case 7: return ['OCR', 'BL', 'FAC', 'FF'];
      default: return ['DPF', 'CDF', 'BL', 'BRF', 'AF', 'FAC', 'FF', 'OCR'];
    }
  }

  get createButtonIcon(): string {
    switch (this.selectedTab) {
      case 1: return 'shopping_cart';
      case 5: return 'person_add';
      default: return 'add';
    }
  }

  get showCreateButton(): boolean {
    if (this.selectedTab === 2 || this.selectedTab === 7) return false;
    switch (this.selectedTab) {
      case 3: return this.perm.has(Permissions.SupplierInvoiceCreate);
      case 1: return this.perm.has(Permissions.PurchaseOrderCreate);
      case 5: return this.perm.has(Permissions.SupplierCreate);
      default: return false;
    }
  }

  onCreateClick(): void {
    if (this.selectedTab === 1) {
      this.openManualPurchaseOrderModal();
      return;
    }
    if (this.selectedTab === 5) {
      this.openSupplierModal();
      return;
    }
    this.openManualInvoiceModal();
  }

  loadAllData(): void {
    this.loading = true;

    this.businessService.getSuppliers().subscribe(suppliers => {
      this.suppliers = suppliers;
    });

    this.businessService.getPurchaseOrders().subscribe(orders => {
      this.purchaseOrders = orders;
    });

    this.businessService.getSupplierInvoices().subscribe(invoices => {
      this.supplierInvoices = this.sortSupplierInvoices(invoices);
      this.loading = false;
    });

    this.documentService.list('Facture').subscribe(documents => {
      this.invoiceDocuments = [...documents]
        .sort((a, b) => new Date(b.dateAdded).getTime() - new Date(a.dateAdded).getTime());
      this.loadDocumentRelations();
    });

    this.documentService.list('BonLivraison').subscribe(documents => {
      this.deliveryDocuments = [...documents]
        .sort((a, b) => new Date(b.dateAdded).getTime() - new Date(a.dateAdded).getTime());
      this.loadDocumentRelations();
    });

    this.businessService.getReceipts().subscribe(receipts => {
      this.receipts = receipts;
    });

    this.loadDocumentRelations();
    this.loadSupplierRfqs();
    this.loadSupplierReturns();
    this.loadSupplierCreditNotes();
  }

  /** Ferme le popup et recharge le formulaire principal. */
  private finishModalSuccess(close: () => void, message?: string): void {
    close();
    if (message) this.actionMessage = message;
    this.loadAllData();
  }

  loadDocumentRelations(): void {
    this.documentService.relations().subscribe({
      next: (rels) => this.documentRelations = rels || [],
      error: () => this.documentRelations = []
    });
  }

  loadSupplierRfqs(): void {
    if (!this.perm.has(Permissions.PurchaseOrderRead)) {
      this.supplierRfqs = [];
      return;
    }
    this.businessService.getSupplierRfqs().subscribe({
      next: (r) => this.supplierRfqs = r,
      error: () => this.supplierRfqs = []
    });
  }

  loadSupplierReturns(): void {
    if (!this.perm.has(Permissions.PurchaseOrderRead)) {
      this.supplierReturns = [];
      return;
    }
    this.businessService.getSupplierReturns().subscribe({
      next: (r) => this.supplierReturns = r,
      error: () => this.supplierReturns = []
    });
  }

  loadSupplierCreditNotes(): void {
    if (!this.perm.has(Permissions.SupplierCreditNoteRead)) {
      this.supplierCreditNotes = [];
      return;
    }
    this.businessService.getSupplierCreditNotes().subscribe({
      next: (c) => this.supplierCreditNotes = c,
      error: () => this.supplierCreditNotes = []
    });
  }

  onSearch(): void {
    if (this.selectedTab === 0) {
      if (this.searchQuery) {
        this.businessService.getSupplierRfqs(this.searchQuery).subscribe(res => this.supplierRfqs = res);
      } else {
        this.loadSupplierRfqs();
      }
      return;
    }

    if (this.selectedTab === 1) {
      this.businessService.getPurchaseOrders(this.searchQuery || undefined).subscribe(res => this.purchaseOrders = res);
      return;
    }

    if (this.selectedTab === 2) {
      this.businessService.getReceipts(this.searchQuery || undefined).subscribe(res => this.receipts = res);
      return;
    }

    if (this.selectedTab === 3) {
      this.businessService.getSupplierInvoices(this.searchQuery || undefined).subscribe(res => {
        this.supplierInvoices = this.sortSupplierInvoices(res);
      });
      return;
    }

    if (this.selectedTab === 4) {
      if (this.searchQuery) {
        this.businessService.getSupplierCreditNotes(this.searchQuery).subscribe(res => this.supplierCreditNotes = res);
      } else {
        this.loadSupplierCreditNotes();
      }
      return;
    }

    if (this.selectedTab === 5) {
      this.businessService.getSuppliers(this.searchQuery || undefined).subscribe(res => this.suppliers = res);
      return;
    }

    if (this.selectedTab === 6) {
      if (this.searchQuery) {
        this.businessService.getSupplierReturns(this.searchQuery).subscribe(res => this.supplierReturns = res);
      } else {
        this.loadSupplierReturns();
      }
      return;
    }

    if (this.selectedTab === 7) {
      const q = (this.searchQuery || '').trim().toLowerCase();
      const filterDoc = (d: Document) => {
        if (!q) return true;
        return (
          (d.numero || '').toLowerCase().includes(q) ||
          (d.supplier || '').toLowerCase().includes(q) ||
          (d.originalFileName || '').toLowerCase().includes(q) ||
          (d.typeDocument || '').toLowerCase().includes(q) ||
          String(d.id).includes(q)
        );
      };
      this.documentService.list('Facture').subscribe(documents => {
        this.invoiceDocuments = documents
          .filter(filterDoc)
          .sort((a, b) => new Date(b.dateAdded).getTime() - new Date(a.dateAdded).getTime());
      });
      this.documentService.list('BonLivraison').subscribe(documents => {
        this.deliveryDocuments = documents
          .filter(filterDoc)
          .sort((a, b) => new Date(b.dateAdded).getTime() - new Date(a.dateAdded).getTime());
      });
      this.loadDocumentRelations();
    }
  }

  openCreateFromDocumentModal(): void {
    this.showCreateFromDocumentModal = true;
    this.selectedSupplierId = null;
    this.selectedDocumentId = null;
    this.defaultVatRate = 21;
    this.createError = '';
  }

  openManualInvoiceModal(): void {
    if (this.suppliers.length === 0) {
      this.createError = '';
      this.actionMessage = '';
      this.highlightMessage = this.i18n.t('purchases.needSupplierFirst');
      this.openSupplierModal('invoice');
      return;
    }
    this.showManualInvoiceModal = true;
    this.createError = '';
    this.newInvoice = this.createEmptyInvoice();
  }

  openManualPurchaseOrderModal(): void {
    if (this.suppliers.length === 0) {
      this.createError = '';
      this.actionMessage = '';
      this.highlightMessage = this.i18n.t('purchases.needSupplierFirst');
      this.openSupplierModal('order');
      return;
    }
    this.showManualPurchaseOrderModal = true;
    this.createError = '';
    this.newPurchaseOrder = this.createEmptyPurchaseOrder();
  }

  downloadPdfFromList(kind: 'PurchaseOrder' | 'SupplierInvoice', id?: number, fileName?: string): void {
    if (!id) return;
    this.downloadPdf(kind, id, fileName || 'document.pdf');
  }

  downloadCurrentDetailPdf(): void {
    if (this.detailKind === 'PurchaseOrder' && this.detailPurchaseOrder?.id) {
      this.downloadPdf('PurchaseOrder', this.detailPurchaseOrder.id, `${this.detailPurchaseOrder.orderNumber || 'commande'}.pdf`);
    } else if (this.detailKind === 'SupplierInvoice' && this.detailSupplierInvoice?.id) {
      this.downloadPdf('SupplierInvoice', this.detailSupplierInvoice.id, `${this.detailSupplierInvoice.invoiceNumber || 'facture'}.pdf`);
    }
  }

  private downloadPdf(kind: 'PurchaseOrder' | 'SupplierInvoice', id: number, fileName: string): void {
    this.pdfDownloading = true;
    this.createError = '';
    const request: Observable<Blob> = kind === 'PurchaseOrder'
      ? this.businessService.downloadPurchaseOrderPdf(id)
      : this.businessService.downloadSupplierInvoicePdf(id);

    request.subscribe({
      next: (blob) => {
        downloadBlob(blob, fileName);
        this.pdfDownloading = false;
        this.actionMessage = this.i18n.t('purchases.pdfDownloaded', { fileName });
      },
      error: () => {
        this.pdfDownloading = false;
        this.createError = this.i18n.t('purchases.pdfError');
        this.actionMessage = '';
      }
    });
  }

  openSupplierModal(from: 'invoice' | 'order' | 'document' | null = null): void {
    this.openSupplierFrom = from;
    this.editingSupplierId = null;
    this.newSupplier = this.createEmptySupplier();
    this.createError = '';
    this.showSupplierModal = true;
  }

  openEditSupplierModal(supplier: Supplier): void {
    if (!supplier.id) return;
    this.openSupplierFrom = null;
    this.editingSupplierId = supplier.id;
    this.newSupplier = {
      supplierCode: supplier.supplierCode,
      name: supplier.name,
      vatNumber: supplier.vatNumber || '',
      address: supplier.address || '',
      city: supplier.city || '',
      postalCode: supplier.postalCode || '',
      country: supplier.country || 'BE',
      email: supplier.email || '',
      phone: supplier.phone || ''
    };
    this.createError = '';
    this.showSupplierModal = true;
  }

  deleteSupplier(supplier: Supplier): void {
    if (!supplier.id) return;
    if (!confirm(this.i18n.t('purchases.confirm.deleteSupplier', { name: supplier.name }))) return;
    this.createError = '';
    this.businessService.deleteSupplier(supplier.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('purchases.supplierDeleted', { name: supplier.name });
        this.loadAllData();
      },
      error: (error) => {
        this.createError = error?.error?.error || error?.error || this.i18n.t('purchases.supplierDeleteError');
      }
    });
  }

  openLinkDocumentModal(invoice: SupplierInvoice): void {
    this.showLinkDocumentModal = true;
    this.selectedInvoiceToLink = invoice;
    this.selectedDocumentId = null;
    this.linkError = '';
  }

  openReceiveDeliveryModal(order: PurchaseOrder): void {
    this.showReceiveDeliveryModal = true;
    this.selectedPurchaseOrderToReceive = order;
    this.selectedDocumentId = null;
    this.linkError = '';
  }

  openComptabiliserModal(doc: Document): void {
    this.selectedGroupToComptabiliser = null;
    this.selectedGroupDeliveryIds = [];
    this.showComptabiliserModal = true;
    this.selectedDocumentToComptabiliser = doc;
    this.selectedPurchaseOrderId = null;
    this.selectedSupplierId = this.resolveSupplierIdForDocument(doc);
    this.linkError = '';
  }

  openComptabiliserGroupModal(group: ParsedDocumentGroup): void {
    this.selectedDocumentToComptabiliser = null;
    this.selectedGroupToComptabiliser = group;
    this.selectedGroupDeliveryIds = group.deliveries
      .filter(d => d.id && !this.isDocumentComptabilise(d))
      .map(d => d.id!);
    this.showComptabiliserModal = true;
    this.selectedPurchaseOrderId = null;
    const seed = group.invoice || group.deliveries[0] || null;
    this.selectedSupplierId = seed ? this.resolveSupplierIdForDocument(seed) : null;
    this.linkError = '';
  }

  toggleGroupDelivery(deliveryId: number, checked: boolean): void {
    if (checked) {
      if (!this.selectedGroupDeliveryIds.includes(deliveryId)) {
        this.selectedGroupDeliveryIds = [...this.selectedGroupDeliveryIds, deliveryId];
      }
      return;
    }
    this.selectedGroupDeliveryIds = this.selectedGroupDeliveryIds.filter(id => id !== deliveryId);
  }

  isGroupDeliverySelected(deliveryId: number): boolean {
    return this.selectedGroupDeliveryIds.includes(deliveryId);
  }

  comptabiliserDocument(): void {
    if (this.selectedGroupToComptabiliser) {
      this.comptabiliserGroup();
      return;
    }

    if (!this.selectedDocumentToComptabiliser?.id) {
      this.linkError = this.i18n.t('purchases.documentMissing');
      return;
    }

    if (!this.selectedSupplierId) {
      this.linkError = this.i18n.t('purchases.selectSupplierError');
      return;
    }

    this.linkError = '';
    this.saving = true;

    if (this.isInvoiceDocument(this.selectedDocumentToComptabiliser)) {
      this.businessService.comptabiliserSupplierInvoice({
        documentId: this.selectedDocumentToComptabiliser.id,
        supplierId: this.selectedSupplierId,
        purchaseOrderId: this.selectedPurchaseOrderId || undefined,
        defaultVatRate: this.defaultVatRate
      }).subscribe({
        next: (result) => {
          this.saving = false;
          this.showComptabiliserModal = false;
          this.selectedDocumentToComptabiliser = null;
          const warnings = result.warnings?.length ? ` ${result.warnings.join(' ')}` : '';
          this.highlightMessage = this.i18n.t('purchases.invoiceComptabilised', {
            number: result.invoice.invoiceNumber,
            warnings
          });
          this.actionMessage = this.highlightMessage;
          this.selectedTab = 3;
          this.loadAllData();
        },
        error: (error) => {
          this.saving = false;
          this.linkError = error?.error?.error || error?.error || this.i18n.t('purchases.comptabiliseError');
        }
      });
      return;
    }

    this.businessService.comptabiliserDeliveryNote({
      documentId: this.selectedDocumentToComptabiliser.id,
      supplierId: this.selectedSupplierId,
      purchaseOrderId: this.selectedPurchaseOrderId || undefined,
      updateStock: true,
      defaultVatRate: this.defaultVatRate
    }).subscribe({
      next: (result) => {
        this.saving = false;
        this.showComptabiliserModal = false;
        this.selectedDocumentToComptabiliser = null;
        this.highlightMessage = this.buildComptabiliserMessage(result);
        this.actionMessage = this.highlightMessage;
        this.selectedTab = 2;
        this.loadAllData();
      },
      error: (error) => {
        this.saving = false;
        this.linkError = error?.error?.error || error?.error || this.i18n.t('purchases.comptabiliseError');
      }
    });
  }

  private comptabiliserGroup(): void {
    const group = this.selectedGroupToComptabiliser;
    if (!group) return;

    if (!this.selectedSupplierId) {
      this.linkError = this.i18n.t('purchases.selectSupplierError');
      return;
    }

    const deliveryIds = this.selectedGroupDeliveryIds.filter(id => {
      const doc = group.deliveries.find(d => d.id === id);
      return !!doc && !this.isDocumentComptabilise(doc);
    });
    const invoicePending = group.invoice && !this.isDocumentComptabilise(group.invoice)
      ? group.invoice
      : null;

    if (!invoicePending && deliveryIds.length === 0) {
      this.linkError = this.i18n.t('purchases.group.nothingToPost');
      return;
    }

    this.linkError = '';
    this.saving = true;
    const supplierId = this.selectedSupplierId;
    const purchaseOrderId = this.selectedPurchaseOrderId || undefined;
    const messages: string[] = [];

    const steps: Array<() => Observable<string>> = [];

    for (const deliveryId of deliveryIds) {
      steps.push(() =>
        this.businessService.comptabiliserDeliveryNote({
          documentId: deliveryId,
          supplierId,
          purchaseOrderId,
          updateStock: true,
          defaultVatRate: this.defaultVatRate
        }).pipe(
          concatMap((result) => {
            const msg = this.buildComptabiliserMessage(result);
            // Auto-lier si suggestion et facture connue
            if (group.kind === 'suggested' && group.invoice?.id && deliveryId) {
              return this.documentService.link(group.invoice.id, deliveryId).pipe(
                concatMap(() => of(msg)),
                catchError(() => of(msg))
              );
            }
            return of(msg);
          })
        )
      );
    }

    if (invoicePending?.id) {
      steps.push(() =>
        this.businessService.comptabiliserSupplierInvoice({
          documentId: invoicePending.id!,
          supplierId,
          purchaseOrderId,
          defaultVatRate: this.defaultVatRate
        }).pipe(
          concatMap((result) => {
            const warnings = result.warnings?.length ? ` ${result.warnings.join(' ')}` : '';
            return of(this.i18n.t('purchases.invoiceComptabilised', {
              number: result.invoice.invoiceNumber,
              warnings
            }));
          })
        )
      );
    }

    from(steps).pipe(
      concatMap(step => step().pipe(
        catchError((error) => {
          const msg = error?.error?.error || error?.error || this.i18n.t('purchases.comptabiliseError');
          throw new Error(typeof msg === 'string' ? msg : this.i18n.t('purchases.comptabiliseError'));
        })
      )),
      toArray()
    ).subscribe({
      next: (parts) => {
        messages.push(...parts);
        this.saving = false;
        this.showComptabiliserModal = false;
        this.selectedGroupToComptabiliser = null;
        this.selectedGroupDeliveryIds = [];
        this.highlightMessage = messages.join(' · ');
        this.actionMessage = this.highlightMessage;
        this.selectedTab = invoicePending ? 3 : 2;
        this.loadAllData();
      },
      error: (error: Error) => {
        this.saving = false;
        this.linkError = error?.message || this.i18n.t('purchases.comptabiliseError');
        this.loadAllData();
      }
    });
  }

  buildComptabiliserMessage(result: { receipt: Receipt; stockUpdated: boolean; stockAlreadyApplied: boolean; stockMovementCount: number; stockQuantityIn: number; warnings: string[] }): string {
    const parts = [
      this.i18n.t('purchases.blComptabilised', { number: result.receipt.receiptNumber })
    ];
    if (result.stockUpdated) {
      parts.push(this.i18n.t('purchases.stockUpdated', { qty: result.stockQuantityIn, count: result.stockMovementCount }));
    } else if (result.stockAlreadyApplied) {
      parts.push(this.i18n.t('purchases.stockAlreadyFed'));
    }
    if (result.warnings?.length) {
      parts.push(result.warnings.join(' '));
    }
    return parts.join(' ');
  }

  resolveSupplierIdForDocument(doc: Document): number | null {
    const name = (doc.supplier || '').trim().toLowerCase();
    if (!name) return null;
    return this.suppliers.find(s => s.name.trim().toLowerCase() === name)?.id ?? null;
  }

  matchingOrdersForSelectedDocument(): PurchaseOrder[] {
    const supplierId = this.selectedSupplierId;
    if (supplierId) {
      return this.purchaseOrders.filter(order => order.supplierId === supplierId);
    }
    const seed = this.selectedDocumentToComptabiliser
      || this.selectedGroupToComptabiliser?.invoice
      || this.selectedGroupToComptabiliser?.deliveries[0]
      || null;
    const supplierName = (seed?.supplier || '').trim().toLowerCase();
    if (!supplierName) return this.purchaseOrders;
    return this.purchaseOrders.filter(order =>
      (order.supplier?.name || '').trim().toLowerCase() === supplierName
    );
  }

  parsedDocuments(): Document[] {
    return [...this.invoiceDocuments, ...this.deliveryDocuments]
      .sort((a, b) => new Date(b.dateAdded).getTime() - new Date(a.dateAdded).getTime());
  }

  /** Lots FAC + BL(s) : relations officielles, suggestions fournisseur, orphelins. */
  parsedDocumentGroups(): ParsedDocumentGroup[] {
    const q = (this.searchQuery || '').trim().toLowerCase();
    const matchesSearch = (doc: Document) => {
      if (!q) return true;
      return (
        (doc.numero || '').toLowerCase().includes(q) ||
        (doc.supplier || '').toLowerCase().includes(q) ||
        (doc.originalFileName || '').toLowerCase().includes(q) ||
        (doc.typeDocument || '').toLowerCase().includes(q) ||
        String(doc.id).includes(q)
      );
    };

    const invoices = this.invoiceDocuments.filter(matchesSearch);
    const deliveries = this.deliveryDocuments.filter(matchesSearch);
    const invoiceById = new Map(invoices.map(i => [i.id!, i]));
    const deliveryById = new Map(deliveries.map(d => [d.id!, d]));

    const usedInvoiceIds = new Set<number>();
    const usedDeliveryIds = new Set<number>();
    const groups: ParsedDocumentGroup[] = [];

    // 1) Relations explicites Compare
    const byInvoice = new Map<number, number[]>();
    for (const rel of this.documentRelations) {
      if (!invoiceById.has(rel.invoiceId) && !this.invoiceDocuments.some(i => i.id === rel.invoiceId)) continue;
      const list = byInvoice.get(rel.invoiceId) ?? [];
      list.push(rel.deliveryId);
      byInvoice.set(rel.invoiceId, list);
    }

    for (const [invoiceId, deliveryIds] of byInvoice) {
      const invoice = invoiceById.get(invoiceId)
        || this.invoiceDocuments.find(i => i.id === invoiceId)
        || null;
      if (!invoice) continue;
      const linkedDeliveries = deliveryIds
        .map(id => deliveryById.get(id) || this.deliveryDocuments.find(d => d.id === id))
        .filter((d): d is Document => !!d);
      if (!matchesSearch(invoice) && !linkedDeliveries.some(matchesSearch)) continue;

      usedInvoiceIds.add(invoiceId);
      linkedDeliveries.forEach(d => { if (d.id) usedDeliveryIds.add(d.id); });
      groups.push({
        id: `linked-${invoiceId}`,
        kind: 'linked',
        invoice,
        deliveries: linkedDeliveries,
        supplierName: invoice.supplier || linkedDeliveries[0]?.supplier || ''
      });
    }

    // 2) Suggestions : même fournisseur, docs encore libres
    const freeInvoices = invoices.filter(i => i.id && !usedInvoiceIds.has(i.id));
    const freeDeliveries = deliveries.filter(d => d.id && !usedDeliveryIds.has(d.id));

    for (const invoice of freeInvoices) {
      const supplierKey = this.normalizeSupplierKey(invoice.supplier);
      if (!supplierKey) continue;
      const candidates = freeDeliveries.filter(d =>
        d.id && !usedDeliveryIds.has(d.id) && this.normalizeSupplierKey(d.supplier) === supplierKey
      );
      if (candidates.length === 0) continue;

      usedInvoiceIds.add(invoice.id!);
      candidates.forEach(d => { if (d.id) usedDeliveryIds.add(d.id); });
      groups.push({
        id: `suggested-${invoice.id}`,
        kind: 'suggested',
        invoice,
        deliveries: candidates,
        supplierName: invoice.supplier || candidates[0]?.supplier || ''
      });
    }

    // 3) Orphelins
    for (const invoice of invoices) {
      if (!invoice.id || usedInvoiceIds.has(invoice.id)) continue;
      groups.push({
        id: `orphan-inv-${invoice.id}`,
        kind: 'orphan-invoice',
        invoice,
        deliveries: [],
        supplierName: invoice.supplier || ''
      });
    }
    for (const delivery of deliveries) {
      if (!delivery.id || usedDeliveryIds.has(delivery.id)) continue;
      groups.push({
        id: `orphan-bl-${delivery.id}`,
        kind: 'orphan-delivery',
        invoice: null,
        deliveries: [delivery],
        supplierName: delivery.supplier || ''
      });
    }

    return groups.sort((a, b) => {
      const da = this.groupSortDate(a);
      const db = this.groupSortDate(b);
      return db - da;
    });
  }

  private normalizeSupplierKey(name?: string | null): string {
    return (name || '').trim().toLowerCase().replace(/\s+/g, ' ');
  }

  private groupSortDate(group: ParsedDocumentGroup): number {
    const dates = [
      group.invoice?.dateAdded,
      ...group.deliveries.map(d => d.dateAdded)
    ].filter(Boolean) as string[];
    return Math.max(0, ...dates.map(d => new Date(d).getTime()));
  }

  groupPendingCount(group: ParsedDocumentGroup): number {
    let n = 0;
    if (group.invoice && !this.isDocumentComptabilise(group.invoice)) n++;
    n += group.deliveries.filter(d => !this.isDocumentComptabilise(d)).length;
    return n;
  }

  isGroupFullyComptabilise(group: ParsedDocumentGroup): boolean {
    return this.groupPendingCount(group) === 0;
  }

  groupKindLabel(group: ParsedDocumentGroup): string {
    switch (group.kind) {
      case 'linked': return this.i18n.t('purchases.group.kind.linked');
      case 'suggested': return this.i18n.t('purchases.group.kind.suggested');
      case 'orphan-invoice': return this.i18n.t('purchases.group.kind.orphanInvoice');
      default: return this.i18n.t('purchases.group.kind.orphanDelivery');
    }
  }

  isInvoiceDocument(doc: Document): boolean {
    const type = (doc.typeDocument || '').trim().toLowerCase();
    return type === 'facture' || type === 'invoice' || type === 'fa' || type.includes('facture') || type.includes('invoice');
  }

  isDeliveryDocument(doc: Document): boolean {
    const type = (doc.typeDocument || '').trim().toLowerCase();
    return type === 'bonlivraison' || type === 'bl' ||
      type.includes('bonlivraison') || type.includes('bon de livraison') ||
      type.includes('delivery note') || (type.includes('bon') && type.includes('livraison'));
  }

  supplierInvoiceForDocument(doc: Document): SupplierInvoice | null {
    if (!doc.id) return null;
    return this.supplierInvoices.find(i => i.documentId === doc.id) || null;
  }

  isDocumentComptabilise(doc: Document): boolean {
    if (this.isInvoiceDocument(doc)) return !!this.supplierInvoiceForDocument(doc);
    if (this.isDeliveryDocument(doc)) return this.isDeliveryComptabilise(doc);
    return false;
  }

  comptabilisationTargetLabel(doc: Document): string {
    if (this.isInvoiceDocument(doc)) {
      const invoice = this.supplierInvoiceForDocument(doc);
      return invoice ? invoice.invoiceNumber : '-';
    }
    const receipt = this.receiptForDocument(doc);
    return receipt ? receipt.receiptNumber : '-';
  }

  comptabilisationTargetKind(doc: Document): string {
    return this.isInvoiceDocument(doc) ? this.i18n.t('purchases.target.invoiceFo') : this.i18n.t('purchases.target.receipt');
  }

  receiptForDocument(doc: Document): Receipt | null {
    if (!doc.id) return null;
    return this.receipts.find(r => r.documentId === doc.id) || null;
  }

  isDeliveryComptabilise(doc: Document): boolean {
    return !!this.receiptForDocument(doc);
  }

  /** @deprecated Prefer isDeliveryComptabilise — kept for CFA receive flow */
  linkedPurchaseOrderForDelivery(doc: Document): PurchaseOrder | null {
    const marker = `Received from delivery #${doc.id}`;
    return this.purchaseOrders.find(order =>
      (order.notes || '').includes(marker)
    ) || null;
  }

  isDeliveryApplied(doc: Document): boolean {
    return this.isDeliveryComptabilise(doc) || !!this.linkedPurchaseOrderForDelivery(doc);
  }

  openMatchPurchaseOrderModal(invoice: SupplierInvoice): void {
    this.showMatchPurchaseOrderModal = true;
    this.selectedInvoiceToMatch = invoice;
    this.selectedPurchaseOrderId = null;
    this.purchaseOrderMatchPreview = null;
    this.linkError = '';
  }

  createSupplierInvoiceFromDocument(): void {
    if (!this.selectedSupplierId || !this.selectedDocumentId) {
      this.createError = this.i18n.t('purchases.selectSupplierAndDoc');
      return;
    }

    this.createError = '';
    this.saving = true;
    this.businessService
      .comptabiliserSupplierInvoice({
        documentId: this.selectedDocumentId,
        supplierId: this.selectedSupplierId,
        defaultVatRate: this.defaultVatRate
      })
      .subscribe({
        next: (result) => {
          this.saving = false;
          this.showCreateFromDocumentModal = false;
          this.selectedTab = 3;
          this.actionMessage = this.i18n.t('purchases.invoiceFromDoc', { number: result.invoice.invoiceNumber });
          this.loadAllData();
        },
        error: (error) => {
          this.saving = false;
          this.createError = error?.error?.error || error?.error || this.i18n.t('purchases.comptabiliseError');
        }
      });
  }

  saveManualInvoice(): void {
    if (!this.newInvoice.supplierId) {
      this.createError = this.i18n.t('purchases.selectSupplierError');
      return;
    }

    if (!this.newInvoice.lines.length || this.newInvoice.lines.every(l => !l.description && !l.productKey)) {
      this.createError = this.i18n.t('purchases.addLineError');
      return;
    }

    this.newInvoice.lines.forEach(line => this.calculateInvoiceLine(line));
    this.createError = '';
    this.saving = true;
    this.businessService.createSupplierInvoice(this.newInvoice).subscribe({
      next: (invoice) => {
        this.saving = false;
        this.showManualInvoiceModal = false;
        this.selectedTab = 3;
        this.actionMessage = this.i18n.t('purchases.supplierInvoiceCreated', { number: invoice.invoiceNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.saving = false;
        this.createError = error?.error?.error || this.i18n.t('purchases.supplierInvoiceCreateError');
      }
    });
  }

  saveManualPurchaseOrder(): void {
    if (!this.newPurchaseOrder.supplierId) {
      this.createError = this.i18n.t('purchases.selectSupplierError');
      return;
    }

    if (!this.newPurchaseOrder.lines.length || this.newPurchaseOrder.lines.every(l => !l.description && !l.productKey)) {
      this.createError = this.i18n.t('purchases.addLineError');
      return;
    }

    this.newPurchaseOrder.lines.forEach(line => this.calculatePurchaseOrderLine(line));
    this.createError = '';
    this.saving = true;
    const payload: PurchaseOrder = {
      ...this.newPurchaseOrder,
      expectedDeliveryDate: this.newPurchaseOrder.expectedDeliveryDate || undefined
    };
    this.businessService.createPurchaseOrder(payload).subscribe({
      next: (order) => {
        this.saving = false;
        this.showManualPurchaseOrderModal = false;
        this.selectedTab = 1;
        this.actionMessage = this.i18n.t('purchases.purchaseOrderCreated', { number: order.orderNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.saving = false;
        this.createError = error?.error?.error || this.i18n.t('purchases.purchaseOrderCreateError');
      }
    });
  }

  saveSupplier(): void {
    if (!this.newSupplier.name?.trim()) {
      this.createError = this.i18n.t('purchases.supplierNameRequired');
      return;
    }

    this.createError = '';
    this.saving = true;
    const payload: Supplier = {
      supplierCode: this.newSupplier.supplierCode?.trim() || '',
      name: this.newSupplier.name.trim(),
      vatNumber: this.newSupplier.vatNumber || undefined,
      address: this.newSupplier.address || undefined,
      city: this.newSupplier.city || undefined,
      postalCode: this.newSupplier.postalCode || undefined,
      country: this.newSupplier.country || 'BE',
      email: this.newSupplier.email || undefined,
      phone: this.newSupplier.phone || undefined
    };

    const request = this.editingSupplierId
      ? this.businessService.updateSupplier(this.editingSupplierId, { ...payload, id: this.editingSupplierId })
      : this.businessService.createSupplier(payload);

    request.subscribe({
      next: (saved) => {
        this.saving = false;
        this.showSupplierModal = false;
        const wasEdit = !!this.editingSupplierId;
        const from = this.openSupplierFrom;
        const verb = wasEdit ? this.i18n.t('common.updated') : this.i18n.t('common.created');
        this.actionMessage = this.i18n.t('purchases.supplierSaved', { name: saved.name, code: saved.supplierCode, verb });
        this.editingSupplierId = null;
        this.openSupplierFrom = null;
        this.loadAllData();
        this.businessService.getSuppliers().subscribe(suppliers => {
          this.suppliers = suppliers;
          if (!wasEdit && saved.id) {
            if (from === 'invoice') {
              this.newInvoice.supplierId = saved.id;
              this.showManualInvoiceModal = true;
            } else if (from === 'order') {
              this.newPurchaseOrder.supplierId = saved.id;
              this.showManualPurchaseOrderModal = true;
            } else if (from === 'document') {
              this.selectedSupplierId = saved.id;
              this.showCreateFromDocumentModal = true;
            } else {
              this.selectedTab = 5;
            }
          }
        });
      },
      error: (error) => {
        this.saving = false;
        this.createError = error?.error?.error || this.i18n.t('purchases.supplierSaveError', {
          action: this.editingSupplierId ? this.i18n.t('purchases.supplierUpdateAction') : this.i18n.t('purchases.supplierCreateAction')
        });
      }
    });
  }

  linkDocumentToInvoice(): void {
    if (!this.selectedInvoiceToLink?.id || !this.selectedDocumentId) {
      this.linkError = this.i18n.t('purchases.selectDocumentError');
      return;
    }

    this.linkError = '';
    this.businessService.linkDocumentToSupplierInvoice(this.selectedInvoiceToLink.id, this.selectedDocumentId).subscribe({
      next: () => {
        this.showLinkDocumentModal = false;
        this.selectedInvoiceToLink = null;
        this.actionMessage = this.i18n.t('purchases.documentLinked');
        this.loadAllData();
      },
      error: (error) => {
        this.linkError = error?.error?.error || this.i18n.t('purchases.linkDocumentError');
      }
    });
  }

  receivePurchaseOrderFromDelivery(): void {
    if (!this.selectedPurchaseOrderToReceive?.id || !this.selectedDocumentId) {
      this.linkError = this.i18n.t('purchases.selectDeliveryError');
      return;
    }

    this.linkError = '';
    this.businessService.receivePurchaseOrderFromDelivery(this.selectedPurchaseOrderToReceive.id, this.selectedDocumentId).subscribe({
      next: (result: ReceiveDeliveryResult) => {
        this.showReceiveDeliveryModal = false;
        this.selectedPurchaseOrderToReceive = null;
        this.highlightMessage = this.buildReceiveDeliveryMessage(result);
        this.actionMessage = this.highlightMessage;
        this.loadAllData();
      },
      error: (error) => {
        this.linkError = error?.error?.error || error?.error || this.i18n.t('purchases.receiveDeliveryError');
      }
    });
  }

  matchSupplierInvoiceToPurchaseOrder(): void {
    if (!this.selectedInvoiceToMatch?.id || !this.selectedPurchaseOrderId) {
      this.linkError = this.i18n.t('purchases.selectPurchaseOrderError');
      return;
    }

    this.linkError = '';
    this.businessService.matchSupplierInvoiceToPurchaseOrder(this.selectedInvoiceToMatch.id, this.selectedPurchaseOrderId).subscribe({
      next: (result: SupplierInvoicePurchaseOrderMatchResult) => {
        this.showMatchPurchaseOrderModal = false;
        this.selectedInvoiceToMatch = null;
        this.highlightMessage = this.buildMatchResultMessage(result);
        this.actionMessage = this.highlightMessage;
        this.loadAllData();
      },
      error: (error) => {
        this.linkError = error?.error?.error || error?.error || this.i18n.t('purchases.matchError');
      }
    });
  }

  approveSupplierInvoice(invoice: SupplierInvoice): void {
    if (!invoice.id || !this.perm.has(Permissions.SupplierInvoiceCreate)) return;
    const reason = prompt(this.i18n.t('purchases.approveMatchPrompt')) || undefined;
    this.businessService.approveSupplierInvoice(invoice.id, reason).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('purchases.approveMatchOk', { number: invoice.invoiceNumber });
        this.loadAllData();
      },
      error: (error) => {
        this.linkError = error?.error?.error || error?.error || this.i18n.t('purchases.approveMatchError');
      }
    });
  }

  previewMatchPurchaseOrder(): void {
    if (!this.selectedInvoiceToMatch?.id || !this.selectedPurchaseOrderId) {
      this.purchaseOrderMatchPreview = null;
      return;
    }

    this.linkError = '';
    this.businessService.previewSupplierInvoicePurchaseOrderMatch(this.selectedInvoiceToMatch.id, this.selectedPurchaseOrderId).subscribe({
      next: (result: SupplierInvoicePurchaseOrderMatchResult) => {
        this.purchaseOrderMatchPreview = result;
      },
      error: (error) => {
        this.purchaseOrderMatchPreview = null;
        this.linkError = error?.error?.error || error?.error || this.i18n.t('purchases.matchPreviewError');
      }
    });
  }

  addInvoiceLine(): void {
    this.newInvoice.lines.push({
      productKey: '',
      description: '',
      quantity: 1,
      unitPrice: 0,
      vatRate: 21,
      totalHT: 0,
      totalTTC: 0,
      lineNumber: this.newInvoice.lines.length + 1
    });
  }

  removeInvoiceLine(index: number): void {
    this.newInvoice.lines.splice(index, 1);
    this.reindexLines();
    this.recalculateInvoiceTotals();
  }

  calculateInvoiceLine(line: SupplierInvoice['lines'][number]): void {
    line.totalHT = (line.quantity || 0) * (line.unitPrice || 0);
    line.totalTTC = line.totalHT * (1 + (line.vatRate || 0) / 100);
    this.recalculateInvoiceTotals();
  }

  addPurchaseOrderLine(): void {
    this.newPurchaseOrder.lines.push({
      productKey: '',
      description: '',
      quantity: 1,
      receivedQuantity: 0,
      unitPrice: 0,
      vatRate: 21,
      totalHT: 0,
      totalTTC: 0,
      lineNumber: this.newPurchaseOrder.lines.length + 1
    });
  }

  removePurchaseOrderLine(index: number): void {
    this.newPurchaseOrder.lines.splice(index, 1);
    this.reindexPurchaseOrderLines();
    this.recalculatePurchaseOrderTotals();
  }

  calculatePurchaseOrderLine(line: PurchaseOrder['lines'][number]): void {
    line.totalHT = (line.quantity || 0) * (line.unitPrice || 0);
    line.totalTTC = line.totalHT * (1 + (line.vatRate || 0) / 100);
    this.recalculatePurchaseOrderTotals();
  }

  supplierName(invoice: SupplierInvoice): string {
    return invoice.supplier?.name || this.i18n.t('purchases.supplierNameHash', { id: invoice.supplierId });
  }

  supplierNameForOrder(order: PurchaseOrder): string {
    return order.supplier?.name || this.i18n.t('purchases.supplierNameHash', { id: order.supplierId });
  }

  supplierForSelectedDocument(): string {
    const document = [...this.invoiceDocuments, ...this.deliveryDocuments].find(d => d.id === this.selectedDocumentId);
    return document?.supplier || '';
  }

  isHighlightedInvoice(invoice: SupplierInvoice): boolean {
    return !!this.highlightedSupplierInvoiceId && invoice.id === this.highlightedSupplierInvoiceId;
  }

  isExpanded(kind: 'PurchaseOrder' | 'SupplierInvoice' | 'Receipt', id?: number): boolean {
    return !!id && this.expandedRowKey === this.rowKey(kind, id);
  }

  togglePurchaseOrderDetail(order: PurchaseOrder): void {
    this.toggleDetail('PurchaseOrder', order.id, () => this.businessService.getPurchaseOrder(order.id!));
  }

  toggleSupplierInvoiceDetail(invoice: SupplierInvoice): void {
    this.toggleDetail('SupplierInvoice', invoice.id, () => this.businessService.getSupplierInvoice(invoice.id!));
  }

  toggleReceiptDetail(receipt: Receipt): void {
    this.toggleDetail('Receipt', receipt.id, () => this.businessService.getReceipt(receipt.id!));
  }

  private rowKey(kind: string, id: number): string {
    return `${kind}:${id}`;
  }

  private toggleDetail<T extends PurchaseOrder | SupplierInvoice | Receipt>(
    kind: 'PurchaseOrder' | 'SupplierInvoice' | 'Receipt',
    id: number | undefined,
    loader: () => Observable<T>
  ): void {
    if (!id) return;
    const key = this.rowKey(kind, id);
    if (this.expandedRowKey === key) {
      this.expandedRowKey = null;
      this.clearDetail();
      return;
    }

    this.expandedRowKey = key;
    this.detailKind = kind;
    this.clearDetail();
    this.detailLoading = true;
    loader().subscribe({
      next: (full) => {
        if (kind === 'PurchaseOrder') this.detailPurchaseOrder = full as PurchaseOrder;
        else if (kind === 'SupplierInvoice') this.detailSupplierInvoice = full as SupplierInvoice;
        else this.detailReceipt = full as Receipt;
        this.detailLoading = false;
      },
      error: (error) => {
        this.detailLoading = false;
        this.expandedRowKey = null;
        this.createError = error?.error?.error || error?.error || this.i18n.t('purchases.detailLoadError');
      }
    });
  }

  private clearDetail(): void {
    this.detailPurchaseOrder = null;
    this.detailSupplierInvoice = null;
    this.detailReceipt = null;
  }

  get detailTitle(): string {
    if (this.detailKind === 'PurchaseOrder') {
      return this.i18n.t('purchases.detailTitle.order', { number: this.detailPurchaseOrder?.orderNumber || '' });
    }
    if (this.detailKind === 'SupplierInvoice') {
      return this.i18n.t('purchases.detailTitle.invoice', { number: this.detailSupplierInvoice?.invoiceNumber || '' });
    }
    if (this.detailKind === 'Receipt') {
      return this.i18n.t('purchases.detailTitle.receipt', { number: this.detailReceipt?.receiptNumber || '' });
    }
    return this.i18n.t('common.detail');
  }

  get detailPartyName(): string {
    if (this.detailPurchaseOrder) return this.supplierNameForOrder(this.detailPurchaseOrder);
    if (this.detailSupplierInvoice) return this.supplierName(this.detailSupplierInvoice);
    if (this.detailReceipt) return this.detailReceipt.supplier?.name || this.i18n.t('purchases.supplierNameHash', { id: this.detailReceipt.supplierId });
    return '-';
  }

  get detailLines(): Array<{ productKey: string; description: string; quantity: number; unitPrice: number; vatRate: number; totalHT: number; totalTTC: number; receivedQuantity?: number }> {
    if (this.detailPurchaseOrder?.lines) {
      return this.detailPurchaseOrder.lines;
    }
    if (this.detailSupplierInvoice?.lines) {
      return this.detailSupplierInvoice.lines;
    }
    return [];
  }

  get detailDoc(): PurchaseOrder | SupplierInvoice | null {
    return this.detailPurchaseOrder || this.detailSupplierInvoice;
  }

  receiptLineTotalTtc(line: Receipt['lines'][number]): number {
    return (line.lineAmountExclTax || 0) + (line.lineTaxAmount || 0);
  }

  get receiptTotals(): { ht: number; vat: number; ttc: number } {
    const lines = this.detailReceipt?.lines || [];
    const ht = lines.reduce((sum, l) => sum + (l.lineAmountExclTax || 0), 0);
    const vat = lines.reduce((sum, l) => sum + (l.lineTaxAmount || 0), 0);
    return { ht, vat, ttc: ht + vat };
  }

  matchingPurchaseOrdersForSelectedInvoice(): PurchaseOrder[] {
    if (!this.selectedInvoiceToMatch) return this.purchaseOrders;
    return this.purchaseOrders.filter(order => order.supplierId === this.selectedInvoiceToMatch?.supplierId);
  }

  private buildMatchResultMessage(result: SupplierInvoicePurchaseOrderMatchResult): string {
    const baseMessage = result.isBalanced
      ? this.i18n.t('purchases.matchOk', { invoice: result.invoice.invoiceNumber, order: result.purchaseOrder.orderNumber })
      : this.i18n.t(result.requiresApproval ? 'purchases.matchNeedsApproval' : 'purchases.matchWithGaps', {
          invoice: result.invoice.invoiceNumber,
          order: result.purchaseOrder.orderNumber
        });

    if (!result.warnings.length) {
      return baseMessage;
    }

    return `${baseMessage} ${result.warnings.slice(0, 2).join(' ')}`;
  }

  private buildReceiveDeliveryMessage(result: ReceiveDeliveryResult): string {
    const orderNumber = result.purchaseOrder?.orderNumber || '';
    let message = this.i18n.t('purchases.receiveApplied', { order: orderNumber });

    if (result.stockUpdated) {
      message += ' ' + this.i18n.t('purchases.receiveStockEntry', { qty: result.stockQuantityIn, count: result.stockMovementCount });
    } else if (result.stockAlreadyApplied) {
      message += ' ' + this.i18n.t('purchases.receiveStockAlreadyFed');
    }

    if (result.warnings?.length) {
      message += ` ${result.warnings.slice(0, 2).join(' ')}`;
    }

    return message;
  }


  private createEmptyInvoice(): SupplierInvoice {
    return {
      invoiceNumber: '',
      supplierId: 0,
      date: new Date().toISOString().slice(0, 10),
      dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
      status: 'Draft',
      totalHT: 0,
      totalVat: 0,
      totalTTC: 0,
      notes: '',
      lines: [
        {
          productKey: '',
          description: '',
          quantity: 1,
          unitPrice: 0,
          vatRate: 21,
          totalHT: 0,
          totalTTC: 0,
          lineNumber: 1
        }
      ]
    };
  }

  private createEmptyPurchaseOrder(): PurchaseOrder {
    return {
      orderNumber: '',
      supplierId: 0,
      date: new Date().toISOString().slice(0, 10),
      expectedDeliveryDate: '',
      status: 'Draft',
      totalHT: 0,
      totalVat: 0,
      totalTTC: 0,
      notes: '',
      lines: [
        {
          productKey: '',
          description: '',
          quantity: 1,
          receivedQuantity: 0,
          unitPrice: 0,
          vatRate: 21,
          totalHT: 0,
          totalTTC: 0,
          lineNumber: 1
        }
      ]
    };
  }

  private createEmptySupplier(): Partial<Supplier> {
    return {
      supplierCode: '',
      name: '',
      vatNumber: '',
      address: '',
      city: '',
      postalCode: '',
      country: 'BE',
      email: '',
      phone: ''
    };
  }

  private recalculateInvoiceTotals(): void {
    this.newInvoice.totalHT = this.newInvoice.lines.reduce((sum, line) => sum + (line.totalHT || 0), 0);
    this.newInvoice.totalTTC = this.newInvoice.lines.reduce((sum, line) => sum + (line.totalTTC || 0), 0);
    this.newInvoice.totalVat = this.newInvoice.totalTTC - this.newInvoice.totalHT;
  }

  private reindexLines(): void {
    this.newInvoice.lines.forEach((line, index) => {
      line.lineNumber = index + 1;
    });
  }

  private recalculatePurchaseOrderTotals(): void {
    this.newPurchaseOrder.totalHT = this.newPurchaseOrder.lines.reduce((sum, line) => sum + (line.totalHT || 0), 0);
    this.newPurchaseOrder.totalTTC = this.newPurchaseOrder.lines.reduce((sum, line) => sum + (line.totalTTC || 0), 0);
    this.newPurchaseOrder.totalVat = this.newPurchaseOrder.totalTTC - this.newPurchaseOrder.totalHT;
  }

  private reindexPurchaseOrderLines(): void {
    this.newPurchaseOrder.lines.forEach((line, index) => {
      line.lineNumber = index + 1;
    });
  }

  private sortSupplierInvoices(invoices: SupplierInvoice[]): SupplierInvoice[] {
    if (!this.highlightedSupplierInvoiceId) return invoices;

    return [...invoices].sort((a, b) => {
      if (a.id === this.highlightedSupplierInvoiceId) return -1;
      if (b.id === this.highlightedSupplierInvoiceId) return 1;
      return new Date(b.date).getTime() - new Date(a.date).getTime();
    });
  }

  // ── DPF — Demandes de prix fournisseur ───────────────────────────────────────

  private createEmptyRfq(): SupplierRfq {
    return {
      rfqNumber: '',
      supplierId: undefined,
      date: new Date().toISOString().slice(0, 10),
      status: 'Draft',
      notes: '',
      lines: [
        { productKey: '', description: '', quantity: 1, estimatedUnitPrice: 0, lineNumber: 1 }
      ]
    };
  }

  openRfqModal(): void {
    if (!this.perm.has(Permissions.PurchaseOrderCreate)) return;
    this.newRfq = this.createEmptyRfq();
    this.rfqError = '';
    this.showRfqModal = true;
  }

  addRfqLine(): void {
    this.newRfq.lines.push({ productKey: '', description: '', quantity: 1, estimatedUnitPrice: 0, lineNumber: this.newRfq.lines.length + 1 });
  }

  removeRfqLine(index: number): void {
    this.newRfq.lines.splice(index, 1);
    this.newRfq.lines.forEach((l, i) => l.lineNumber = i + 1);
  }

  saveRfq(): void {
    if (!this.newRfq.lines.length || this.newRfq.lines.every(l => !l.description && !l.productKey)) {
      this.rfqError = this.i18n.t('purchases.addLineError');
      return;
    }
    this.rfqError = '';
    this.saving = true;
    this.businessService.createSupplierRfq(this.newRfq).subscribe({
      next: (created) => {
        this.saving = false;
        this.finishModalSuccess(
          () => { this.showRfqModal = false; },
          this.i18n.t('purchases.rfqs.created', { number: created.rfqNumber })
        );
      },
      error: (err) => {
        this.saving = false;
        this.rfqError = err?.error?.error || err?.error || this.i18n.t('purchases.rfqs.error');
      }
    });
  }

  canSendRfq(r: SupplierRfq): boolean {
    return !!r.id && this.perm.has(Permissions.PurchaseOrderUpdate) && (r.status || '').toLowerCase() === 'draft';
  }

  canConvertRfq(r: SupplierRfq): boolean {
    if (!r.id || !this.perm.has(Permissions.PurchaseOrderCreate)) return false;
    const s = (r.status || '').toLowerCase();
    return s !== 'processed' && s !== 'cancelled';
  }

  canCancelRfq(r: SupplierRfq): boolean {
    if (!r.id || !this.perm.has(Permissions.PurchaseOrderUpdate)) return false;
    const s = (r.status || '').toLowerCase();
    return s !== 'processed' && s !== 'cancelled';
  }

  canDeleteRfq(r: SupplierRfq): boolean {
    return !!r.id && this.perm.has(Permissions.PurchaseOrderUpdate) && (r.status || '').toLowerCase() === 'draft';
  }

  sendSupplierRfq(r: SupplierRfq): void {
    if (!r.id || !this.canSendRfq(r)) return;
    this.rfqError = '';
    this.businessService.sendSupplierRfq(r.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('purchases.rfqs.sent', { number: updated.rfqNumber });
        this.loadSupplierRfqs();
      },
      error: (err) => {
        this.rfqError = err?.error?.error || err?.error || this.i18n.t('purchases.rfqs.error');
      }
    });
  }

  convertRfqToPurchaseOrder(r: SupplierRfq): void {
    if (!r.id || !this.canConvertRfq(r)) return;
    this.rfqError = '';
    this.businessService.convertRfqToPurchaseOrder(r.id).subscribe({
      next: (order) => {
        this.actionMessage = this.i18n.t('purchases.rfqs.converted', { order: order.orderNumber });
        this.loadSupplierRfqs();
        this.businessService.getPurchaseOrders().subscribe(o => this.purchaseOrders = o);
      },
      error: (err) => {
        this.rfqError = err?.error?.error || err?.error || this.i18n.t('purchases.rfqs.error');
      }
    });
  }

  cancelSupplierRfq(r: SupplierRfq): void {
    if (!r.id || !this.canCancelRfq(r)) return;
    const reason = prompt(this.i18n.t('purchases.cancelReasonPrompt'));
    if (reason == null) return;
    this.rfqError = '';
    this.businessService.cancelSupplierRfq(r.id, reason.trim() || undefined).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('purchases.rfqs.cancelled', { number: updated.rfqNumber });
        this.loadSupplierRfqs();
      },
      error: (err) => {
        this.rfqError = err?.error?.error || err?.error || this.i18n.t('purchases.rfqs.error');
      }
    });
  }

  deleteSupplierRfq(r: SupplierRfq): void {
    if (!r.id || !this.canDeleteRfq(r)) return;
    if (!confirm(this.i18n.t('purchases.rfqs.confirmDelete', { number: r.rfqNumber }))) return;
    this.businessService.deleteSupplierRfq(r.id).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('purchases.rfqs.deleted', { number: r.rfqNumber });
        this.supplierRfqs = this.supplierRfqs.filter(x => x.id !== r.id);
      },
      error: (err) => {
        this.rfqError = err?.error?.error || err?.error || this.i18n.t('purchases.rfqs.error');
      }
    });
  }

  // ── BRF — Retours fournisseur ─────────────────────────────────────────────────

  private createEmptySupplierReturn(): SupplierReturn {
    return {
      returnNumber: '',
      supplierId: 0,
      date: new Date().toISOString().slice(0, 10),
      status: 'Draft',
      totalHT: 0,
      totalVat: 0,
      totalTTC: 0,
      notes: '',
      lines: [
        { productKey: '', description: '', quantity: 1, unitPrice: 0, vatRate: 21, totalHT: 0, totalTTC: 0, lineNumber: 1 }
      ]
    };
  }

  openSupplierReturnModal(): void {
    if (!this.perm.has(Permissions.PurchaseOrderCreate)) return;
    this.newSupplierReturn = this.createEmptySupplierReturn();
    this.supplierReturnError = '';
    this.showSupplierReturnModal = true;
  }

  addSupplierReturnLine(): void {
    this.newSupplierReturn.lines.push({ productKey: '', description: '', quantity: 1, unitPrice: 0, vatRate: 21, totalHT: 0, totalTTC: 0, lineNumber: this.newSupplierReturn.lines.length + 1 });
  }

  removeSupplierReturnLine(index: number): void {
    this.newSupplierReturn.lines.splice(index, 1);
    this.newSupplierReturn.lines.forEach((l, i) => l.lineNumber = i + 1);
  }

  calcSupplierReturnLine(line: SupplierReturnLine): void {
    line.totalHT = (line.quantity || 0) * (line.unitPrice || 0);
    line.totalTTC = line.totalHT * (1 + (line.vatRate || 0) / 100);
  }

  saveSupplierReturn(): void {
    if (!this.newSupplierReturn.supplierId) {
      this.supplierReturnError = this.i18n.t('purchases.selectSupplierError');
      return;
    }
    if (!this.newSupplierReturn.lines.length || this.newSupplierReturn.lines.every(l => !l.description && !l.productKey)) {
      this.supplierReturnError = this.i18n.t('purchases.addLineError');
      return;
    }
    this.newSupplierReturn.lines.forEach(l => this.calcSupplierReturnLine(l));
    this.supplierReturnError = '';
    this.saving = true;
    this.businessService.createSupplierReturn(this.newSupplierReturn).subscribe({
      next: (created) => {
        this.saving = false;
        this.finishModalSuccess(
          () => { this.showSupplierReturnModal = false; },
          this.i18n.t('purchases.supplierReturns.created', { number: created.returnNumber })
        );
      },
      error: (err) => {
        this.saving = false;
        this.supplierReturnError = err?.error?.error || err?.error || this.i18n.t('purchases.supplierReturns.error');
      }
    });
  }

  canShipSupplierReturn(r: SupplierReturn): boolean {
    return !!r.id && this.perm.has(Permissions.PurchaseOrderUpdate) && (r.status || '').toLowerCase() === 'draft';
  }

  canCancelSupplierReturn(r: SupplierReturn): boolean {
    if (!r.id || !this.perm.has(Permissions.PurchaseOrderUpdate)) return false;
    return (r.status || '').toLowerCase() !== 'cancelled' && !r.creditNoteId;
  }

  canCreateCreditNoteForSupplierReturn(r: SupplierReturn): boolean {
    if (!r.id || r.creditNoteId || !this.perm.has(Permissions.SupplierCreditNoteCreate)) return false;
    return (r.status || '').toLowerCase() !== 'cancelled';
  }

  shipSupplierReturn(r: SupplierReturn): void {
    if (!r.id || !this.canShipSupplierReturn(r)) return;
    this.supplierReturnError = '';
    this.businessService.shipSupplierReturn(r.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('purchases.supplierReturns.shipped', { number: updated.returnNumber });
        this.loadSupplierReturns();
      },
      error: (err) => {
        this.supplierReturnError = err?.error?.error || err?.error || this.i18n.t('purchases.supplierReturns.error');
      }
    });
  }

  cancelSupplierReturn(r: SupplierReturn): void {
    if (!r.id || !this.canCancelSupplierReturn(r)) return;
    const reason = prompt(this.i18n.t('purchases.cancelReasonPrompt'));
    if (reason == null) return;
    this.supplierReturnError = '';
    this.businessService.cancelSupplierReturn(r.id, reason.trim() || undefined).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('purchases.supplierReturns.cancelled', { number: updated.returnNumber });
        this.loadSupplierReturns();
      },
      error: (err) => {
        this.supplierReturnError = err?.error?.error || err?.error || this.i18n.t('purchases.supplierReturns.error');
      }
    });
  }

  createCreditNoteFromSupplierReturn(r: SupplierReturn): void {
    if (!r.id || !this.canCreateCreditNoteForSupplierReturn(r)) return;
    this.supplierReturnError = '';
    this.businessService.createCreditNoteFromSupplierReturn(r.id, r.supplierInvoiceId).subscribe({
      next: (creditNote) => {
        this.actionMessage = this.i18n.t('purchases.supplierReturns.creditNoteCreated', { number: creditNote.creditNoteNumber });
        this.loadSupplierReturns();
        this.loadSupplierCreditNotes();
      },
      error: (err) => {
        this.supplierReturnError = err?.error?.error || err?.error || this.i18n.t('purchases.supplierReturns.error');
      }
    });
  }

  // ── AF — Avoirs fournisseur ───────────────────────────────────────────────────

  canValidateSupplierCreditNote(c: SupplierCreditNote): boolean {
    return !!c.id && this.perm.has(Permissions.SupplierCreditNoteUpdate) && (c.status || '').toLowerCase() === 'draft';
  }

  canApplySupplierCreditNote(c: SupplierCreditNote): boolean {
    if (!c.id || !this.perm.has(Permissions.SupplierCreditNoteUpdate)) return false;
    const s = (c.status || '').toLowerCase();
    return s === 'draft' || s === 'validated';
  }

  canCancelSupplierCreditNote(c: SupplierCreditNote): boolean {
    if (!c.id || !this.perm.has(Permissions.SupplierCreditNoteUpdate)) return false;
    const s = (c.status || '').toLowerCase();
    return s !== 'applied' && s !== 'cancelled';
  }

  validateSupplierCreditNote(c: SupplierCreditNote): void {
    if (!c.id || !this.canValidateSupplierCreditNote(c)) return;
    this.creditNoteError = '';
    this.businessService.validateSupplierCreditNote(c.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('purchases.supplierCreditNotes.validated', { number: updated.creditNoteNumber });
        this.loadSupplierCreditNotes();
      },
      error: (err) => {
        this.creditNoteError = err?.error?.error || err?.error || this.i18n.t('purchases.supplierCreditNotes.error');
      }
    });
  }

  applySupplierCreditNote(c: SupplierCreditNote): void {
    if (!c.id || !this.canApplySupplierCreditNote(c)) return;
    this.creditNoteError = '';
    this.businessService.applySupplierCreditNote(c.id).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('purchases.supplierCreditNotes.applied', { number: updated.creditNoteNumber });
        this.loadSupplierCreditNotes();
      },
      error: (err) => {
        this.creditNoteError = err?.error?.error || err?.error || this.i18n.t('purchases.supplierCreditNotes.error');
      }
    });
  }

  cancelSupplierCreditNote(c: SupplierCreditNote): void {
    if (!c.id || !this.canCancelSupplierCreditNote(c)) return;
    const reason = prompt(this.i18n.t('purchases.cancelReasonPrompt'));
    if (reason == null) return;
    this.creditNoteError = '';
    this.businessService.cancelSupplierCreditNote(c.id, reason.trim() || undefined).subscribe({
      next: (updated) => {
        this.actionMessage = this.i18n.t('purchases.supplierCreditNotes.cancelled', { number: updated.creditNoteNumber });
        this.loadSupplierCreditNotes();
      },
      error: (err) => {
        this.creditNoteError = err?.error?.error || err?.error || this.i18n.t('purchases.supplierCreditNotes.error');
      }
    });
  }
}
