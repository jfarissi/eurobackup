import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { finalize } from 'rxjs';
import { ErpProductService } from '../../services/erp-product.service';
import { OemSearchHit } from '../../models/erp-product';
import { CompanyService } from '../../services/company.service';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { CatalogSubnavComponent } from '../shared/catalog-subnav/catalog-subnav.component';

@Component({
  selector: 'app-oem-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MaterialModule, TPipe, CatalogSubnavComponent],
  templateUrl: './oem-search.component.html',
  styleUrls: ['./oem-search.component.css']
})
export class OemSearchComponent implements OnInit {
  oemQuery = '';
  loading = false;
  error = '';
  searched = false;
  total = 0;
  page = 1;
  pageSize = 50;
  hits: OemSearchHit[] = [];

  readonly hints = ['0281002937', '7700105767', '1K0615301', '8200274075'];

  constructor(
    private erp: ErpProductService,
    public company: CompanyService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {}

  get moduleMissing(): boolean {
    return this.company.modules.length > 0 && !this.company.hasAutoParts;
  }

  get placeholderLabel(): string {
    return this.i18n.t('oemSearch.placeholder');
  }

  get resultSummary(): string {
    return this.i18n.t('oemSearch.resultCount', {
      count: this.total,
      query: this.oemQuery.trim()
    });
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  useHint(hint: string): void {
    this.oemQuery = hint;
    this.search(1);
  }

  search(page = 1): void {
    const q = this.oemQuery.trim();
    if (q.length < 3) {
      this.error = this.i18n.t('oemSearch.minLength');
      return;
    }
    this.loading = true;
    this.error = '';
    this.searched = true;
    this.page = page;
    this.erp.searchByOem(q, page, this.pageSize).pipe(
      finalize(() => this.loading = false)
    ).subscribe({
      next: res => {
        this.hits = res?.items ?? [];
        this.total = res?.total ?? 0;
        this.page = res?.page ?? page;
      },
      error: err => {
        this.hits = [];
        this.total = 0;
        this.error = err?.error?.message || this.i18n.t('oemSearch.error');
      }
    });
  }

  prevPage(): void {
    if (this.page > 1) this.search(this.page - 1);
  }

  nextPage(): void {
    if (this.page < this.totalPages) this.search(this.page + 1);
  }

  formatPrice(v: number | null | undefined): string {
    if (v == null || Number.isNaN(Number(v))) return '—';
    return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}
