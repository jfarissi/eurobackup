import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../material.module';
import { ErpBrand } from '../../models/erp-product';
import { ErpBrandForm, ErpBrandService } from '../../services/erp-brand.service';
import { CarApiService } from '../../services/car-api.service';
import { CompanyService } from '../../services/company.service';
import { ErpProductService } from '../../services/erp-product.service';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { TPipe } from '../../pipes/t.pipe';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { CatalogSubnavComponent } from '../shared/catalog-subnav/catalog-subnav.component';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppI18nService } from '../../services/app-i18n.service';

@Component({
  selector: 'app-erp-brands',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent, CatalogSubnavComponent],
  templateUrl: './erp-brands.component.html',
  styleUrls: ['./erp-brands.component.css']
})
export class ErpBrandsComponent implements OnInit {
  readonly P = Permissions;
  brands: ErpBrand[] = [];
  allBrands: ErpBrand[] = [];
  brandFilter: 'all' | 'vehicle' | 'supplier' = 'all';
  loading = false;
  importingVehicle = false;
  saving = false;
  error = '';
  selected: ErpBrand | null = null;
  editing = false;
  form: ErpBrandForm = this.emptyForm();

  constructor(
    private brandService: ErpBrandService,
    private carApi: CarApiService,
    private erpService: ErpProductService,
    public company: CompanyService,
    public perm: PermissionService,
    private i18n: AppI18nService,
    private snack: MatSnackBar
  ) {}

  get canCreate(): boolean {
    return this.perm.hasAny(Permissions.BrandCreate, Permissions.ProductCreate);
  }
  get canUpdate(): boolean {
    return this.perm.hasAny(Permissions.BrandUpdate, Permissions.ProductUpdate);
  }
  get canDelete(): boolean {
    return this.perm.hasAny(Permissions.BrandDelete, Permissions.ProductDelete);
  }

  ngOnInit(): void {
    if (!this.company.hasErpCatalogSync) {
      this.brandFilter = 'vehicle';
    }
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.brandService.list().subscribe({
      next: items => {
        this.allBrands = items ?? [];
        if (this.allBrands.length) {
          this.applyBrandFilter();
          this.loading = false;
          return;
        }
        this.loadVehicleBrandsFallback();
      },
      error: () => this.loadVehicleBrandsFallback()
    });
  }

  private loadVehicleBrandsFallback(): void {
    this.carApi.getBrands().subscribe({
      next: items => {
        this.allBrands = (items ?? []).map((b, i) => ({
          id: -(i + 1),
          name: b.brand,
          slug: b.brand.toLowerCase().replace(/\s+/g, '-'),
          isActive: true,
          description: 'Marque véhicule (catalogue car-api)'
        }));
        this.applyBrandFilter();
        this.loading = false;
      },
      error: () => {
        this.error = 'catalog.brands.loadError';
        this.allBrands = [];
        this.brands = [];
        this.loading = false;
      }
    });
  }

  setBrandFilter(filter: 'all' | 'vehicle' | 'supplier'): void {
    this.brandFilter = filter;
    this.applyBrandFilter();
  }

  private applyBrandFilter(): void {
    const isVehicle = (b: ErpBrand) =>
      (b.description ?? '').toLowerCase().includes('véhicule')
      || (b.description ?? '').toLowerCase().includes('vehicle')
      || (b.description ?? '').toLowerCase().includes('car-api');
    if (this.brandFilter === 'vehicle') {
      this.brands = this.allBrands.filter(isVehicle);
    } else if (this.brandFilter === 'supplier') {
      this.brands = this.allBrands.filter(b => !isVehicle(b));
    } else {
      this.brands = this.allBrands;
    }
  }

  importVehicleBrands(): void {
    if (this.importingVehicle) return;
    this.importingVehicle = true;
    this.erpService.importCarApi({
      importParts: false,
      importVehicleBrands: true,
      applyFrenchNames: false,
      ensureVehicleAttribute: false
    }).subscribe({
      next: res => {
        this.importingVehicle = false;
        const n = res.import?.vehicleBrandsCreated ?? 0;
        this.snack.open(this.i18n.t('catalog.brands.vehicleImportDone', { count: n }), undefined, { duration: 4000 });
        this.load();
      },
      error: err => {
        this.importingVehicle = false;
        this.snack.open(err?.error?.message || err?.error?.detail || 'Error', undefined, { duration: 4000 });
      }
    });
  }

  startCreate(): void {
    this.selected = null;
    this.editing = true;
    this.form = this.emptyForm();
  }

  select(b: ErpBrand): void {
    this.selected = b;
    this.editing = false;
    this.form = {
      id: b.id,
      name: b.name,
      slug: b.slug,
      logoUrl: b.logoUrl ?? '',
      websiteUrl: b.websiteUrl ?? '',
      description: b.description ?? '',
      isActive: b.isActive
    };
  }

  startEdit(): void {
    if (!this.selected) return;
    this.editing = true;
  }

  cancelEdit(): void {
    if (this.selected) this.select(this.selected);
    else {
      this.editing = false;
      this.form = this.emptyForm();
    }
  }

  save(): void {
    if (!this.form.name?.trim()) {
      this.error = 'catalog.brands.nameRequired';
      return;
    }
    this.saving = true;
    this.error = '';
    const body: ErpBrandForm = {
      name: this.form.name.trim(),
      slug: this.form.slug?.trim() || undefined,
      logoUrl: this.form.logoUrl || null,
      websiteUrl: this.form.websiteUrl || null,
      description: this.form.description || null,
      isActive: this.form.isActive
    };
    const req = this.selected
      ? this.brandService.update(this.selected.id, body)
      : this.brandService.create(body);
    req.subscribe({
      next: saved => {
        this.saving = false;
        this.editing = false;
        this.load();
        this.select(saved);
      },
      error: err => {
        this.saving = false;
        this.error = err?.error?.error || 'catalog.brands.saveError';
      }
    });
  }

  deactivate(b: ErpBrand, event?: Event): void {
    event?.stopPropagation();
    if (!confirm(this.i18n.t('catalog.brands.confirmDeactivate'))) return;
    this.brandService.deactivate(b.id).subscribe({
      next: () => {
        if (this.selected?.id === b.id) {
          this.selected = null;
          this.editing = false;
        }
        this.load();
      }
    });
  }

  private emptyForm(): ErpBrandForm {
    return { name: '', slug: '', logoUrl: '', websiteUrl: '', description: '', isActive: true };
  }
}
