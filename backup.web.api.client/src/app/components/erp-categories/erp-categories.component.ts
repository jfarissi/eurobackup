import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../material.module';
import { ErpCategory } from '../../models/erp-product';
import { ErpCategoryForm, ErpCategoryService } from '../../services/erp-category.service';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';
import { TPipe } from '../../pipes/t.pipe';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { CatalogSubnavComponent } from '../shared/catalog-subnav/catalog-subnav.component';
import { AppI18nService } from '../../services/app-i18n.service';

@Component({
  selector: 'app-erp-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent, CatalogSubnavComponent],
  templateUrl: './erp-categories.component.html',
  styleUrls: ['./erp-categories.component.css']
})
export class ErpCategoriesComponent implements OnInit {
  readonly P = Permissions;
  levels = ['MainType', 'Type', 'SubType'];
  levelFilter = 'MainType';
  parentFilterId: number | null = null;
  categories: ErpCategory[] = [];
  parents: ErpCategory[] = [];
  loading = false;
  saving = false;
  error = '';
  selected: ErpCategory | null = null;
  editing = false;
  form: ErpCategoryForm = this.emptyForm();

  constructor(
    private categoryService: ErpCategoryService,
    public perm: PermissionService,
    private i18n: AppI18nService
  ) {}

  get canCreate(): boolean {
    return this.perm.hasAny(Permissions.CategoryCreate, Permissions.ProductCreate);
  }
  get canUpdate(): boolean {
    return this.perm.hasAny(Permissions.CategoryUpdate, Permissions.ProductUpdate);
  }
  get canDelete(): boolean {
    return this.perm.hasAny(Permissions.CategoryDelete, Permissions.ProductDelete);
  }

  ngOnInit(): void {
    this.reloadParents();
    this.load();
  }

  onLevelFilterChange(): void {
    this.parentFilterId = null;
    this.reloadParents();
    this.load();
    if (!this.selected) this.form.level = this.levelFilter;
  }

  onParentFilterChange(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    const q: { level?: string; parentId?: number } = { level: this.levelFilter };
    if (this.parentFilterId != null) q.parentId = this.parentFilterId;
    this.categoryService.list(q).subscribe({
      next: items => {
        this.categories = items ?? [];
        this.loading = false;
      },
      error: () => {
        this.error = 'catalog.categories.loadError';
        this.loading = false;
      }
    });
  }

  reloadParents(): void {
    if (this.levelFilter === 'MainType' && this.form.level === 'MainType') {
      this.parents = [];
      return;
    }
    const level = this.editing ? this.form.level : this.levelFilter;
    if (level === 'MainType') {
      this.parents = [];
      return;
    }
    const parentLevel = level === 'Type' ? 'MainType' : 'Type';
    this.categoryService.list({ level: parentLevel, activeOnly: true }).subscribe(items => {
      this.parents = items ?? [];
    });
  }

  startCreate(): void {
    this.selected = null;
    this.editing = true;
    this.form = this.emptyForm();
    this.form.level = this.levelFilter;
    this.form.parentId = this.parentFilterId;
    this.reloadParents();
  }

  select(c: ErpCategory): void {
    this.selected = c;
    this.editing = false;
    this.form = {
      id: c.id,
      level: c.level,
      parentId: c.parentId ?? null,
      erpExternalId: c.erpExternalId,
      nameNl: c.nameNl,
      nameFr: c.nameFr,
      nameEn: c.nameEn,
      sortOrder: c.sortOrder,
      isActive: c.isActive
    };
  }

  startEdit(): void {
    if (!this.selected) return;
    this.editing = true;
    this.reloadParents();
  }

  cancelEdit(): void {
    if (this.selected) this.select(this.selected);
    else {
      this.editing = false;
      this.form = this.emptyForm();
    }
  }

  label(c: ErpCategory): string {
    return c.nameFr || c.nameNl || c.nameEn || c.erpExternalId;
  }

  save(): void {
    if (!this.form.nameFr?.trim() && !this.form.nameNl?.trim() && !this.form.nameEn?.trim()) {
      this.error = 'catalog.categories.nameRequired';
      return;
    }
    this.saving = true;
    this.error = '';
    const body: ErpCategoryForm = {
      level: this.form.level,
      parentId: this.form.level === 'MainType' ? null : this.form.parentId,
      erpExternalId: this.form.erpExternalId,
      nameNl: this.form.nameNl ?? '',
      nameFr: this.form.nameFr ?? '',
      nameEn: this.form.nameEn ?? '',
      sortOrder: this.form.sortOrder ?? 0,
      isActive: this.form.isActive
    };
    const req = this.selected
      ? this.categoryService.update(this.selected.id, body)
      : this.categoryService.create(body);
    req.subscribe({
      next: saved => {
        this.saving = false;
        this.editing = false;
        this.levelFilter = saved.level;
        this.reloadParents();
        this.load();
        this.select(saved);
      },
      error: err => {
        this.saving = false;
        this.error = err?.error?.error || 'catalog.categories.saveError';
      }
    });
  }

  deactivate(c: ErpCategory, event?: Event): void {
    event?.stopPropagation();
    if (!confirm(this.i18n.t('catalog.categories.confirmDeactivate'))) return;
    this.categoryService.deactivate(c.id).subscribe({
      next: () => {
        if (this.selected?.id === c.id) {
          this.selected = null;
          this.editing = false;
        }
        this.load();
      }
    });
  }

  private emptyForm(): ErpCategoryForm {
    return {
      level: 'MainType',
      parentId: null,
      nameNl: '',
      nameFr: '',
      nameEn: '',
      sortOrder: 0,
      isActive: true
    };
  }
}
