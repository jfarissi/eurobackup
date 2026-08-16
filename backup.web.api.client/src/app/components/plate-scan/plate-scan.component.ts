import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';

import { CommonModule } from '@angular/common';

import { FormsModule } from '@angular/forms';

import { RouterModule } from '@angular/router';

import { MaterialModule } from '../../material.module';

import { finalize, interval, Subscription, switchMap, takeWhile, tap } from 'rxjs';

import {

  KTypeSyncProgress,

  PlateHistoryItem,

  PlateScanResult,

  PlateScanService,

  RapidApiCategory

} from '../../services/plate-scan.service';

import { CompanyService } from '../../services/company.service';

import { AppI18nService } from '../../services/app-i18n.service';

import { TPipe } from '../../pipes/t.pipe';

import { CatalogSubnavComponent } from '../shared/catalog-subnav/catalog-subnav.component';



type ScanMode = 'camera' | 'manual' | 'vin';



@Component({

  selector: 'app-plate-scan',

  standalone: true,

  imports: [CommonModule, FormsModule, RouterModule, MaterialModule, TPipe, CatalogSubnavComponent],

  templateUrl: './plate-scan.component.html',

  styleUrls: ['./plate-scan.component.css']

})

export class PlateScanComponent implements OnInit, OnDestroy {

  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;



  mode: ScanMode = 'manual';

  scanning = false;

  isDragging = false;

  previewUrl: string | null = null;

  selectedFile: File | null = null;

  error = '';

  result: PlateScanResult | null = null;

  history: PlateHistoryItem[] = [];

  syncProgress: KTypeSyncProgress | null = null;
  syncRefreshing = false;
  catsLoading = false;
  catFilter = '';
  categories: RapidApiCategory[] = [];
  categoryGroups: { family: string; label: string; items: RapidApiCategory[] }[] = [];
  selectedCatIds = new Set<number>();
  expandedFamilies = new Set<string>();



  /** Défaut marché Maroc. */

  manualPlate = '';

  manualCountry = 'MA';

  manualVin = '';

  /** VIN saisi pour associer une plaque inconnue (scénario B). */

  linkVin = '';



  private progressSub?: Subscription;

  private pendingVinRefresh = false;

  private pendingPlateRefresh = false;

  private pendingPlateNumber = '';

  private pendingPlateCountry = 'MA';



  readonly countries = [

    { code: 'MA', labelKey: 'plate.country.MA' },

    { code: 'FR', labelKey: 'plate.country.FR' },

    { code: 'BE', labelKey: 'plate.country.BE' },

    { code: 'DZ', labelKey: 'plate.country.DZ' },

    { code: 'TN', labelKey: 'plate.country.TN' },

    { code: 'NL', labelKey: 'plate.country.NL' }

  ];



  constructor(

    private plateScan: PlateScanService,

    public company: CompanyService,

    private i18n: AppI18nService

  ) {}



  ngOnInit(): void {

    this.loadHistory();

  }



  ngOnDestroy(): void {

    this.stopProgressPolling(true);

  }



  get moduleMissing(): boolean {

    return this.company.modules.length > 0 && !this.company.hasAutoParts;

  }



  get catalogSyncActive(): boolean {
    return !!this.syncProgress && this.syncProgress.status === 'Running';
  }

  /** Barre visible pendant tout le flux sync + rafraîchissement résultats. */
  get syncUiVisible(): boolean {
    return this.catalogSyncActive
      || this.syncRefreshing
      || (this.syncProgress?.status === 'Done' || this.syncProgress?.status === 'Failed');
  }

  get selectedCatCount(): number {
    return this.selectedCatIds.size;
  }

  trackByFamily = (_: number, g: { family: string }): string => g.family;

  trackByCatId = (_: number, c: RapidApiCategory): number => c.id;

  onCatFilterChange(): void {
    this.rebuildCategoryGroups();
  }



  onDragOver(event: DragEvent): void {

    event.preventDefault();

    this.isDragging = true;

  }



  onDragLeave(event: DragEvent): void {

    event.preventDefault();

    this.isDragging = false;

  }



  onDrop(event: DragEvent): void {

    event.preventDefault();

    this.isDragging = false;

    const file = event.dataTransfer?.files?.[0];

    if (file) this.setPreview(file);

  }



  onFileSelected(event: Event): void {

    const input = event.target as HTMLInputElement;

    const file = input.files?.[0];

    if (file) this.setPreview(file);

  }



  clearPreview(): void {

    this.previewUrl = null;

    this.selectedFile = null;

    if (this.fileInput?.nativeElement) this.fileInput.nativeElement.value = '';

  }



  scanImage(): void {

    if (!this.selectedFile) return;

    this.beginScan();

    this.plateScan.scanPlate(this.selectedFile).pipe(

      finalize(() => this.endScanUnlessSyncing())

    ).subscribe({

      next: (r) => this.handleScanResult(r),

      error: (err) => this.error = this.readError(err)

    });

  }



  searchManual(): void {

    if (!this.manualPlate.trim()) return;

    this.beginScan();

    this.plateScan.searchByPlate(this.manualPlate.trim(), this.manualCountry || 'MA').pipe(

      finalize(() => this.endScanUnlessSyncing())

    ).subscribe({

      next: (r) => this.handleScanResult(r),

      error: (err) => this.error = this.readError(err)

    });

  }



  searchVin(): void {

    if (this.manualVin.trim().length !== 17) return;

    this.beginScan();

    this.plateScan.searchByVin(this.manualVin.trim()).pipe(

      finalize(() => this.endScanUnlessSyncing())

    ).subscribe({

      next: (r) => this.handleScanResult(r),

      error: (err) => this.error = this.readError(err)

    });

  }



  linkPlateVin(): void {

    if (!this.result?.needsVehicleLink || !this.result.plateNumber) return;

    const vin = this.linkVin.trim().toUpperCase();

    if (vin.length !== 17) {

      this.error = this.i18n.t('plate.link.vinRequired');

      return;

    }

    this.beginScan();

    this.plateScan.linkPlateToVin({

      plate: this.result.plateNumber,

      country: this.result.country || this.manualCountry || 'MA',

      vin

    }).pipe(

      finalize(() => this.endScanUnlessSyncing())

    ).subscribe({

      next: (r) => {

        this.handleScanResult(r);

        this.linkVin = '';

      },

      error: (err) => this.error = this.readError(err)

    });

  }



  reuseHistory(item: PlateHistoryItem): void {
    const plate = (item.plateNumber || '').trim();
    const vin = (item.vin || '').trim().toUpperCase();
    const looksLikeVin =
      /^[A-HJ-NPR-Z0-9]{17}$/i.test(plate)
      || (!!vin && vin.length === 17 && (!plate || plate.toUpperCase() === vin));

    if (looksLikeVin) {
      this.mode = 'vin';
      this.manualVin = (vin || plate).toUpperCase();
      this.searchVin();
      return;
    }

    this.mode = 'manual';
    this.manualPlate = plate;
    this.manualCountry = item.country || 'MA';
    this.searchManual();
  }



  openProductsForVehicle(): void {

    if (!this.result?.make) return;

    void this.i18n;

  }



  vehicleLabel(p: { vehicleMake?: string; vehicleModel?: string; vehicleTypeName?: string }): string {

    const make = p.vehicleMake || this.result?.make || '';

    const model = p.vehicleModel || this.result?.model || '';

    const type = p.vehicleTypeName || '';

    const base = `${make} ${model}`.trim();

    if (type && type !== model) return `${base} ${type}`.trim();

    return base || '—';

  }



  yearsLabel(p: { yearFrom?: number; yearTo?: number }): string {

    const from = p.yearFrom ?? this.result?.year ?? null;

    const to = p.yearTo ?? null;

    if (from && to && from !== to) return `${from}–${to}`;

    if (from) return String(from);

    if (to) return String(to);

    return '—';

  }

  isDiesel(fuel?: string | null): boolean {
    return (fuel || '').toLowerCase().includes('diesel') || (fuel || '').toLowerCase().includes('gazole');
  }

  isPetrol(fuel?: string | null): boolean {
    const f = (fuel || '').toLowerCase();
    return f.includes('essence') || f.includes('petrol') || f.includes('gasoline');
  }



  private beginScan(): void {

    this.scanning = true;

    this.error = '';

    this.result = null;

    this.stopProgressPolling(true);

    this.syncProgress = null;
    this.categories = [];
    this.categoryGroups = [];
    this.selectedCatIds = new Set();
    this.expandedFamilies = new Set();
    this.catFilter = '';
    this.catsLoading = false;

  }



  private endScanUnlessSyncing(): void {
    if (this.catalogSyncActive || this.syncRefreshing || this.pendingVinRefresh || this.pendingPlateRefresh) {
      return;
    }
    this.scanning = false;
  }



  private handleScanResult(r: PlateScanResult, opts?: { skipSyncPoll?: boolean }): void {
    this.result = r;
    this.loadHistory();

    if (opts?.skipSyncPoll) {
      this.scanning = false;
      this.syncRefreshing = false;
      this.syncProgress = null;
      if (r.kType) this.loadCategories(r.kType);
      return;
    }

    const shouldTrackSync = !!r.kType && !!r.kTypeSyncInProgress;

    if (shouldTrackSync) {
      this.pendingVinRefresh = this.mode === 'vin' && this.manualVin.trim().length === 17;
      this.pendingPlateRefresh =
        (this.mode === 'manual' && !!this.manualPlate.trim()) ||
        (this.mode === 'camera' && !!r.plateNumber);
      this.pendingPlateNumber = r.plateNumber || this.manualPlate.trim();
      this.pendingPlateCountry = r.country || this.manualCountry || 'MA';
      this.startProgressPolling(r.kType!);
      return;
    }

    this.syncProgress = null;
    this.syncRefreshing = false;
    this.scanning = false;
    if (r.kType) {
      this.loadCategories(r.kType);
    }
  }

  private loadCategories(kType: string): void {
    this.catsLoading = true;
    this.plateScan.listKTypeCategories(kType).pipe(
      finalize(() => this.catsLoading = false)
    ).subscribe({
      next: (list) => {
        this.categories = list.categories || [];
        this.rebuildCategoryGroups();
      },
      error: () => {
        this.categories = [];
        this.categoryGroups = [];
        this.error = this.i18n.t('plate.cats.loadFailed');
      }
    });
  }

  isCatSelected(id: number): boolean {
    return this.selectedCatIds.has(id);
  }

  toggleCat(id: number, checked: boolean): void {
    if (checked) this.selectedCatIds.add(id);
    else this.selectedCatIds.delete(id);
  }

  isFamilyFullySelected(items: RapidApiCategory[]): boolean {
    return items.length > 0 && items.every(c => this.selectedCatIds.has(c.id));
  }

  isFamilyPartiallySelected(items: RapidApiCategory[]): boolean {
    const n = this.countFamilySelected(items);
    return n > 0 && n < items.length;
  }

  countFamilySelected(items: RapidApiCategory[]): number {
    return items.filter(c => this.selectedCatIds.has(c.id)).length;
  }

  isFamilyOpen(family: string): boolean {
    return !!this.catFilter.trim() || this.expandedFamilies.has(family);
  }

  toggleFamilyOpen(family: string): void {
    if (this.expandedFamilies.has(family)) this.expandedFamilies.delete(family);
    else this.expandedFamilies.add(family);
  }

  toggleFamily(items: RapidApiCategory[], checked: boolean): void {
    for (const c of items) {
      if (checked) this.selectedCatIds.add(c.id);
      else this.selectedCatIds.delete(c.id);
    }
  }

  private rebuildCategoryGroups(): void {
    const q = this.catFilter.trim().toLowerCase();
    const filtered = q
      ? this.categories.filter(c =>
          c.name.toLowerCase().includes(q)
          || (c.parentName || '').toLowerCase().includes(q)
          || c.familyLabel.toLowerCase().includes(q))
      : this.categories;
    const map = new Map<string, { family: string; label: string; items: RapidApiCategory[] }>();
    for (const c of filtered) {
      const g = map.get(c.family) ?? { family: c.family, label: c.familyLabel, items: [] };
      g.items.push(c);
      map.set(c.family, g);
    }
    this.categoryGroups = [...map.values()];
  }

  importSelectedCategories(): void {
    if (!this.result?.kType || this.selectedCatIds.size === 0 || this.scanning) return;
    this.error = '';
    this.scanning = true;
    this.plateScan.importKTypeCategories({
      kType: this.result.kType,
      make: this.result.make,
      model: this.result.model,
      year: this.result.year,
      vin: this.result.vin || this.manualVin.trim() || undefined,
      fuelType: this.result.fuelType || undefined,
      categoryIds: [...this.selectedCatIds]
    }).subscribe({
      next: (res) => {
        if (!res?.syncInProgress) {
          this.scanning = false;
          this.error = res?.message || this.i18n.t('plate.sync.failed');
          return;
        }
        this.pendingVinRefresh = this.mode === 'vin' && this.manualVin.trim().length === 17;
        this.pendingPlateRefresh =
          (this.mode === 'manual' && !!this.manualPlate.trim()) ||
          (this.mode === 'camera' && !!this.result?.plateNumber);
        this.pendingPlateNumber = this.result?.plateNumber || this.manualPlate.trim();
        this.pendingPlateCountry = this.result?.country || this.manualCountry || 'MA';
        this.startProgressPolling(this.result!.kType!);
      },
      error: (err) => {
        this.scanning = false;
        this.error = this.readError(err);
      }
    });
  }

  private startProgressPolling(kType: string): void {
    this.stopProgressPolling(false);
    this.scanning = true;
    this.syncRefreshing = false;
    this.syncProgress = {
      kType,
      status: 'Running',
      phase: 'start',
      current: 0,
      total: 1,
      percent: 0,
      message: this.i18n.t('plate.sync.message')
    };

    let idlePolls = 0;
    this.progressSub = interval(500).pipe(
      switchMap(() => this.plateScan.getKTypeSyncProgress(kType)),
      tap((p) => {
        if (p.status === 'Idle') {
          idlePolls += 1;
          this.syncProgress = {
            ...this.syncProgress!,
            status: 'Running',
            message: this.syncProgress?.message || this.i18n.t('plate.sync.message')
          };
        } else {
          idlePolls = 0;
          this.syncProgress = p;
        }
      }),
      takeWhile((p) => {
        if (p.status === 'Running') return true;
        if (p.status === 'Idle') return idlePolls < 40;
        return false;
      }, true)
    ).subscribe({
      next: (p) => {
        if (p.status === 'Done') {
          this.refreshAfterSync();
        } else if (p.status === 'Failed') {
          this.finishSyncUi(p.message || this.i18n.t('plate.sync.failed'), true);
        } else if (p.status === 'Idle' && idlePolls >= 40) {
          this.finishSyncUi(this.i18n.t('plate.sync.failed'), true);
        }
      },
      error: () => this.finishSyncUi(this.i18n.t('plate.sync.failed'), true)
    });
  }

  private refreshAfterSync(): void {
    this.stopProgressPolling(false);

    if (this.pendingVinRefresh && this.manualVin.trim().length === 17) {
      this.syncRefreshing = true;
      this.syncProgress = {
        kType: this.result?.kType || '',
        status: 'Running',
        phase: 'refresh',
        current: 1,
        total: 1,
        percent: 100,
        message: this.i18n.t('plate.sync.refresh')
      };
      this.plateScan.searchByVin(this.manualVin.trim()).pipe(
        finalize(() => this.finishSyncUi())
      ).subscribe({
        next: (r) => this.handleScanResult(r, { skipSyncPoll: true }),
        error: (err) => this.finishSyncUi(this.readError(err), true)
      });
      return;
    }

    if (this.pendingPlateRefresh && this.pendingPlateNumber) {
      this.syncRefreshing = true;
      this.syncProgress = {
        kType: this.result?.kType || '',
        status: 'Running',
        phase: 'refresh',
        current: 1,
        total: 1,
        percent: 100,
        message: this.i18n.t('plate.sync.refresh')
      };
      this.plateScan.searchByPlate(this.pendingPlateNumber, this.pendingPlateCountry).pipe(
        finalize(() => this.finishSyncUi())
      ).subscribe({
        next: (r) => this.handleScanResult(r, { skipSyncPoll: true }),
        error: (err) => this.finishSyncUi(this.readError(err), true)
      });
      return;
    }

    this.finishSyncUi();
  }

  private finishSyncUi(errorMessage?: string, isError = false): void {
    this.scanning = false;
    this.syncRefreshing = false;
    this.syncProgress = null;
    this.pendingVinRefresh = false;
    this.pendingPlateRefresh = false;
    this.pendingPlateNumber = '';
    if (isError && errorMessage) {
      this.error = errorMessage;
    }
  }

  private stopProgressPolling(clearPending = false): void {
    this.progressSub?.unsubscribe();
    this.progressSub = undefined;
    if (clearPending) {
      this.pendingVinRefresh = false;
      this.pendingPlateRefresh = false;
      this.pendingPlateNumber = '';
    }
  }



  private setPreview(file: File): void {

    if (!file.type.startsWith('image/')) {

      this.error = this.i18n.t('plate.error.notImage');

      return;

    }

    if (file.size > 5 * 1024 * 1024) {

      this.error = this.i18n.t('plate.error.tooLarge');

      return;

    }

    this.error = '';

    this.selectedFile = file;

    const reader = new FileReader();

    reader.onload = () => this.previewUrl = reader.result as string;

    reader.readAsDataURL(file);

  }



  private loadHistory(): void {

    if (this.moduleMissing) {

      this.history = [];

      return;

    }

    this.plateScan.getHistory(15).subscribe({

      next: (rows) => this.history = rows || [],

      error: () => this.history = []

    });

  }



  private readError(err: unknown): string {

    const e = err as { error?: string | { error?: string } };

    if (typeof e?.error === 'string') return e.error;

    if (e?.error && typeof e.error === 'object' && e.error.error) return e.error.error;

    return this.i18n.t('plate.error.generic');

  }

}

