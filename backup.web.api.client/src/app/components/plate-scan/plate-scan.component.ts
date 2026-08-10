import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { finalize } from 'rxjs';
import {
  PlateHistoryItem,
  PlateScanResult,
  PlateScanService
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
export class PlateScanComponent implements OnInit {
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  mode: ScanMode = 'manual';
  scanning = false;
  isDragging = false;
  previewUrl: string | null = null;
  selectedFile: File | null = null;
  error = '';
  result: PlateScanResult | null = null;
  history: PlateHistoryItem[] = [];

  /** Défaut marché Maroc. */
  manualPlate = '';
  manualCountry = 'MA';
  manualVin = '';

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

  get moduleMissing(): boolean {
    return this.company.modules.length > 0 && !this.company.hasAutoParts;
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
    this.scanning = true;
    this.error = '';
    this.result = null;
    this.plateScan.scanPlate(this.selectedFile).pipe(
      finalize(() => this.scanning = false)
    ).subscribe({
      next: (r) => {
        this.result = r;
        this.loadHistory();
      },
      error: (err) => this.error = this.readError(err)
    });
  }

  searchManual(): void {
    if (!this.manualPlate.trim()) return;
    this.scanning = true;
    this.error = '';
    this.result = null;
    this.plateScan.searchByPlate(this.manualPlate.trim(), this.manualCountry || 'MA').pipe(
      finalize(() => this.scanning = false)
    ).subscribe({
      next: (r) => {
        this.result = r;
        this.loadHistory();
      },
      error: (err) => this.error = this.readError(err)
    });
  }

  searchVin(): void {
    if (this.manualVin.trim().length !== 17) return;
    this.scanning = true;
    this.error = '';
    this.result = null;
    this.plateScan.searchByVin(this.manualVin.trim()).pipe(
      finalize(() => this.scanning = false)
    ).subscribe({
      next: (r) => {
        this.result = r;
        this.loadHistory();
      },
      error: (err) => this.error = this.readError(err)
    });
  }

  reuseHistory(item: PlateHistoryItem): void {
    this.mode = 'manual';
    this.manualPlate = item.plateNumber;
    this.manualCountry = item.country || 'MA';
    this.searchManual();
  }

  openProductsForVehicle(): void {
    if (!this.result?.make) return;
    void this.i18n;
    // Navigation via routerLink in template preferred
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
