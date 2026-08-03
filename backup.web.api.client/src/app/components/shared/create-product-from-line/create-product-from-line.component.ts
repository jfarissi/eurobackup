import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../../material.module';
import { ErpBrand, ErpCategory, CreateErpProductRequest, CreateErpProductResult } from '../../../models/erp-product';
import { ErpProductService } from '../../../services/erp-product.service';
import { AppI18nService } from '../../../services/app-i18n.service';
import { TPipe } from '../../../pipes/t.pipe';
import { FormHelpComponent } from '../form-help/form-help.component';

export interface CreateProductLineDraft {
  name?: string | null;
  reference?: string | null;
  ean?: string | null;
  purchasePrice?: number | null;
  supplierName?: string | null;
}

@Component({
  selector: 'app-create-product-from-line',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './create-product-from-line.component.html',
  styleUrls: ['./create-product-from-line.component.css']
})
export class CreateProductFromLineComponent implements OnInit, OnChanges {
  @Input() open = false;
  @Input() draft: CreateProductLineDraft | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() created = new EventEmitter<CreateErpProductResult>();

  name = '';
  reference = '';
  ean = '';
  purchasePrice: number | null = null;
  vatPercent = 21;
  supplierName = '';

  brands: ErpBrand[] = [];
  brandId: number | null = null;
  brandToken: string | null = null;

  mainTypes: ErpCategory[] = [];
  types: ErpCategory[] = [];
  subTypes: ErpCategory[] = [];
  mainTypeId: number | null = null;
  typeId: number | null = null;
  subTypeId: number | null = null;

  saving = false;
  error = '';

  constructor(
    private erp: ErpProductService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.erp.getCategories({ level: 'MainType' }).subscribe(items => this.mainTypes = items ?? []);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] || changes['draft']) {
      if (this.open) {
        this.applyDraft();
        this.loadBrandSuggestion();
      }
    }
  }

  categoryLabel(c: ErpCategory): string {
    return c.nameFr || c.nameNl || c.nameEn || c.erpExternalId;
  }

  onMainTypeChange(): void {
    this.typeId = null;
    this.subTypeId = null;
    this.types = [];
    this.subTypes = [];
    if (!this.mainTypeId) return;
    this.erp.getCategories({ level: 'Type', parentId: this.mainTypeId }).subscribe(items => this.types = items ?? []);
  }

  onTypeChange(): void {
    this.subTypeId = null;
    this.subTypes = [];
    if (!this.typeId) return;
    this.erp.getCategories({ level: 'SubType', parentId: this.typeId }).subscribe(items => this.subTypes = items ?? []);
  }

  close(): void {
    this.closed.emit();
  }

  save(): void {
    this.error = '';
    if (!this.name.trim() && !this.reference.trim() && !this.ean.trim()) {
      this.error = this.i18n.t('createProduct.error.identity');
      return;
    }

    const categoryId = this.subTypeId || this.typeId || this.mainTypeId || undefined;
    const payload: CreateErpProductRequest = {
      name: this.name.trim() || undefined,
      reference: this.reference.trim() || undefined,
      ean: this.ean.trim() || undefined,
      purchasePrice: this.purchasePrice ?? undefined,
      vatPercent: this.vatPercent,
      brandId: this.brandId || undefined,
      categoryId,
      supplierName: this.supplierName.trim() || undefined
    };

    this.saving = true;
    this.erp.createProduct(payload).subscribe({
      next: (res) => {
        this.saving = false;
        this.created.emit(res);
        this.closed.emit();
      },
      error: (err) => {
        this.saving = false;
        this.error = err?.error?.message || err?.error || this.i18n.t('createProduct.error.save');
      }
    });
  }

  loadBrandSuggestion(): void {
    if (!this.supplierName) {
      this.erp.getBrands().subscribe(b => this.brands = b ?? []);
      return;
    }
    this.erp.suggestBrand({ supplierName: this.supplierName }).subscribe({
      next: (res) => {
        this.brandToken = res.token;
        this.brands = (res.brands || []).map(b => ({
          id: b.id,
          name: b.name,
          slug: b.slug || '',
          isActive: b.isActive !== false
        }));
        this.brandId = res.suggestedBrandId ?? (this.brands.length === 1 ? this.brands[0].id : null);
        if (this.brands.length === 0) {
          this.erp.getBrands().subscribe(all => this.brands = all ?? []);
        }
      },
      error: () => this.erp.getBrands().subscribe(b => this.brands = b ?? [])
    });
  }

  private applyDraft(): void {
    this.error = '';
    this.name = this.draft?.name?.trim() || '';
    this.reference = this.draft?.reference?.trim() || '';
    this.ean = this.draft?.ean?.trim() || '';
    this.purchasePrice = this.draft?.purchasePrice ?? null;
    this.supplierName = this.draft?.supplierName?.trim() || '';
    this.brandId = null;
    this.brandToken = null;
    this.brands = [];
    this.mainTypeId = null;
    this.typeId = null;
    this.subTypeId = null;
    this.types = [];
    this.subTypes = [];
  }
}
