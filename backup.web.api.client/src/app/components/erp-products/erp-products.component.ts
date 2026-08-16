import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { ErpBrand, ErpCategory, ErpProduct, ErpProductOem, ErpProductVehicle, ErpSyncLog } from '../../models/erp-product';
import { ErpProductService } from '../../services/erp-product.service';
import { SupplierQuoteService } from '../../services/supplier-quote.service';
import { SupplierQuoteRealtimeService } from '../../services/supplier-quote-realtime.service';
import { SupplierQuotesResult } from '../../models/supplier-quote';
import { ProductDiagram, DiagramHotspot } from '../../models/product-diagram';
import { ProductDiagramService } from '../../services/product-diagram.service';
import { environment } from '../../../environments/environment';
import { forkJoin, interval, Observable, of, Subscription, switchMap, takeWhile, timer, combineLatest } from 'rxjs';
import { catchError, distinctUntilChanged, filter, map } from 'rxjs/operators';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { CatalogSubnavComponent } from '../shared/catalog-subnav/catalog-subnav.component';
import { TableSortState } from '../../utils/table-sort';
import { SortableThComponent } from '../shared/sortable-th/sortable-th.component';
import { CompanyService } from '../../services/company.service';
import { BusinessService } from '../../services/business.service';
import { Supplier } from '../../models/business';
import { ErpBrandService } from '../../services/erp-brand.service';
import { ErpCategoryService } from '../../services/erp-category.service';
import {
  CarApiService,
  CarApiVehicleBrand,
  CarApiVehicleGeneration,
  CarApiVehicleModel,
  VehicleCompatibilityEntry
} from '../../services/car-api.service';
import {
  ErpCatalogExtrasService,
  ErpProductAttributeDefinition,
  ErpProductAttributeValue,
  ErpProductImage,
  ErpProductVariant
} from '../../services/erp-catalog-extras.service';

@Component({
  selector: 'app-erp-products',
  templateUrl: './erp-products.component.html',
  styleUrls: ['./erp-products.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule, TPipe, FormHelpComponent, SortableThComponent, CatalogSubnavComponent]
})
export class ErpProductsComponent implements OnInit, OnDestroy {
  products: ErpProduct[] = [];
  selected: ErpProduct | null = null;
  detailTab: 'info' | 'variants' | 'images' | 'attributes' | 'vehicles' | 'oems' | 'suppliers' | 'diagram' = 'info';
  createTab: 'info' | 'variants' | 'images' | 'attributes' = 'info';

  get productModalOpen(): boolean {
    return this.productEditing;
  }
  listSort = new TableSortState('name', 'asc');
  catalogView: 'list' | 'cards' = ErpProductsComponent.readStoredCatalogView();
  private static readonly catalogViewKey = 'pulse.erpProducts.catalogView';
  total = 0;
  page = 1;
  pageSize = 50;
  loading = false;
  syncingId: number | null = null;
  syncingAll = false;
  syncProgress: ErpSyncLog | null = null;
  syncMode: 'catalog' | 'enrich' | null = null;
  syncFilterLabel = '';
  private syncPollSub: Subscription | null = null;
  private companyContextSub: Subscription | null = null;
  private galleryLoopSub: Subscription | null = null;
  private quotesRealtimeSub: Subscription | null = null;
  supplierQuotes: SupplierQuotesResult | null = null;
  supplierQuotesLoading = false;
  productDiagrams: ProductDiagram[] = [];
  productDiagramsLoading = false;

  /** Index courant de la galerie détail (loop multi-images pièces auto). */
  galleryIndex = 0;
  galleryPaused = false;
  galleryLightboxOpen = false;

  searchQuery = '';
  /** Deep-link OEM / catalogue : ouvrir ce produit après chargement. */
  private pendingFocusProductId: number | null = null;
  brandFilter = '';
  sourceFilter = '';
  mainTypeId = '';
  typeId = '';
  subTypeId = '';
  vehicleBrandFilter = '';
  vehicleModelFilter = '';
  vehicleYearFilter: number | null = null;
  /** Saisie année (4 chiffres max, ex. 2018). */
  vehicleYearDraft = '';
  vehicleFuelFilter = '';
  vehicleBodyFilter = '';
  vehicleDriveFilter = '';
  vehicleTransmissionFilter = '';
  vehicleEngineFilter = '';
  vehicleKTypeFilter = '';
  vehicleFiltersOpen = true;
  filterVehicleBrands: string[] = [];
  filterVehicleModels: string[] = [];
  filterVehicleFuels: string[] = [];
  filterVehicleBodies: string[] = [];
  filterVehicleDrives: string[] = [];
  filterVehicleTransmissions: string[] = [];

  brands: ErpBrand[] = [];
  mainTypes: ErpCategory[] = [];
  /** Catalogue RapidAPI : catégories plates Level=Type (pas de MainType). */
  categoryRootsAreTypes = false;
  types: ErpCategory[] = [];
  subTypes: ErpCategory[] = [];

  variants: ErpProductVariant[] = [];
  images: ErpProductImage[] = [];
  productVehicles: ErpProductVehicle[] = [];
  productOems: ErpProductOem[] = [];
  /** Filtres locaux de l'onglet Véhicules (détail produit). */
  detailVehicleMake = '';
  detailVehicleModel = '';
  detailVehicleQuery = '';
  attrDefs: ErpProductAttributeDefinition[] = [];
  attrValues: ErpProductAttributeValue[] = [];
  editingVariantId: string | null = null;
  draftVariantEditIndex: number | null = null;
  variantForm: Partial<ErpProductVariant> = { sku: '', stockQuantity: 0, attributesJson: '{}', isActive: true };
  imageForm: Partial<ErpProductImage> = { url: '', altText: '', isMain: false, sortOrder: 0 };
  attrValueDrafts: Record<string, string> = {};
  extrasLoading = false;
  extrasSaving = false;

  productEditing = false;
  productCreating = false;
  productSaving = false;
  productForm = {
    name: '',
    reference: '',
    ean: '',
    purchasePrice: null as number | null,
    unitPrice: null as number | null,
    vatPercent: 21,
    brandId: null as number | null,
    brandName: '',
    mainTypeCatId: null as number | null,
    typeCatId: null as number | null,
    subTypeCatId: null as number | null,
    comment: '',
    weight: null as number | null,
    height: null as number | null,
    width: null as number | null,
    depth: null as number | null,
    isDropship: false,
    dropshipSupplierId: null as number | null
  };
  formMainTypes: ErpCategory[] = [];
  formTypes: ErpCategory[] = [];
  formSubTypes: ErpCategory[] = [];
  formBrands: ErpBrand[] = [];
  formSuppliers: Supplier[] = [];
  draftVariants: Array<{
    sku: string;
    barcode: string;
    costPrice: number | null;
    priceOverride: number | null;
    stockQuantity: number;
    attributesJson: string;
  }> = [];
  draftImages: Array<{ url: string; altText: string; isMain: boolean; sortOrder: number }> = [];
  draftVariantSku = '';
  draftVariantBarcode = '';
  draftVariantCost: number | null = null;
  draftVariantPrice: number | null = null;
  draftVariantStock = 0;
  draftImageUrl = '';
  draftImageAlt = '';

  readonly vehicleCompatCode = 'vehicle_compat';
  vehicleCompatDef: ErpProductAttributeDefinition | null = null;
  vehicleBrands: CarApiVehicleBrand[] = [];
  vehicleModels: CarApiVehicleModel[] = [];
  vehicleGenerations: CarApiVehicleGeneration[] = [];
  vehiclePick = { brand: '', model: '', generation: '', yearFrom: null as number | null, yearTo: null as number | null };
  vehicleCompatList: VehicleCompatibilityEntry[] = [];

  private readonly allSourceOptions = [
    { value: '', labelKey: 'erpProducts.filter.allSources' },
    { value: 'Excel', labelKey: 'erpProducts.filter.sourceExcel' },
    { value: 'Merged', labelKey: 'erpProducts.filter.sourceMerged' },
    { value: 'Erp', labelKey: 'erpProducts.filter.sourceErp' }
  ];

  get sourceOptions(): { value: string; labelKey: string }[] {
    return this.allSourceOptions;
  }

  constructor(
    private erpService: ErpProductService,
    private brandService: ErpBrandService,
    private categoryService: ErpCategoryService,
    private extras: ErpCatalogExtrasService,
    private carApi: CarApiService,
    private supplierQuotesApi: SupplierQuoteService,
    private supplierQuotesRealtime: SupplierQuoteRealtimeService,
    private productDiagramsApi: ProductDiagramService,
    private businessService: BusinessService,
    private sanitizer: DomSanitizer,
    private snack: MatSnackBar,
    private i18n: AppI18nService,
    public perm: PermissionService,
    public company: CompanyService,
    private route: ActivatedRoute
  ) {}

  readonly P = Permissions;

  get hasErpCatalogSync(): boolean {
    return this.company.hasErpCatalogSync;
  }

  /** Module pièces auto (scan plaque / VIN) : pas Euro Brico ERP. */
  get showAutoPartsImport(): boolean {
    if (!this.company.modulesReady) return false;
    if (this.hasErpCatalogSync) return false;
    if (this.company.modules.length === 0) return true;
    return this.company.hasAutoParts;
  }

  /** Filtres véhicule (marque / modèle / année) : réservés au module auto_parts. */
  get showVehicleFilters(): boolean {
    if (!this.company.modulesReady) return false;
    if (this.company.modules.length === 0) return true;
    return this.company.hasAutoParts;
  }

  get genericAttrDefs(): ErpProductAttributeDefinition[] {
    return this.attrDefs.filter(d => d.code !== this.vehicleCompatCode);
  }

  get sortedProducts(): ErpProduct[] {
    void this.listSort.version;
    return this.listSort.sort(this.products, {
      name: p => p.name ?? '',
      reference: p => p.reference ?? p.ean ?? '',
      brand: p => p.brand ?? '',
      unitPrice: p => p.unitPrice ?? null,
      stockQuantity: p => p.stockQuantity ?? null,
      dataSource: p => p.dataSource ?? '',
      lastSyncAt: p => p.lastSyncAt ?? ''
    });
  }

  setCatalogView(view: 'list' | 'cards'): void {
    this.catalogView = view;
    try {
      localStorage.setItem(ErpProductsComponent.catalogViewKey, view);
    } catch {
      /* ignore quota / private mode */
    }
  }

  private static readStoredCatalogView(): 'list' | 'cards' {
    try {
      return localStorage.getItem(ErpProductsComponent.catalogViewKey) === 'cards' ? 'cards' : 'list';
    } catch {
      return 'list';
    }
  }

  ngOnInit(): void {
    this.bindCompanyContext();
    this.quotesRealtimeSub = this.supplierQuotesRealtime.quotes$.subscribe(result => {
      if (this.selected?.id === result.productId) {
        this.supplierQuotes = result;
      }
    });
  }

  private lastBoundCompanyId: string | null = null;

  /** Attendre modules société (évite mode pièces auto au F5 sur EuroBrico). */
  private bindCompanyContext(): void {
    this.companyContextSub?.unsubscribe();
    this.companyContextSub = combineLatest([
      this.company.modulesReady$.pipe(filter(ready => ready)),
      this.company.activeCompanyId$
    ]).pipe(
      distinctUntilChanged((a, b) => a[0] === b[0] && a[1] === b[1]),
      map(([, companyId]) => companyId)
    ).subscribe(companyId => {
      if (!companyId) return;

      const companyChanged = this.lastBoundCompanyId !== companyId;
      if (companyChanged) {
        this.lastBoundCompanyId = companyId;
      } else {
        return;
      }

      const hasVehicleQuery = !!(
        this.route.snapshot.queryParamMap.get('vehicleBrand')?.trim()
        || this.route.snapshot.queryParamMap.get('q')?.trim()
        || this.route.snapshot.queryParamMap.get('productId')?.trim()
        || this.route.snapshot.queryParamMap.get('id')?.trim()
        || this.route.snapshot.queryParamMap.get('vehicleKType')?.trim()
      );

      this.resetCategoryFiltersForCompany();
      this.applyVehicleQueryParams();
      this.loadFilterOptions();
      if (!hasVehicleQuery) this.loadProducts();
      if (this.showVehicleFilters) {
        this.initVehicleCompatibility();
        this.loadVehicleFilterBrands();
      }
    });
  }

  private resetCategoryFiltersForCompany(): void {
    this.categoryRootsAreTypes = false;
    this.mainTypeId = '';
    this.typeId = '';
    this.subTypeId = '';
    this.mainTypes = [];
    this.types = [];
    this.subTypes = [];
    this.vehicleBrandFilter = '';
    this.vehicleModelFilter = '';
    this.vehicleYearFilter = null;
    this.vehicleYearDraft = '';
    this.vehicleFuelFilter = '';
    this.vehicleBodyFilter = '';
    this.vehicleDriveFilter = '';
    this.vehicleTransmissionFilter = '';
    this.vehicleEngineFilter = '';
    this.vehicleKTypeFilter = '';
    this.sourceFilter = '';
    this.filterVehicleModels = [];
    this.filterVehicleFuels = [];
    this.filterVehicleBodies = [];
    this.filterVehicleDrives = [];
    this.filterVehicleTransmissions = [];
  }

  /** Deep-link : ?q= / ?productId= / ?vehicleBrand=&vehicleModel=&vehicleYear=&vehicleKType= */
  private applyVehicleQueryParams(): void {
    const q = this.route.snapshot.queryParamMap;
    const search = q.get('q')?.trim();
    if (search) this.searchQuery = search;

    const productIdRaw = q.get('productId')?.trim() || q.get('id')?.trim();
    const focusProductId = productIdRaw && !Number.isNaN(Number(productIdRaw))
      ? Number(productIdRaw)
      : null;
    this.pendingFocusProductId = focusProductId;

    const kType = q.get('vehicleKType')?.trim();
    if (kType) this.vehicleKTypeFilter = kType;

    if (!this.showVehicleFilters) {
      if (search || focusProductId != null || kType) this.applyFilters();
      return;
    }

    const brand = q.get('vehicleBrand')?.trim();
    const model = q.get('vehicleModel')?.trim();
    const yearRaw = q.get('vehicleYear')?.trim();
    const fuel = q.get('vehicleFuel')?.trim();
    if (yearRaw) {
      const digits = this.sanitizeVehicleYearDraft(yearRaw);
      const y = this.normalizeVehicleYear(Number(digits));
      this.vehicleYearFilter = y;
      this.vehicleYearDraft = digits;
    }
    if (fuel) this.vehicleFuelFilter = fuel;
    if (!brand) {
      this.loadVehicleFuelOptions();
      // OEM / lien catalogue : ?q= / ?productId= / ?vehicleKType= sans marque
      if (fuel || search || focusProductId != null || kType) this.applyFilters();
      return;
    }

    this.vehicleBrandFilter = brand;
    this.filterVehicleModels = [];
    this.erpService.getVehicleModels(brand).subscribe({
      next: models => {
        this.filterVehicleModels = models ?? [];
        if (model) this.vehicleModelFilter = model;
        this.loadVehicleFuelOptions();
        this.applyFilters();
      },
      error: () => {
        this.filterVehicleModels = [];
        if (model) this.vehicleModelFilter = model;
        this.loadVehicleFuelOptions();
        this.applyFilters();
      }
    });
  }

  ngOnDestroy(): void {
    this.companyContextSub?.unsubscribe();
    this.quotesRealtimeSub?.unsubscribe();
    void this.supplierQuotesRealtime.unwatch();
    this.stopGalleryLoop();
    this.stopSyncPoll();
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (!this.galleryLightboxOpen) return;
    if (event.key === 'Escape') {
      this.closeGalleryLightbox();
      return;
    }
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.nextGalleryImage();
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.prevGalleryImage();
    }
  }

  get hasSyncFilter(): boolean {
    return !!(
      this.brandFilter.trim()
      || this.mainTypeId
      || this.typeId
      || this.subTypeId
    );
  }

  get syncProgressTitle(): string {
    const mode = this.parseSyncDetails(this.syncProgress ?? ({} as ErpSyncLog)).mode;
    if (mode === 'FullCatalog') return this.i18n.t('erpProducts.progress.fullCatalog');
    if (this.syncMode === 'catalog') return this.i18n.t('erpProducts.progress.filtered');
    return this.i18n.t('erpProducts.progress.enrich');
  }

  get syncProgressPercent(): number {
    const log = this.syncProgress;
    if (!log || !log.totalProducts || log.totalProducts <= 0) return 0;
    const processed = log.processedProducts ?? 0;
    return Math.min(100, Math.round((processed / log.totalProducts) * 100));
  }

  get syncProgressIndeterminate(): boolean {
    return !!this.syncProgress
      && this.syncProgress.status === 'Running'
      && (!this.syncProgress.totalProducts || this.syncProgress.totalProducts <= 0);
  }

  get syncProgressLabel(): string {
    const log = this.syncProgress;
    if (!log) return '';

    const details = this.parseSyncDetails(log);
    if (details.phase === 'collecting' || details.phase === 'starting' || this.syncProgressIndeterminate) {
      if (details.mode === 'FullCatalog') {
        return 'Collecte des IDs produits depuis l\'ERP… (peut prendre plusieurs minutes)';
      }
      return this.syncMode === 'catalog'
        ? 'Préparation de la sync filtrée…'
        : 'Préparation de l\'enrichissement…';
    }

    const processed = log.processedProducts ?? 0;
    const scope = details.mode === 'FullCatalog'
      ? 'produits ERP'
      : 'produits synchronisés';
    return `${processed} / ${log.totalProducts} ${scope}`
      + ` · +${log.newProducts} créés · ${log.updatedProducts} maj · ${log.failedProducts} échecs`;
  }

  private parseSyncDetails(log: ErpSyncLog): { mode?: string; phase?: string } {
    if (!log.details) return {};
    try {
      return JSON.parse(log.details) as { mode?: string; phase?: string };
    } catch {
      return {};
    }
  }

  private buildSyncFilterLabel(): string {
    const parts: string[] = [];
    if (this.brandFilter.trim()) parts.push(this.brandFilter.trim());

    const main = this.mainTypes.find(c => c.erpExternalId === this.mainTypeId);
    if (main) parts.push(this.categoryLabel(main));

    const type = this.types.find(c => c.erpExternalId === this.typeId);
    if (type) parts.push(this.categoryLabel(type));

    const sub = this.subTypes.find(c => c.erpExternalId === this.subTypeId);
    if (sub) parts.push(this.categoryLabel(sub));

    return parts.join(' / ');
  }

  private startSyncTracking(mode: 'catalog' | 'enrich'): void {
    this.syncMode = mode;
    this.syncFilterLabel = mode === 'catalog' ? this.buildSyncFilterLabel() : '';
    this.syncingAll = true;
    this.syncProgress = null;
  }

  private resetSyncTracking(): void {
    this.syncingAll = false;
    this.syncMode = null;
    this.syncFilterLabel = '';
    this.syncProgress = null;
  }

  private currentBrandFilter(): string | undefined {
    return this.brandFilter.trim() || undefined;
  }

  private currentCategoryFilter(): { mainTypeId?: string; typeId?: string; subTypeId?: string } {
    if (this.categoryRootsAreTypes) {
      return {
        typeId: this.mainTypeId || undefined
      };
    }
    return {
      subTypeId: this.subTypeId || undefined,
      typeId: (!this.subTypeId && this.typeId) || undefined,
      mainTypeId: (!this.subTypeId && !this.typeId && this.mainTypeId) || undefined
    };
  }

  loadFilterOptions(): void {
    this.loadBrands();
    this.loadMainTypes();
  }

  loadBrands(): void {
    const apply = (brands: ErpBrand[]) => {
      // Filtre produit = marque fabricant (Bosch…), pas marque véhicule (BMW…).
      const supplierOnly = (brands ?? []).filter(b => !this.isVehicleBrand(b));
      this.brands = this.mergeBrandOptions(supplierOnly);
      if (this.brandFilter && !this.brands.some(b => b.name === this.brandFilter)) {
        this.brandFilter = '';
      }
    };
    this.brandService.list().subscribe({
      next: brands => {
        if (brands?.length) {
          apply(brands);
          return;
        }
        this.erpService.getBrands(this.currentCategoryFilter()).subscribe({
          next: apply,
          error: () => apply([])
        });
      },
      error: () => {
        this.erpService.getBrands(this.currentCategoryFilter()).subscribe({
          next: apply,
          error: () => apply([])
        });
      }
    });
  }

  private isVehicleBrand(b: ErpBrand): boolean {
    const d = (b.description ?? '').toLowerCase();
    return d.includes('véhicule') || d.includes('vehicule') || d.includes('vehicle') || d.includes('car-api');
  }

  loadMainTypes(): void {
    if (this.showVehicleFilters) {
      this.loadAutoPartsCategories();
      return;
    }
    this.loadEuroBricoMainTypes();
  }

  /** Pièces auto : une seule liste Type (RapidAPI / CarAPI). */
  private loadAutoPartsCategories(): void {
    this.categoryRootsAreTypes = true;
    this.types = [];
    this.subTypes = [];
    this.typeId = '';
    this.subTypeId = '';
    const brand = this.currentBrandFilter();
    this.erpService.getCategories({
      level: 'Type',
      brand,
      flatCatalog: true
    }).subscribe({
      next: (items) => {
        this.mainTypes = items ?? [];
        if (this.mainTypeId && !this.mainTypes.some(c => c.erpExternalId === this.mainTypeId)) {
          this.mainTypeId = '';
        }
      },
      error: () => { this.mainTypes = []; }
    });
  }

  /** EuroBrico : hiérarchie MainType → Type → SubType. */
  private loadEuroBricoMainTypes(): void {
    this.categoryRootsAreTypes = false;
    const brand = this.currentBrandFilter();
    const req = brand
      ? this.erpService.getCategories({ level: 'MainType', brand })
      : this.categoryService.list({ level: 'MainType', activeOnly: true });
    req.subscribe({
      next: (items) => {
        this.mainTypes = items ?? [];
        if (this.mainTypeId && !this.mainTypes.some(c => c.erpExternalId === this.mainTypeId)) {
          this.mainTypeId = '';
          this.typeId = '';
          this.subTypeId = '';
          this.types = [];
          this.subTypes = [];
        }
      },
      error: () => {
        this.categoryService.list({ level: 'MainType', activeOnly: true }).subscribe({
          next: items => {
            this.mainTypes = items ?? [];
          },
          error: () => { this.mainTypes = []; }
        });
      }
    });
  }

  private loadChildCategories(
    level: 'Type' | 'SubType',
    parent: ErpCategory,
    target: 'types' | 'subTypes'
  ): void {
    const brand = this.currentBrandFilter();
    const req = brand
      ? this.erpService.getCategories({ level, parentId: parent.id, brand })
      : this.categoryService.list({ level, parentId: parent.id, activeOnly: true });
    req.subscribe({
      next: (items) => {
        if (target === 'types') this.types = items ?? [];
        else this.subTypes = items ?? [];
      },
      error: () => {
        this.categoryService.list({ level, parentId: parent.id, activeOnly: true }).subscribe({
          next: items => {
            if (target === 'types') this.types = items ?? [];
            else this.subTypes = items ?? [];
          },
          error: () => {
            if (target === 'types') this.types = [];
            else this.subTypes = [];
          }
        });
      }
    });
  }

  onBrandFilterChange(): void {
    this.mainTypeId = '';
    this.typeId = '';
    this.subTypeId = '';
    this.types = [];
    this.subTypes = [];
    this.loadMainTypes();
    this.page = 1;
    this.loadProducts();
  }

  onMainTypeChange(): void {
    this.typeId = '';
    this.subTypeId = '';
    this.types = [];
    this.subTypes = [];

    if (!this.categoryRootsAreTypes) {
      const mainType = this.mainTypes.find(c => c.erpExternalId === this.mainTypeId);
      if (mainType) {
        this.loadChildCategories('Type', mainType, 'types');
      }
    }

    this.loadBrands();
    this.page = 1;
    this.loadProducts();
  }

  onTypeChange(): void {
    this.subTypeId = '';
    this.subTypes = [];

    const type = this.types.find(c => c.erpExternalId === this.typeId);
    if (type) {
      this.loadChildCategories('SubType', type, 'subTypes');
    }

    this.loadBrands();
    this.page = 1;
    this.loadProducts();
  }

  onSubTypeChange(): void {
    this.loadBrands();
    this.page = 1;
    this.loadProducts();
  }

  categoryLabel(category: ErpCategory): string {
    return category.nameNl || category.nameFr || category.nameEn || category.erpExternalId;
  }

  productCategoryDisplay(p: ErpProduct): string {
    if (this.categoryRootsAreTypes) {
      return p.typeName || p.mainTypeName || p.subTypeName || '—';
    }
    const parts = [p.mainTypeName, p.typeName, p.subTypeName].filter(x => !!x && x.trim());
    return parts.length ? parts.join(' / ') : '—';
  }

  dropshipSupplierName(id: number | null | undefined): string {
    if (id == null) return '';
    return this.formSuppliers.find(s => s.id === id)?.name || `#${id}`;
  }

  onDropshipToggle(): void {
    if (!this.productForm.isDropship) this.productForm.dropshipSupplierId = null;
  }

  @HostListener('document:visibilitychange')
  onDocumentVisibilityChange(): void {
    if (document.visibilityState === 'hidden') {
      this.syncVehicleYearFromDraft();
    }
  }

  loadProducts(): void {
    this.syncVehicleYearFromDraft();

    this.loading = true;
    const cat = this.currentCategoryFilter();
    this.erpService.getProducts({
      page: this.page,
      pageSize: this.pageSize,
      q: this.searchQuery.trim() || undefined,
      brand: this.currentBrandFilter(),
      dataSource: this.hasErpCatalogSync ? (this.sourceFilter || undefined) : undefined,
      subTypeId: cat.subTypeId,
      typeId: cat.typeId,
      mainTypeId: cat.mainTypeId,
      vehicleBrand: this.showVehicleFilters ? (this.vehicleBrandFilter || undefined) : undefined,
      vehicleModel: this.showVehicleFilters ? (this.vehicleModelFilter || undefined) : undefined,
      vehicleYear: this.showVehicleFilters ? this.vehicleYearQueryParam() : undefined,
      vehicleFuel: this.showVehicleFilters ? (this.vehicleFuelFilter || undefined) : undefined,
      vehicleBody: this.showVehicleFilters ? (this.vehicleBodyFilter || undefined) : undefined,
      vehicleDrive: this.showVehicleFilters ? (this.vehicleDriveFilter || undefined) : undefined,
      vehicleTransmission: this.showVehicleFilters ? (this.vehicleTransmissionFilter || undefined) : undefined,
      vehicleEngine: this.showVehicleFilters ? (this.vehicleEngineFilter || undefined) : undefined,
      vehicleKType: this.vehicleKTypeFilter || undefined,
    }).subscribe({
      next: (res) => {
        this.products = res.items ?? [];
        this.total = res.total ?? 0;
        this.page = res.page ?? this.page;
        this.loading = false;
        if (this.pendingFocusProductId != null) {
          const focus = this.products.find(p => p.id === this.pendingFocusProductId);
          if (focus) this.selectProduct(focus);
          else if (this.products.length === 1) this.selectProduct(this.products[0]);
          this.pendingFocusProductId = null;
        } else if (this.selected) {
          const refreshed = this.products.find(p => p.id === this.selected!.id);
          if (refreshed) this.selected = refreshed;
        }
        if (this.productCreating || this.productEditing) {
          this.formBrands = this.mergeBrandOptions(this.formBrands.filter(b => b.id > 0));
        }
      },
      error: (err) => {
        console.error(err);
        this.loading = false;
        this.snack.open(this.i18n.t('erpProducts.snack.loadError'), this.i18n.t('common.close'), { duration: 3500 });
      }
    });
  }

  applyFilters(): void {
    this.page = 1;
    this.loadProducts();
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.brandFilter = '';
    this.sourceFilter = '';
    this.mainTypeId = '';
    this.typeId = '';
    this.subTypeId = '';
    this.types = [];
    this.subTypes = [];
    this.vehicleBrandFilter = '';
    this.vehicleModelFilter = '';
    this.vehicleYearFilter = null;
    this.vehicleYearDraft = '';
    this.vehicleFuelFilter = '';
    this.vehicleBodyFilter = '';
    this.vehicleDriveFilter = '';
    this.vehicleTransmissionFilter = '';
    this.vehicleEngineFilter = '';
    this.vehicleKTypeFilter = '';
    this.filterVehicleModels = [];
    this.filterVehicleFuels = [];
    this.filterVehicleBodies = [];
    this.filterVehicleDrives = [];
    this.filterVehicleTransmissions = [];
    this.categoryRootsAreTypes = false;
    this.page = 1;
    this.loadFilterOptions();
    this.loadProducts();
  }

  loadVehicleFilterBrands(): void {
    this.erpService.getVehicleMakes().subscribe({
      next: makes => this.filterVehicleBrands = makes ?? [],
      error: () => this.filterVehicleBrands = []
    });
    this.loadVehicleFacets();
  }

  loadVehicleFacets(): void {
    if (!this.showVehicleFilters) {
      this.filterVehicleFuels = [];
      this.filterVehicleBodies = [];
      this.filterVehicleDrives = [];
      this.filterVehicleTransmissions = [];
      return;
    }
    this.erpService.getVehicleFacets(
      this.vehicleBrandFilter || undefined,
      this.vehicleModelFilter || undefined
    ).subscribe({
      next: facets => {
        this.filterVehicleFuels = facets?.fuels ?? [];
        this.filterVehicleBodies = facets?.bodyTypes ?? [];
        this.filterVehicleDrives = facets?.driveTypes ?? [];
        this.filterVehicleTransmissions = facets?.transmissions ?? [];
      },
      error: () => {
        this.filterVehicleFuels = [];
        this.filterVehicleBodies = [];
        this.filterVehicleDrives = [];
        this.filterVehicleTransmissions = [];
      }
    });
  }

  /** @deprecated use loadVehicleFacets */
  loadVehicleFuelOptions(): void {
    this.loadVehicleFacets();
  }

  onVehicleBrandFilterChange(): void {
    this.vehicleKTypeFilter = '';
    this.vehicleModelFilter = '';
    this.filterVehicleModels = [];
    if (this.vehicleBrandFilter) {
      this.erpService.getVehicleModels(this.vehicleBrandFilter).subscribe({
        next: models => this.filterVehicleModels = models ?? [],
        error: () => this.filterVehicleModels = []
      });
    }
    this.loadVehicleFacets();
    this.applyFilters();
  }

  onVehicleModelFilterChange(): void {
    this.vehicleKTypeFilter = '';
    this.loadVehicleFacets();
    this.applyFilters();
  }

  onVehicleYearFilterChange(): void {
    this.vehicleKTypeFilter = '';
    this.vehicleYearFilter = this.normalizeVehicleYear(this.vehicleYearFilter);
    this.vehicleYearDraft = this.vehicleYearFilter != null ? String(this.vehicleYearFilter) : '';
    this.applyFilters();
  }

  onVehicleYearDraftChange(value: string | null): void {
    const digits = this.sanitizeVehicleYearDraft(value);
    this.vehicleYearDraft = digits;

    if (digits.length === 0) {
      this.vehicleYearFilter = null;
      this.vehicleKTypeFilter = '';
      this.applyFilters();
      return;
    }
    if (digits.length < 4) {
      if (this.vehicleYearFilter != null) {
        this.vehicleYearFilter = null;
        this.vehicleKTypeFilter = '';
        this.applyFilters();
      }
      return;
    }

    this.vehicleYearFilter = this.normalizeVehicleYear(Number(digits));
    this.vehicleKTypeFilter = '';
    this.applyFilters();
  }

  commitVehicleYearFilter(event?: Event): void {
    event?.preventDefault();
    event?.stopPropagation();
    this.captureVehicleYearFromInput(event);
    this.syncVehicleYearFromDraft();
    this.vehicleKTypeFilter = '';
    this.applyFilters();
  }

  /** Lit la valeur directement depuis l'input (ngModel pas encore à jour au keydown Enter). */
  private captureVehicleYearFromInput(event?: Event): void {
    const el = event?.target;
    if (!(el instanceof HTMLInputElement)) return;
    this.vehicleYearDraft = this.sanitizeVehicleYearDraft(el.value);
  }

  /** Applique le brouillon année au filtre actif (sans relancer la recherche). */
  private syncVehicleYearFromDraft(): void {
    const digits = this.vehicleYearDraftText();
    if (!digits) {
      this.vehicleYearFilter = null;
      return;
    }
    this.vehicleYearFilter = this.normalizeVehicleYear(Number(digits));
  }

  private sanitizeVehicleYearDraft(value: string | null | undefined): string {
    return (value ?? '').replace(/\D/g, '').slice(0, 4);
  }

  private vehicleYearDraftText(): string {
    return this.sanitizeVehicleYearDraft(this.vehicleYearDraft);
  }

  /** Paramètre API : année valide ou brute (4 chiffres) → backend renvoie 0 si invalide. */
  private vehicleYearQueryParam(): string | undefined {
    const digits = this.vehicleYearDraftText();
    if (digits.length >= 4) return digits;
    const y = this.normalizeVehicleYear(this.vehicleYearFilter);
    return y != null ? String(y) : undefined;
  }

  private normalizeVehicleYear(raw: number | null | undefined): number | null {
    if (raw == null) return null;
    const y = Math.trunc(Number(raw));
    if (!Number.isFinite(y) || y < 1950 || y > 2035) return null;
    return y;
  }

  private resolvedVehicleYear(): number | undefined {
    const fromFilter = this.normalizeVehicleYear(this.vehicleYearFilter);
    if (fromFilter != null) return fromFilter;
    const trimmed = this.vehicleYearDraftText();
    if (trimmed.length >= 4) {
      const fromDraft = this.normalizeVehicleYear(Number(trimmed));
      if (fromDraft != null) return fromDraft;
    }
    return undefined;
  }

  productVehicleLabel(p: ErpProduct): string {
    const make = p.vehicleMake?.trim() || '';
    const model = p.vehicleModel?.trim() || '';
    const type = p.vehicleTypeName?.trim() || '';
    const base = `${make} ${model}`.trim();
    if (!base && !type) return '—';
    if (type && type !== model) return `${base} ${type}`.trim();
    return base || type;
  }

  productVehicleYears(p: ErpProduct): string {
    const from = p.vehicleYearFrom ?? null;
    const to = p.vehicleYearTo ?? null;
    if (from && to && from !== to) return `${from}–${to}`;
    if (from) return String(from);
    if (to) return String(to);
    return '';
  }

  onVehicleFuelFilterChange(): void {
    this.applyFilters();
  }

  onVehicleSpecFilterChange(): void {
    this.applyFilters();
  }

  clearVehicleFilters(): void {
    this.vehicleBrandFilter = '';
    this.vehicleModelFilter = '';
    this.vehicleYearFilter = null;
    this.vehicleYearDraft = '';
    this.vehicleFuelFilter = '';
    this.vehicleBodyFilter = '';
    this.vehicleDriveFilter = '';
    this.vehicleTransmissionFilter = '';
    this.vehicleEngineFilter = '';
    this.vehicleKTypeFilter = '';
    this.filterVehicleModels = [];
    this.loadVehicleFacets();
    this.applyFilters();
  }

  get hasActiveVehicleFilters(): boolean {
    return !!(
      this.vehicleBrandFilter
      || this.vehicleModelFilter
      || this.vehicleYearFilter
      || this.vehicleYearDraftText()
      || this.vehicleFuelFilter
      || this.vehicleBodyFilter
      || this.vehicleDriveFilter
      || this.vehicleTransmissionFilter
      || this.vehicleEngineFilter
      || this.vehicleKTypeFilter
    );
  }

  get activeVehicleLabel(): string {
    const parts = [
      this.vehicleBrandFilter,
      this.vehicleModelFilter,
      this.resolvedVehicleYear() != null ? String(this.resolvedVehicleYear()) : '',
      this.vehicleFuelFilter,
      this.vehicleBodyFilter
    ].filter(Boolean);
    return parts.join(' · ') || '—';
  }

  triggerCatalogSync(): void {
    if (!this.hasSyncFilter || this.syncingAll || this.syncingId != null) return;

    this.startSyncTracking('catalog');
    this.snack.open(`Sync de ${this.total} produit(s) filtré(s)…`, undefined, { duration: 2500 });

    const category = this.currentCategoryFilter();
    this.erpService.syncCatalog({
      brand: this.currentBrandFilter(),
      mainTypeId: category.mainTypeId,
      typeId: category.typeId,
      subTypeId: category.subTypeId
    }, true).subscribe({
      next: (log) => this.watchSyncJob(log),
      error: (err) => {
        this.resetSyncTracking();
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync: ${detail}` : this.i18n.t('erpProducts.snack.syncFailed'),
          this.i18n.t('common.close'),
          { duration: 8000 }
        );
      }
    });
  }

  cancelSync(): void {
    this.erpService.cancelRunningSync().subscribe({
      next: () => {
        this.resetSyncTracking();
        this.snack.open(this.i18n.t('erpProducts.snack.syncCancelled'), this.i18n.t('common.ok'), { duration: 3000 });
      },
      error: () => {
        this.resetSyncTracking();
        this.snack.open('Sync arrêtée (ou déjà terminée)', this.i18n.t('common.ok'), { duration: 3000 });
      }
    });
  }

  triggerSyncAll(): void {
    if (this.syncingAll || this.syncingId != null) return;
    this.startSyncTracking('enrich');
    this.snack.open(this.i18n.t('erpProducts.snack.enrichStarted'), undefined, { duration: 2500 });
    this.erpService.syncAll().subscribe({
      next: (log) => this.watchSyncJob(log),
      error: (err) => {
        this.resetSyncTracking();
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync: ${detail}` : this.i18n.t('erpProducts.snack.syncFailed'),
          this.i18n.t('common.close'),
          { duration: 8000 }
        );
      }
    });
  }

  selectProduct(product: ErpProduct): void {
    this.selected = product;
    this.detailTab = 'info';
    this.productEditing = false;
    this.productCreating = false;
    this.galleryIndex = 0;
    this.galleryLightboxOpen = false;
    this.stopGalleryLoop();
    this.loadProductExtras(product.id);
    this.loadSupplierQuotes(product.id);
    this.loadProductDiagrams(product.id);
    if (product.isDropship && this.formSuppliers.length === 0) {
      this.businessService.getSuppliers().subscribe({
        next: items => { this.formSuppliers = items ?? []; },
        error: () => { this.formSuppliers = []; }
      });
    }
  }

  closeDetail(): void {
    this.selected = null;
    this.productEditing = false;
    this.productCreating = false;
    this.variants = [];
    this.images = [];
    this.productVehicles = [];
    this.productOems = [];
    this.detailVehicleMake = '';
    this.detailVehicleModel = '';
    this.detailVehicleQuery = '';
    this.attrValues = [];
    this.galleryLightboxOpen = false;
    this.stopGalleryLoop();
    this.supplierQuotes = null;
    this.productDiagrams = [];
    void this.supplierQuotesRealtime.unwatch();
  }

  startCreateProduct(): void {
    this.selected = null;
    this.productCreating = true;
    this.productEditing = true;
    this.detailTab = 'info';
    this.createTab = 'info';
    this.draftVariants = [];
    this.draftImages = [];
    this.draftVariantEditIndex = null;
    this.draftVariantSku = '';
    this.draftVariantBarcode = '';
    this.draftVariantCost = null;
    this.draftVariantPrice = null;
    this.draftVariantStock = 0;
    this.draftImageUrl = '';
    this.draftImageAlt = '';
    this.productForm = {
      name: '',
      reference: '',
      ean: '',
      purchasePrice: null,
      unitPrice: null,
      vatPercent: 21,
      brandId: null,
      brandName: '',
      mainTypeCatId: null,
      typeCatId: null,
      subTypeCatId: null,
      comment: '',
      weight: null,
      height: null,
      width: null,
      depth: null,
      isDropship: false,
      dropshipSupplierId: null
    };
    this.formTypes = [];
    this.formSubTypes = [];
    this.loadProductFormLookups();
  }

  startEditProduct(): void {
    if (!this.selected) return;
    this.productEditing = true;
    this.productCreating = false;
    this.detailTab = 'info';
    this.createTab = 'info';
    this.productForm = {
      name: this.selected.name ?? '',
      reference: this.selected.reference ?? '',
      ean: this.selected.ean ?? '',
      purchasePrice: this.selected.cPrice ?? null,
      unitPrice: this.selected.unitPrice ?? null,
      vatPercent: this.selected.typeVatPerc ?? 21,
      brandId: this.selected.brandId ?? null,
      brandName: this.selected.brand ?? '',
      mainTypeCatId: null,
      typeCatId: null,
      subTypeCatId: null,
      comment: this.selected.comment ?? '',
      weight: this.selected.weight ?? null,
      height: this.selected.height ?? null,
      width: this.selected.width ?? null,
      depth: this.selected.depth ?? null,
      isDropship: !!this.selected.isDropship,
      dropshipSupplierId: this.selected.dropshipSupplierId ?? null
    };
    this.formTypes = [];
    this.formSubTypes = [];
    this.loadProductFormLookups(() => {
      if (!this.productForm.brandId && this.selected?.brand) {
        const match = this.formBrands.find(
          b => b.name.localeCompare(this.selected!.brand!, undefined, { sensitivity: 'accent' }) === 0
        );
        if (match) this.productForm.brandId = match.id;
      }
      this.applyProductCategoryToForm();
    });
  }

  loadProductFormLookups(done?: () => void): void {
    let pending = 3;
    const finish = () => {
      pending -= 1;
      if (pending <= 0) done?.();
    };

    this.businessService.getSuppliers().subscribe({
      next: items => { this.formSuppliers = items ?? []; finish(); },
      error: () => { this.formSuppliers = []; finish(); }
    });

    const apply = (items: ErpBrand[]) => {
      this.formBrands = this.mergeBrandOptions((items ?? []).filter(b => !this.isVehicleBrand(b)));
      finish();
    };
    this.brandService.list().subscribe({
      next: items => {
        if (items?.length) {
          apply(items);
          return;
        }
        this.erpService.getBrands().subscribe({
          next: apply,
          error: () => apply([])
        });
      },
      error: () => {
        this.erpService.getBrands().subscribe({
          next: apply,
          error: () => apply([])
        });
      }
    });

    const applyCats = (items: ErpCategory[]) => {
      this.formMainTypes = items ?? [];
      finish();
    };
    if (this.categoryRootsAreTypes) {
      this.erpService.getCategories({ level: 'Type', flatCatalog: true }).subscribe({
        next: applyCats,
        error: () => applyCats([])
      });
      return;
    }
    this.categoryService.list({ level: 'MainType', activeOnly: true }).subscribe({
      next: applyCats,
      error: () => applyCats([])
    });
  }

  /** Fusionne API + filtre + marques présentes sur les produits de la page. */
  private mergeBrandOptions(fromApi: ErpBrand[]): ErpBrand[] {
    const map = new Map<string, ErpBrand>();
    let synthId = -1;
    const add = (b: ErpBrand | null | undefined) => {
      const name = (b?.name || '').trim();
      if (!name) return;
      const key = name.toLowerCase();
      const existing = map.get(key);
      const incomingId = b!.id;
      if (!existing) {
        map.set(key, {
          id: incomingId > 0 ? incomingId : synthId--,
          name,
          slug: b!.slug || '',
          isActive: b!.isActive !== false,
          logoUrl: b!.logoUrl,
          websiteUrl: b!.websiteUrl,
          description: b!.description
        });
        return;
      }
      if (existing.id <= 0 && incomingId > 0) {
        existing.id = incomingId;
        existing.slug = b!.slug || existing.slug;
      }
    };
    for (const b of fromApi) add(b);
    for (const b of this.brands) add(b);
    for (const p of this.products) {
      const name = (p.brand || '').trim();
      if (name) add({ id: 0, name, slug: '', isActive: true });
    }
    return [...map.values()].sort((a, b) => a.name.localeCompare(b.name));
  }

  onBrandSelectChange(): void {
    const match = this.formBrands.find(b => b.id === this.productForm.brandId);
    if (match) this.productForm.brandName = match.name;
  }

  /** Préremplit la cascade catégorie depuis CategoryId ou les IDs/noms ERP du produit. */
  private applyProductCategoryToForm(): void {
    const p = this.selected;
    if (!p) return;

    const applyIds = (mainId: number | null, typeId: number | null, subId: number | null) => {
      this.productForm.mainTypeCatId = mainId;
      this.formTypes = [];
      this.formSubTypes = [];
      this.productForm.typeCatId = null;
      this.productForm.subTypeCatId = null;
      if (!mainId) return;
      this.categoryService.list({ level: 'Type', parentId: mainId, activeOnly: true }).subscribe(types => {
        this.formTypes = types ?? [];
        this.productForm.typeCatId = typeId;
        if (!typeId) return;
        this.categoryService.list({ level: 'SubType', parentId: typeId, activeOnly: true }).subscribe(subs => {
          this.formSubTypes = subs ?? [];
          this.productForm.subTypeCatId = subId;
        });
      });
    };

    if (p.categoryId) {
      this.categoryService.getById(p.categoryId).subscribe({
        next: leaf => this.resolveCategoryChain(leaf, applyIds),
        error: () => this.matchCategoryByErpFields(p, applyIds)
      });
      return;
    }
    this.matchCategoryByErpFields(p, applyIds);
  }

  private resolveCategoryChain(
    leaf: ErpCategory,
    apply: (mainId: number | null, typeId: number | null, subId: number | null) => void
  ): void {
    const level = (leaf.level || '').toLowerCase();
    if (this.categoryRootsAreTypes) {
      apply(leaf.id, null, null);
      return;
    }
    if (level === 'maintype') {
      apply(leaf.id, null, null);
      return;
    }
    if (!leaf.parentId) {
      if (level === 'type') apply(null, leaf.id, null);
      else if (level === 'subtype') apply(null, null, leaf.id);
      else apply(null, null, null);
      return;
    }
    this.categoryService.getById(leaf.parentId).subscribe({
      next: parent => {
        const parentLevel = (parent.level || '').toLowerCase();
        if (level === 'subtype' && parentLevel === 'type') {
          if (parent.parentId) {
            this.categoryService.getById(parent.parentId).subscribe({
              next: main => apply(main.id, parent.id, leaf.id),
              error: () => apply(null, parent.id, leaf.id)
            });
          } else {
            apply(null, parent.id, leaf.id);
          }
        } else if (level === 'type' && parentLevel === 'maintype') {
          apply(parent.id, leaf.id, null);
        } else {
          apply(null, null, leaf.id);
        }
      },
      error: () => apply(null, null, leaf.id)
    });
  }

  private matchCategoryByErpFields(
    p: ErpProduct,
    apply: (mainId: number | null, typeId: number | null, subId: number | null) => void
  ): void {
    if (this.categoryRootsAreTypes) {
      const typeExt = (p.typeID || p.mainTypeID || '').trim();
      const match = this.findCategoryMatch(this.formMainTypes, typeExt, p.typeName || p.mainTypeName);
      apply(match?.id ?? null, null, null);
      return;
    }

    const mainExt = (p.mainTypeID || '').trim();
    const typeExt = (p.typeID || '').trim();
    const subExt = (p.subTypeID || '').trim();

    this.categoryService.list({ level: 'MainType', activeOnly: true }).subscribe(mains => {
      this.formMainTypes = mains ?? [];
      const main = this.findCategoryMatch(this.formMainTypes, mainExt, p.mainTypeName);
      if (!main) {
        apply(null, null, null);
        return;
      }
      this.categoryService.list({ level: 'Type', parentId: main.id, activeOnly: true }).subscribe(types => {
        this.formTypes = types ?? [];
        const type = this.findCategoryMatch(this.formTypes, typeExt, p.typeName);
        if (!type) {
          apply(main.id, null, null);
          return;
        }
        this.categoryService.list({ level: 'SubType', parentId: type.id, activeOnly: true }).subscribe(subs => {
          this.formSubTypes = subs ?? [];
          const sub = this.findCategoryMatch(this.formSubTypes, subExt, p.subTypeName);
          apply(main.id, type.id, sub?.id ?? null);
        });
      });
    });
  }

  private findCategoryMatch(list: ErpCategory[], erpId: string, name?: string | null): ErpCategory | undefined {
    if (erpId) {
      const byId = list.find(c => (c.erpExternalId || '').localeCompare(erpId, undefined, { sensitivity: 'accent' }) === 0);
      if (byId) return byId;
    }
    const n = (name || '').trim().toLowerCase();
    if (!n) return undefined;
    return list.find(c =>
      [c.nameFr, c.nameNl, c.nameEn].some(x => (x || '').trim().toLowerCase() === n)
    );
  }

  onFormMainTypeChange(): void {
    if (this.categoryRootsAreTypes) return;
    this.productForm.typeCatId = null;
    this.productForm.subTypeCatId = null;
    this.formTypes = [];
    this.formSubTypes = [];
    if (!this.productForm.mainTypeCatId) return;
    this.categoryService.list({ level: 'Type', parentId: this.productForm.mainTypeCatId, activeOnly: true })
      .subscribe(items => this.formTypes = items ?? []);
  }

  onFormTypeChange(): void {
    this.productForm.subTypeCatId = null;
    this.formSubTypes = [];
    if (!this.productForm.typeCatId) return;
    this.categoryService.list({ level: 'SubType', parentId: this.productForm.typeCatId, activeOnly: true })
      .subscribe(items => this.formSubTypes = items ?? []);
  }

  resolveFormCategoryId(): number | undefined {
    return this.productForm.subTypeCatId
      ?? this.productForm.typeCatId
      ?? this.productForm.mainTypeCatId
      ?? undefined;
  }

  quickAddBrand(): void {
    if (!this.perm.hasAny(Permissions.BrandCreate, Permissions.ProductCreate)) return;
    const name = prompt(this.i18n.t('catalog.products.brandPrompt'));
    if (!name?.trim()) return;
    this.brandService.create({ name: name.trim(), isActive: true }).subscribe({
      next: created => {
        this.formBrands = [...this.formBrands, created].sort((a, b) => a.name.localeCompare(b.name));
        this.productForm.brandId = created.id;
        this.productForm.brandName = created.name;
        this.loadBrands();
        this.snack.open(this.i18n.t('catalog.brands.new'), undefined, { duration: 1500 });
      },
      error: err => this.snack.open(err?.error?.error || this.i18n.t('catalog.brands.saveError'), undefined, { duration: 3500 })
    });
  }

  quickAddCategory(level: 'MainType' | 'Type' | 'SubType'): void {
    if (!this.perm.hasAny(Permissions.CategoryCreate, Permissions.ProductCreate)) return;
    const flatType = this.categoryRootsAreTypes && level === 'Type';
    if (level === 'Type' && !flatType && !this.productForm.mainTypeCatId) {
      this.snack.open(this.i18n.t('catalog.products.pickMainFirst'), undefined, { duration: 2500 });
      return;
    }
    if (level === 'SubType' && !this.productForm.typeCatId) {
      this.snack.open(this.i18n.t('catalog.products.pickTypeFirst'), undefined, { duration: 2500 });
      return;
    }
    const name = prompt(this.i18n.t('catalog.products.categoryPrompt'));
    if (!name?.trim()) return;
    const parentId = level === 'MainType' || flatType
      ? null
      : (level === 'Type' ? this.productForm.mainTypeCatId : this.productForm.typeCatId);
    this.categoryService.create({
      level,
      parentId,
      nameFr: name.trim(),
      nameNl: name.trim(),
      nameEn: name.trim(),
      sortOrder: 0,
      isActive: true
    }).subscribe({
      next: created => {
        if (level === 'MainType' || flatType) {
          this.formMainTypes = [...this.formMainTypes, created];
          this.productForm.mainTypeCatId = created.id;
          if (!flatType) this.onFormMainTypeChange();
        } else if (level === 'Type') {
          this.formTypes = [...this.formTypes, created];
          this.productForm.typeCatId = created.id;
          this.onFormTypeChange();
        } else {
          this.formSubTypes = [...this.formSubTypes, created];
          this.productForm.subTypeCatId = created.id;
        }
        this.snack.open(this.i18n.t('catalog.categories.new'), undefined, { duration: 1500 });
      },
      error: err => this.snack.open(err?.error?.error || this.i18n.t('catalog.categories.saveError'), undefined, { duration: 3500 })
    });
  }

  cancelProductEdit(): void {
    if (this.productCreating) {
      this.productCreating = false;
      this.productEditing = false;
      this.draftVariants = [];
      this.draftImages = [];
    } else {
      this.productEditing = false;
    }
  }

  addDraftVariant(): void {
    const sku = this.draftVariantSku.trim();
    if (!sku) {
      this.snack.open(this.i18n.t('catalog.variants.sku'), undefined, { duration: 2000 });
      return;
    }
    if (this.draftVariantPrice == null || this.draftVariantPrice < 0) {
      this.snack.open(this.i18n.t('catalog.products.variantPriceRequired'), undefined, { duration: 2500 });
      return;
    }
    const dup = this.draftVariants.some((v, i) =>
      v.sku.toLowerCase() === sku.toLowerCase() && i !== this.draftVariantEditIndex
    );
    if (dup) {
      this.snack.open(this.i18n.t('catalog.products.variantSkuDup'), undefined, { duration: 2500 });
      return;
    }
    const row = {
      sku,
      barcode: this.draftVariantBarcode.trim(),
      costPrice: this.draftVariantCost,
      priceOverride: this.draftVariantPrice,
      stockQuantity: this.draftVariantStock || 0,
      attributesJson: '{}'
    };
    if (this.draftVariantEditIndex != null) {
      this.draftVariants[this.draftVariantEditIndex] = row;
      this.draftVariantEditIndex = null;
    } else {
      this.draftVariants.push(row);
    }
    this.draftVariantSku = '';
    this.draftVariantBarcode = '';
    this.draftVariantCost = null;
    this.draftVariantPrice = null;
    this.draftVariantStock = 0;
  }

  editDraftVariant(index: number): void {
    const v = this.draftVariants[index];
    if (!v) return;
    this.draftVariantEditIndex = index;
    this.draftVariantSku = v.sku;
    this.draftVariantBarcode = v.barcode;
    this.draftVariantCost = v.costPrice;
    this.draftVariantPrice = v.priceOverride;
    this.draftVariantStock = v.stockQuantity;
  }

  removeDraftVariant(index: number): void {
    this.draftVariants.splice(index, 1);
    if (this.draftVariantEditIndex === index) {
      this.draftVariantEditIndex = null;
      this.draftVariantSku = '';
      this.draftVariantBarcode = '';
      this.draftVariantCost = null;
      this.draftVariantPrice = null;
      this.draftVariantStock = 0;
    } else if (this.draftVariantEditIndex != null && this.draftVariantEditIndex > index) {
      this.draftVariantEditIndex--;
    }
  }

  addDraftImage(): void {
    const url = this.draftImageUrl.trim();
    if (!url) {
      this.snack.open(this.i18n.t('catalog.images.url'), undefined, { duration: 2000 });
      return;
    }
    this.draftImages.push({
      url,
      altText: this.draftImageAlt.trim(),
      isMain: this.draftImages.length === 0,
      sortOrder: this.draftImages.length
    });
    this.draftImageUrl = '';
    this.draftImageAlt = '';
  }

  removeDraftImage(index: number): void {
    this.draftImages.splice(index, 1);
    if (this.draftImages.length && !this.draftImages.some(i => i.isMain)) {
      this.draftImages[0].isMain = true;
    }
  }

  setDraftImageMain(index: number): void {
    this.draftImages.forEach((img, i) => img.isMain = i === index);
  }

  private persistDraftExtras(productId: number, done: () => void): void {
    const jobs: Observable<unknown>[] = [
      ...this.draftVariants.map(v =>
        this.extras.createVariant({
          productId,
          sku: v.sku,
          barcode: v.barcode || null,
          costPrice: v.costPrice,
          priceOverride: v.priceOverride,
          stockQuantity: v.stockQuantity,
          attributesJson: v.attributesJson || '{}',
          isActive: true
        })
      ),
      ...this.draftImages.map(img =>
        this.extras.createImage({
          productId,
          url: img.url,
          altText: img.altText,
          isMain: img.isMain,
          sortOrder: img.sortOrder
        })
      )
    ];
    if (!jobs.length) {
      done();
      return;
    }
    forkJoin(jobs.map(j => j.pipe(catchError(() => of({ __failed: true }))))).subscribe({
      next: results => {
        if (results.some(r => r && typeof r === 'object' && '__failed' in (r as object))) {
          this.snack.open(this.i18n.t('catalog.products.extrasPartialError'), undefined, { duration: 4000 });
        }
        done();
      },
      error: () => {
        this.snack.open(this.i18n.t('catalog.products.extrasPartialError'), undefined, { duration: 4000 });
        done();
      }
    });
  }

  saveProduct(): void {
    if (!this.productForm.name?.trim() && !this.productForm.reference?.trim() && !this.productForm.ean?.trim()) {
      this.snack.open(this.i18n.t('catalog.products.nameRequired'), undefined, { duration: 3000 });
      return;
    }
    if (this.productCreating && this.draftVariants.length === 0) {
      this.createTab = 'variants';
      this.snack.open(this.i18n.t('catalog.products.needVariantForPriceMode'), undefined, { duration: 3000 });
      return;
    }
    if (this.productCreating && this.draftVariants.some(v => v.priceOverride == null)) {
      this.createTab = 'variants';
      this.snack.open(this.i18n.t('catalog.products.variantPriceRequired'), undefined, { duration: 3000 });
      return;
    }
    const brandName = this.productForm.brandId && this.productForm.brandId > 0
      ? (this.formBrands.find(b => b.id === this.productForm.brandId)?.name ?? this.productForm.brandName)
      : (this.productForm.brandName || undefined);
    const brandId = this.productForm.brandId && this.productForm.brandId > 0
      ? this.productForm.brandId
      : undefined;
    const categoryId = this.resolveFormCategoryId();
    this.productSaving = true;
    if (this.productCreating) {
      const first = this.draftVariants[0];
      this.erpService.createProduct({
        name: this.productForm.name || undefined,
        reference: this.productForm.reference || undefined,
        ean: this.productForm.ean || undefined,
        purchasePrice: first?.costPrice ?? undefined,
        unitPrice: first?.priceOverride ?? undefined,
        vatPercent: this.productForm.vatPercent,
        brandId,
        brandName,
        categoryId,
        isDropship: this.productForm.isDropship,
        dropshipSupplierId: this.productForm.isDropship ? this.productForm.dropshipSupplierId : null
      }).subscribe({
        next: res => {
          const product = res.product;
          if (!product) {
            this.productSaving = false;
            this.snack.open(res.message || this.i18n.t('catalog.products.saveError'), undefined, { duration: 3000 });
            return;
          }
          this.persistDraftExtras(product.id, () => {
            this.productSaving = false;
            this.productCreating = false;
            this.productEditing = false;
            this.draftVariants = [];
            this.draftImages = [];
            this.loadProducts();
            this.selectProduct(product);
            this.snack.open(res.message || this.i18n.t('catalog.products.saved'), undefined, { duration: 2500 });
          });
        },
        error: err => {
          this.productSaving = false;
          this.snack.open(err?.error?.message || this.i18n.t('catalog.products.saveError'), undefined, { duration: 4000 });
        }
      });
      return;
    }
    if (!this.selected) return;
    this.erpService.updateProduct(this.selected.id, {
      name: this.productForm.name,
      reference: this.productForm.reference,
      ean: this.productForm.ean,
      purchasePrice: this.productForm.purchasePrice ?? undefined,
      unitPrice: this.productForm.unitPrice ?? undefined,
      vatPercent: this.productForm.vatPercent,
      brandId: this.productForm.brandId ?? undefined,
      brandName,
      categoryId,
      comment: this.productForm.comment,
      weight: this.productForm.weight,
      height: this.productForm.height,
      width: this.productForm.width,
      depth: this.productForm.depth,
      isDropship: this.productForm.isDropship,
      dropshipSupplierId: this.productForm.isDropship ? this.productForm.dropshipSupplierId : null
    }).subscribe({
      next: updated => {
        this.productSaving = false;
        this.productEditing = false;
        const idx = this.products.findIndex(p => p.id === updated.id);
        if (idx >= 0) this.products[idx] = updated;
        this.selected = updated;
        this.snack.open(this.i18n.t('catalog.products.saved'), undefined, { duration: 2000 });
      },
      error: err => {
        this.productSaving = false;
        this.snack.open(err?.error?.message || this.i18n.t('catalog.products.saveError'), undefined, { duration: 4000 });
      }
    });
  }

  archiveProduct(product: ErpProduct, event?: Event): void {
    event?.stopPropagation();
    if (!confirm(this.i18n.t('catalog.products.confirmArchive'))) return;
    this.erpService.archiveProduct(product.id).subscribe({
      next: () => {
        if (this.selected?.id === product.id) this.closeDetail();
        this.loadProducts();
        this.snack.open(this.i18n.t('catalog.products.archived'), undefined, { duration: 2000 });
      },
      error: err => this.snack.open(err?.error?.message || 'Error', undefined, { duration: 3000 })
    });
  }

  setDetailTab(tab: 'info' | 'variants' | 'images' | 'attributes' | 'vehicles' | 'oems' | 'suppliers' | 'diagram'): void {
    this.detailTab = tab;
    if (tab === 'suppliers' && this.selected) {
      this.loadSupplierQuotes(this.selected.id);
    }
    if (tab === 'diagram' && this.selected) {
      this.loadProductDiagrams(this.selected.id);
    }
  }

  loadSupplierQuotes(productId: number): void {
    this.supplierQuotesLoading = true;
    this.supplierQuotesApi.get(productId).subscribe({
      next: result => {
        this.supplierQuotes = result;
        this.supplierQuotesLoading = false;
        void this.supplierQuotesRealtime.watch(productId);
      },
      error: () => {
        this.supplierQuotes = null;
        this.supplierQuotesLoading = false;
      }
    });
  }

  refreshSupplierQuotes(): void {
    if (!this.selected) return;
    this.supplierQuotesLoading = true;
    this.supplierQuotesApi.refresh(this.selected.id).subscribe({
      next: result => {
        this.supplierQuotes = result;
        this.supplierQuotesLoading = false;
      },
      error: () => {
        this.supplierQuotesLoading = false;
      }
    });
  }

  quoteScoreLabel(reason?: string | null): string {
    if (reason === 'stock_local') return this.i18n.t('catalog.quotes.reasonLocal');
    if (reason === 'lowest_price') return this.i18n.t('catalog.quotes.reasonPrice');
    return '';
  }

  loadProductDiagrams(productId: number): void {
    this.productDiagramsLoading = true;
    this.productDiagramsApi.getByProduct(productId).subscribe({
      next: list => {
        this.productDiagrams = list ?? [];
        this.productDiagramsLoading = false;
      },
      error: () => {
        this.productDiagrams = [];
        this.productDiagramsLoading = false;
      }
    });
  }

  diagramImage(url: string): SafeUrl | string {
    if (url?.startsWith('data:')) {
      return this.sanitizer.bypassSecurityTrustUrl(url);
    }
    return url;
  }

  openDiagramPart(hotspot: DiagramHotspot): void {
    if (!hotspot?.targetProductId) return;
    this.erpService.getById(hotspot.targetProductId).subscribe({
      next: product => this.selectProduct(product),
      error: () => this.snack.open(this.i18n.t('catalog.diagram.openError'), undefined, { duration: 2500 })
    });
  }

  loadProductExtras(productId: number): void {
    this.extrasLoading = true;
    this.editingVariantId = null;
    this.variantForm = { sku: '', stockQuantity: 0, attributesJson: '{}', isActive: true };
    this.imageForm = { url: '', altText: '', isMain: false, sortOrder: 0 };
    this.productVehicles = [];
    this.productOems = [];
    this.detailVehicleMake = '';
    this.detailVehicleModel = '';
    this.detailVehicleQuery = '';
    this.extras.getVariants(productId).subscribe({
      next: v => this.variants = v ?? [],
      error: () => this.variants = []
    });
    this.extras.getImages(productId).subscribe({
      next: i => {
        this.images = i ?? [];
        this.galleryIndex = 0;
        this.startGalleryLoop();
      },
      error: () => {
        this.images = [];
        this.galleryIndex = 0;
        this.startGalleryLoop();
      }
    });
    this.erpService.getProductVehicles(productId).subscribe({
      next: v => this.productVehicles = v ?? [],
      error: () => this.productVehicles = []
    });
    this.erpService.getProductOems(productId).subscribe({
      next: o => this.productOems = o ?? [],
      error: () => this.productOems = []
    });
    this.extras.getAttributeDefinitions().subscribe({
      next: defs => {
        this.attrDefs = (defs ?? []).filter(d => d.isActive);
        this.extras.getAttributeValues(productId).subscribe({
          next: vals => {
            this.attrValues = vals ?? [];
            this.attrValueDrafts = {};
            for (const d of this.attrDefs) {
              this.attrValueDrafts[d.id] = this.attrValues.find(v => v.attributeId === d.id)?.value ?? '';
            }
            this.syncVehicleCompatFromAttributes();
            this.extrasLoading = false;
          },
          error: () => { this.attrValues = []; this.extrasLoading = false; }
        });
      },
      error: () => { this.attrDefs = []; this.extrasLoading = false; }
    });
  }

  vehicleYearsLabel(v: ErpProductVehicle): string {
    const from = v.yearFrom != null ? String(v.yearFrom) : '…';
    const to = v.yearTo != null ? String(v.yearTo) : '…';
    if (v.yearFrom == null && v.yearTo == null) return '—';
    return `${from} – ${to}`;
  }

  vehiclePowerLabel(v: ErpProductVehicle): string {
    const parts: string[] = [];
    if (v.powerKW != null) parts.push(`${v.powerKW} kW`);
    if (v.powerHP != null) parts.push(`${v.powerHP} ch`);
    if (v.ccm != null) parts.push(`${v.ccm} cm³`);
    return parts.length ? parts.join(' · ') : '';
  }

  get detailVehicleMakes(): string[] {
    return [...new Set(this.productVehicles.map(v => v.make).filter(Boolean))]
      .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
  }

  get detailVehicleModels(): string[] {
    const make = (this.detailVehicleMake || '').trim().toLowerCase();
    const source = make
      ? this.productVehicles.filter(v => (v.make || '').toLowerCase() === make)
      : this.productVehicles;
    return [...new Set(source.map(v => v.model).filter(Boolean))]
      .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
  }

  get filteredProductVehicles(): ErpProductVehicle[] {
    const make = (this.detailVehicleMake || '').trim().toLowerCase();
    const model = (this.detailVehicleModel || '').trim().toLowerCase();
    const q = (this.detailVehicleQuery || '').trim().toLowerCase();
    return this.productVehicles.filter(v => {
      if (make && (v.make || '').toLowerCase() !== make) return false;
      if (model && (v.model || '').toLowerCase() !== model) return false;
      if (!q) return true;
      const hay = [
        v.make, v.model, v.typeName, v.engineCode, v.fuelType, v.bodyType,
        v.driveType, v.transmission, v.kType,
        v.powerKW != null ? String(v.powerKW) : '',
        v.powerHP != null ? String(v.powerHP) : '',
        v.ccm != null ? String(v.ccm) : ''
      ].join(' ').toLowerCase();
      return hay.includes(q);
    });
  }

  /** Groupes Make → Model pour affichage compact du détail. */
  get productVehicleGroups(): { make: string; model: string; items: ErpProductVehicle[] }[] {
    const map = new Map<string, ErpProductVehicle[]>();
    for (const v of this.filteredProductVehicles) {
      const key = `${v.make || ''}|||${v.model || ''}`;
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(v);
    }
    return Array.from(map.entries()).map(([key, items]) => {
      const [make, model] = key.split('|||');
      return { make, model, items };
    });
  }

  onDetailVehicleMakeChange(): void {
    this.detailVehicleModel = '';
  }

  clearDetailVehicleFilters(): void {
    this.detailVehicleMake = '';
    this.detailVehicleModel = '';
    this.detailVehicleQuery = '';
  }

  startEditVariant(v: ErpProductVariant): void {
    this.editingVariantId = v.id;
    this.variantForm = {
      sku: v.sku,
      barcode: v.barcode ?? '',
      priceOverride: v.priceOverride ?? null,
      costPrice: v.costPrice ?? null,
      stockQuantity: v.stockQuantity ?? 0,
      attributesJson: v.attributesJson || '{}',
      isActive: v.isActive !== false
    };
  }

  cancelVariantEdit(): void {
    this.editingVariantId = null;
    this.variantForm = { sku: '', stockQuantity: 0, attributesJson: '{}', isActive: true };
  }

  saveVariant(): void {
    if (!this.selected || !this.variantForm.sku?.trim()) return;
    this.extrasSaving = true;
    const body = {
      productId: this.selected.id,
      sku: this.variantForm.sku.trim(),
      barcode: this.variantForm.barcode || null,
      priceOverride: this.variantForm.priceOverride ?? null,
      costPrice: this.variantForm.costPrice ?? null,
      stockQuantity: this.variantForm.stockQuantity ?? 0,
      attributesJson: this.variantForm.attributesJson || '{}',
      isActive: this.variantForm.isActive !== false
    };
    const req$ = this.editingVariantId
      ? this.extras.updateVariant(this.editingVariantId, body)
      : this.extras.createVariant(body);
    req$.subscribe({
      next: () => {
        this.extrasSaving = false;
        this.editingVariantId = null;
        this.loadProductExtras(this.selected!.id);
        this.snack.open(this.i18n.t('catalog.variants.saved'), undefined, { duration: 2000 });
      },
      error: err => {
        this.extrasSaving = false;
        this.snack.open(err?.error?.error || this.i18n.t('catalog.variants.saveError'), undefined, { duration: 4000 });
      }
    });
  }

  deleteVariant(id: string): void {
    if (!this.selected || !confirm(this.i18n.t('catalog.variants.confirmDelete'))) return;
    this.extras.deleteVariant(id).subscribe(() => {
      if (this.editingVariantId === id) this.cancelVariantEdit();
      this.loadProductExtras(this.selected!.id);
    });
  }

  saveImage(): void {
    if (!this.selected || !this.imageForm.url?.trim()) return;
    this.extrasSaving = true;
    this.extras.createImage({
      productId: this.selected.id,
      url: this.imageForm.url.trim(),
      altText: this.imageForm.altText || '',
      isMain: !!this.imageForm.isMain,
      sortOrder: this.imageForm.sortOrder ?? 0
    }).subscribe({
      next: () => {
        this.extrasSaving = false;
        this.loadProductExtras(this.selected!.id);
      },
      error: err => {
        this.extrasSaving = false;
        this.snack.open(err?.error?.error || this.i18n.t('catalog.images.saveError'), undefined, { duration: 4000 });
      }
    });
  }

  deleteImage(id: string): void {
    if (!this.selected) return;
    this.extras.deleteImage(id).subscribe(() => this.loadProductExtras(this.selected!.id));
  }

  saveAttrValue(attributeId: string): void {
    if (!this.selected) return;
    this.extras.upsertAttributeValue({
      productId: this.selected.id,
      attributeId,
      value: this.attrValueDrafts[attributeId] ?? ''
    }).subscribe({
      next: () => this.snack.open(this.i18n.t('common.save'), undefined, { duration: 1500 }),
      error: err => this.snack.open(err?.error?.error || 'Error', undefined, { duration: 3000 })
    });
  }

  createAttrDefinition(): void {
    const code = prompt(this.i18n.t('catalog.attributes.codePrompt'));
    if (!code?.trim()) return;
    const name = prompt(this.i18n.t('catalog.attributes.namePrompt'), code) || code;
    this.extras.createAttributeDefinition({ code: code.trim(), name: name.trim(), isActive: true }).subscribe({
      next: () => this.selected && this.loadProductExtras(this.selected.id),
      error: err => this.snack.open(err?.error?.error || 'Error', undefined, { duration: 3000 })
    });
  }

  syncProduct(product: ErpProduct, event?: Event): void {
    event?.stopPropagation();
    if (this.syncingId != null) return;

    this.syncingId = product.id;
    this.erpService.syncProduct(product).subscribe({
      next: (updated) => {
        this.syncingId = null;
        const idx = this.products.findIndex(p => p.id === product.id);
        if (idx >= 0) this.products[idx] = { ...this.products[idx], ...updated };
        if (this.selected?.id === product.id) this.selected = { ...this.selected, ...updated };
        this.snack.open(
          this.i18n.t('erpProducts.snack.productSyncOk', {
            name: updated.name || updated.reference || updated.erpProductId
          }),
          this.i18n.t('common.ok'),
          { duration: 3000 }
        );
      },
      error: (err) => {
        this.syncingId = null;
        const detail = err?.error?.detail || err?.error?.message || err?.message;
        this.snack.open(
          detail ? `Échec sync: ${detail}` : 'Échec sync ERP pour ce produit',
          'Fermer',
          { duration: 8000 }
        );
      }
    });
  }

  private watchSyncJob(log: ErpSyncLog): void {
    const details = this.parseSyncDetails(log);
    // Ancien job fantôme LocalEnrich massif (ex. 15k figés) — pas FullCatalog.
    const looksLikeStaleMassLocal =
      details.mode !== 'FullCatalog'
      && (log.processedProducts ?? 0) > 0
      && log.status === 'Running'
      && (log.totalProducts > this.total * 10 && this.total > 0);

    if (details.mode === 'CatalogFilter' || looksLikeStaleMassLocal) {
      this.snack.open(
        'Ancienne sync ERP détectée (job fantôme). Annulation… Relancez la sync.',
        'Fermer',
        { duration: 10000 }
      );
      this.erpService.cancelRunningSync().subscribe({
        next: () => this.resetSyncTracking(),
        error: () => this.resetSyncTracking()
      });
      return;
    }

    this.syncProgress = log;
    this.stopSyncPoll();

    if (log.status !== 'Running') {
      this.onSyncFinished(log);
      return;
    }

    this.syncPollSub = timer(0, 1500).pipe(
      switchMap(() => this.erpService.getSyncLog(log.jobId)),
      takeWhile((current) => current.status === 'Running', true)
    ).subscribe({
      next: (current) => {
        this.syncProgress = current;
        if (current.status !== 'Running') {
          this.onSyncFinished(current);
        }
      },
      error: () => {
        this.resetSyncTracking();
        this.stopSyncPoll();
        this.snack.open('Impossible de suivre la progression du sync', 'Fermer', { duration: 4000 });
      }
    });
  }

  private onSyncFinished(log: ErpSyncLog): void {
    this.syncingAll = false;
    this.stopSyncPoll();
    this.syncProgress = log;
    this.snack.open(
      `Sync ${log.status}: +${log.newProducts} créés, ${log.updatedProducts} maj, ${log.failedProducts} échecs`,
      'OK',
      { duration: 6000 }
    );
    this.loadFilterOptions();
    this.loadProducts();
    this.syncMode = null;
    this.syncFilterLabel = '';
  }

  private stopSyncPoll(): void {
    this.syncPollSub?.unsubscribe();
    this.syncPollSub = null;
  }

  prevPage(): void {
    if (this.page <= 1) return;
    this.page -= 1;
    this.loadProducts();
  }

  nextPage(): void {
    if (this.page * this.pageSize >= this.total) return;
    this.page += 1;
    this.loadProducts();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    return new Date(value).toLocaleString(this.i18n.numberLocale(), {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatDimensions(p: ErpProduct): string {
    const l = p.depth;
    const w = p.width;
    const h = p.height;
    if (l == null && w == null && h == null) return '—';
    const fmt = (v: number | null | undefined) => (v == null ? '—' : String(v));
    return `${fmt(l)} × ${fmt(w)} × ${fmt(h)} mm`;
  }

  formatPrice(value?: number | null): string {
    if (value == null) return '—';
    return value.toLocaleString(this.i18n.numberLocale(), { minimumFractionDigits: 2, maximumFractionDigits: 4 });
  }

  /** Prix vente HT : PriceHT s'il est fiable, sinon dérivé du TTC catalogue. */
  salePriceHt(product: ErpProduct): number | null {
    const vat = Number(product.typeVatPerc ?? 21);
    const ttc = product.unitPrice ?? product.rPrice;
    const ht = product.priceHT;
    const cost = product.cPrice;
    if (ht != null && (cost == null || Math.abs(ht - cost) > 0.0001)) {
      return ht;
    }
    if (ttc != null && vat > 0) {
      return +(ttc / (1 + vat / 100)).toFixed(4);
    }
    return ht ?? null;
  }

  sourceClass(source?: string | null): string {
    switch (source) {
      case 'Excel': return 'src-excel';
      case 'Merged': return 'src-merged';
      case 'Erp': return 'src-erp';
      case 'CarApi': return 'src-carapi';
      case 'RapidApi': return 'src-rapidapi';
      default: return 'src-unknown';
    }
  }

  isSynced(product: ErpProduct): boolean {
    return !!product.lastSyncAt;
  }

  /** Miniatures : URL absolue RapidAPI/S3 telle quelle ; sinon proxy ERP local (PicName fichier). */
  productImageUrl(product: ErpProduct | null | undefined): string | null {
    return this.normalizeMediaSrc(product?.picName);
  }

  isPdfUrl(url: string | null | undefined): boolean {
    if (!url?.trim()) return false;
    const raw = url.trim().split('?')[0].split('#')[0].toLowerCase();
    if (raw.endsWith('.pdf')) return true;
    // Proxy EuroBrico : ?f=fichier.pdf
    try {
      const q = new URL(url, 'http://local').searchParams.get('f');
      if (q && q.toLowerCase().split('?')[0].endsWith('.pdf')) return true;
    } catch { /* ignore */ }
    return /\.pdf$/i.test(decodeURIComponent(raw));
  }

  /** Médias galerie (images + PDF). */
  get galleryItems(): Array<{ url: string; kind: 'image' | 'pdf' }> {
    const items: Array<{ url: string; kind: 'image' | 'pdf' }> = [];
    const seen = new Set<string>();
    const add = (raw: string | null | undefined) => {
      const url = this.normalizeMediaSrc(raw);
      if (!url || seen.has(url)) return;
      seen.add(url);
      items.push({ url, kind: this.isPdfUrl(raw) || this.isPdfUrl(url) ? 'pdf' : 'image' });
    };

    const sorted = [...this.images].sort((a, b) => {
      if (a.isMain !== b.isMain) return a.isMain ? -1 : 1;
      return (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
    });
    for (const img of sorted) add(img.url);
    add(this.selected?.picName);
    return items;
  }

  get galleryUrls(): string[] {
    return this.galleryItems.map(i => i.url);
  }

  get currentGalleryItem(): { url: string; kind: 'image' | 'pdf' } | null {
    const items = this.galleryItems;
    if (!items.length) return null;
    return items[((this.galleryIndex % items.length) + items.length) % items.length];
  }

  get currentGalleryUrl(): string | null {
    return this.currentGalleryItem?.url ?? null;
  }

  get currentGalleryIsPdf(): boolean {
    return this.currentGalleryItem?.kind === 'pdf';
  }

  get galleryCount(): number {
    return this.galleryItems.length;
  }

  normalizeMediaSrc(raw: string | null | undefined): string | null {
    if (!raw?.trim()) return null;
    let file = raw.trim().replace(/\\/g, '/');
    if (/^https?:\/\//i.test(file)) return file;
    file = file.replace(/^\/+/, '');
    const slash = file.lastIndexOf('/');
    if (slash >= 0) file = file.slice(slash + 1);
    if (!file || file.includes('..')) return null;
    const api = (environment.apiBaseUrl ?? '/api').replace(/\/+$/, '');
    // PDF : lien direct proxy image (sert aussi les fichiers) ou URL telle quelle
    return `${api}/erp-products/image?f=${encodeURIComponent(file)}`;
  }

  /** @deprecated use normalizeMediaSrc */
  normalizeImageSrc(raw: string | null | undefined): string | null {
    return this.normalizeMediaSrc(raw);
  }

  startGalleryLoop(): void {
    this.stopGalleryLoop();
    if (this.galleryItems.length <= 1) return;
    this.galleryLoopSub = interval(3200).subscribe(() => {
      if (this.galleryPaused && !this.galleryLightboxOpen) return;
      this.nextGalleryImage();
    });
  }

  stopGalleryLoop(): void {
    this.galleryLoopSub?.unsubscribe();
    this.galleryLoopSub = null;
  }

  nextGalleryImage(): void {
    const n = this.galleryItems.length;
    if (n <= 0) return;
    this.galleryIndex = (this.galleryIndex + 1) % n;
  }

  prevGalleryImage(): void {
    const n = this.galleryItems.length;
    if (n <= 0) return;
    this.galleryIndex = (this.galleryIndex - 1 + n) % n;
  }

  setGalleryIndex(index: number): void {
    const n = this.galleryItems.length;
    if (n <= 0) return;
    this.galleryIndex = ((index % n) + n) % n;
  }

  openGalleryLightbox(index?: number): void {
    if (!this.galleryItems.length) return;
    if (index != null) this.setGalleryIndex(index);
    const item = this.currentGalleryItem;
    if (item?.kind === 'pdf') {
      window.open(item.url, '_blank', 'noopener');
      return;
    }
    this.galleryLightboxOpen = true;
  }

  closeGalleryLightbox(): void {
    this.galleryLightboxOpen = false;
  }

  openGalleryMedia(item: { url: string; kind: 'image' | 'pdf' }, index: number): void {
    this.setGalleryIndex(index);
    if (item.kind === 'pdf') {
      window.open(item.url, '_blank', 'noopener');
      return;
    }
    this.openGalleryLightbox(index);
  }

  onGalleryMainError(event: Event): void {
    const img = event.target as HTMLImageElement | null;
    if (!img) return;
    if (this.galleryCount > 1) {
      this.nextGalleryImage();
      return;
    }
    img.style.visibility = 'hidden';
  }

  onProductImageError(event: Event): void {
    const img = event.target as HTMLImageElement | null;
    if (!img) return;
    img.style.display = 'none';
    const parent = img.parentElement;
    if (parent && !parent.querySelector('.product-thumb.placeholder') && !parent.querySelector('.gallery-placeholder')) {
      const ph = document.createElement('div');
      ph.className = img.classList.contains('detail') || parent.classList.contains('gallery-stage')
        ? 'product-thumb detail placeholder gallery-placeholder'
        : 'product-thumb placeholder';
      ph.textContent = '—';
      parent.appendChild(ph);
    }
  }

  initVehicleCompatibility(): void {
    if (!this.showVehicleFilters) return;
    if (this.vehicleBrands.length) return;
    this.carApi.getBrands().subscribe({
      next: brands => this.vehicleBrands = brands ?? [],
      error: () => this.vehicleBrands = []
    });
    this.carApi.ensureVehicleAttribute().subscribe({
      next: def => {
        this.vehicleCompatDef = def;
        if (!this.attrDefs.some(d => d.id === def.id)) {
          this.attrDefs = [...this.attrDefs, def];
        }
      }
    });
  }

  onVehicleBrandChange(): void {
    this.vehiclePick.model = '';
    this.vehiclePick.generation = '';
    this.vehiclePick.yearFrom = null;
    this.vehiclePick.yearTo = null;
    this.vehicleModels = [];
    this.vehicleGenerations = [];
    if (!this.vehiclePick.brand) return;
    this.carApi.getModels(this.vehiclePick.brand).subscribe({
      next: models => this.vehicleModels = models ?? [],
      error: () => this.vehicleModels = []
    });
  }

  onVehicleModelChange(): void {
    this.vehiclePick.generation = '';
    this.vehiclePick.yearFrom = null;
    this.vehiclePick.yearTo = null;
    this.vehicleGenerations = [];
    if (!this.vehiclePick.brand || !this.vehiclePick.model) return;
    this.carApi.getGenerations(this.vehiclePick.brand, this.vehiclePick.model).subscribe({
      next: gens => this.vehicleGenerations = gens ?? [],
      error: () => this.vehicleGenerations = []
    });
  }

  onVehicleGenerationChange(): void {
    const gen = this.vehicleGenerations.find(g => g.name === this.vehiclePick.generation);
    this.vehiclePick.yearFrom = gen?.yearFrom ?? null;
    this.vehiclePick.yearTo = gen?.yearTo ?? null;
  }

  addVehicleCompatibility(): void {
    if (!this.vehiclePick.brand || !this.vehiclePick.model) return;
    const entry: VehicleCompatibilityEntry = {
      brand: this.vehiclePick.brand,
      model: this.vehiclePick.model,
      generation: this.vehiclePick.generation || '—',
      yearFrom: this.vehiclePick.yearFrom,
      yearTo: this.vehiclePick.yearTo
    };
    const key = `${entry.brand}|${entry.model}|${entry.generation}`;
    if (this.vehicleCompatList.some(v => `${v.brand}|${v.model}|${v.generation}` === key)) return;
    this.vehicleCompatList = [...this.vehicleCompatList, entry];
  }

  removeVehicleCompatibility(index: number): void {
    this.vehicleCompatList = this.vehicleCompatList.filter((_, i) => i !== index);
  }

  saveVehicleCompatibility(): void {
    if (!this.selected || !this.vehicleCompatDef) return;
    const value = JSON.stringify(this.vehicleCompatList);
    this.extras.upsertAttributeValue({
      productId: this.selected.id,
      attributeId: this.vehicleCompatDef.id,
      value
    }).subscribe({
      next: () => {
        this.attrValueDrafts[this.vehicleCompatDef!.id] = value;
        this.snack.open(this.i18n.t('catalog.vehicleCompat.saved'), undefined, { duration: 2000 });
      },
      error: err => this.snack.open(err?.error?.error || 'Error', undefined, { duration: 3000 })
    });
  }

  vehicleCompatYearsLabel(entry: VehicleCompatibilityEntry): string {
    const from = entry.yearFrom ?? '?';
    const to = entry.yearTo ?? '…';
    return `${from} – ${to}`;
  }

    private syncVehicleCompatFromAttributes(): void {
    this.vehicleCompatDef = this.attrDefs.find(d => d.code === this.vehicleCompatCode) ?? null;
    const raw = this.vehicleCompatDef
      ? this.attrValueDrafts[this.vehicleCompatDef.id]
      : '';
    this.vehicleCompatList = this.parseVehicleCompat(raw);
    this.initVehicleCompatibility();
  }

  private parseVehicleCompat(raw?: string | null): VehicleCompatibilityEntry[] {
    if (!raw?.trim()) return [];
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }
}
