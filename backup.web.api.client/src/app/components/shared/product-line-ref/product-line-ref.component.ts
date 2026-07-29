import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { of } from 'rxjs';
import { ErpProduct } from '../../../models/erp-product';
import { ProductLineFields } from '../../../models/product-line-fields';
import { ErpProductService } from '../../../services/erp-product.service';

@Component({
  selector: 'app-product-line-ref',
  standalone: true,
  imports: [CommonModule, FormsModule, MatAutocompleteModule, MatInputModule],
  templateUrl: './product-line-ref.component.html',
  styleUrls: ['./product-line-ref.component.css']
})
export class ProductLineRefComponent implements OnInit, OnDestroy {
  @Input({ required: true }) line!: ProductLineFields;
  /** sale = prix vente (HT), purchase = prix achat (coût). */
  @Input() priceMode: 'sale' | 'purchase' = 'sale';
  @Output() productSelected = new EventEmitter<void>();

  suggestions: ErpProduct[] = [];
  searching = false;
  lastQuery = '';

  private readonly search$ = new Subject<string>();
  private sub?: Subscription;

  constructor(private erpProductService: ErpProductService) {}

  ngOnInit(): void {
    this.sub = this.search$.pipe(
      debounceTime(280),
      distinctUntilChanged(),
      switchMap(term => {
        const q = term.trim();
        if (q.length < 2) {
          this.lastQuery = q;
          this.searching = false;
          this.suggestions = [];
          return of(null);
        }
        this.searching = true;
        this.lastQuery = q;
        return this.erpProductService.getProducts({ q, page: 1, pageSize: 12 });
      })
    ).subscribe(result => {
      this.searching = false;
      this.suggestions = result?.items ?? [];
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  onInput(value: string | ErpProduct): void {
    if (typeof value !== 'string') return; // option selected → handled by onOptionSelected
    this.line.productKey = value;
    this.search$.next(value);
  }

  onOptionSelected(event: MatAutocompleteSelectedEvent): void {
    const product = event.option.value as ErpProduct;
    this.applyProduct(product);
    this.suggestions = [];
    this.lastQuery = '';
    this.productSelected.emit();
  }

  /** Used by [displayWith] — converts the option value back to a display string. */
  displayRef(value: ErpProduct | string | null): string {
    if (!value) return '';
    if (typeof value === 'string') return value;
    return this.productRef(value);
  }

  productRef(product: ErpProduct): string {
    return product.reference?.trim() || product.erpProductId || `#${product.id}`;
  }

  productLabel(product: ErpProduct): string {
    const ref = this.productRef(product);
    const name = product.name?.trim();
    return name ? `${ref} — ${name}` : ref;
  }

  productPrice(product: ErpProduct): number | null {
    const price = this.priceMode === 'purchase'
      ? (product.cPrice ?? product.unitPrice ?? product.priceHT)
      : (product.priceHT ?? product.unitPrice ?? product.rPrice);
    return price != null ? price : null;
  }

  private applyProduct(product: ErpProduct): void {
    this.line.productKey = this.productRef(product);
    this.line.description = product.name?.trim() || product.name2?.trim() || '';
    this.line.unitPrice = this.productPrice(product) ?? 0;
    this.line.vatRate = product.typeVatPerc ?? 21;
  }
}
