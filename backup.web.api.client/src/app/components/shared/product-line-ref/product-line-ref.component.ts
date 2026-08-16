import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { Subject, Subscription, of } from 'rxjs';
import { catchError, debounceTime, finalize, switchMap } from 'rxjs/operators';
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
export class ProductLineRefComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) line!: ProductLineFields;
  /** sale = prix vente (HT), purchase = prix achat (coût). */
  @Input() priceMode: 'sale' | 'purchase' = 'sale';
  /** Si défini, ne propose que les produits des marques du fournisseur. */
  @Input() supplierId: number | null = null;
  @Output() productSelected = new EventEmitter<ErpProduct>();

  suggestions: ErpProduct[] = [];
  searching = false;
  searchFailed = false;
  lastQuery = '';

  private readonly search$ = new Subject<{ q: string; supplierId: number | null }>();
  private sub?: Subscription;

  constructor(private erpProductService: ErpProductService) {}

  ngOnInit(): void {
    this.sub = this.search$.pipe(
      debounceTime(200),
      // Pas de distinctUntilChanged : resaisir la même réf doit relancer la recherche.
      switchMap(({ q, supplierId }) => {
        const term = q.trim();
        if (term.length < 2) {
          this.lastQuery = term;
          this.searching = false;
          this.searchFailed = false;
          this.suggestions = [];
          return of(null);
        }
        this.searching = true;
        this.searchFailed = false;
        this.lastQuery = term;
        return this.erpProductService.getProducts({
          q: term,
          page: 1,
          pageSize: 25,
          supplierId: supplierId && supplierId > 0 ? supplierId : undefined
        }).pipe(
          catchError(() => {
            this.searchFailed = true;
            return of({ items: [] as ErpProduct[], total: 0, page: 1, pageSize: 25 });
          }),
          finalize(() => {
            this.searching = false;
          })
        );
      })
    ).subscribe(result => {
      if (result == null) return;
      this.suggestions = result.items ?? [];
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['supplierId'] && !changes['supplierId'].firstChange) {
      this.suggestions = [];
      if (this.lastQuery.trim().length >= 2) {
        this.emitSearch(this.lastQuery);
      }
    }
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  onInput(value: string | ErpProduct): void {
    if (typeof value !== 'string') return; // option selected → handled by onOptionSelected
    this.line.productKey = value;
    this.emitSearch(value);
  }

  /** Relance la recherche au focus (évite « Aucun produit » stale après un échec réseau). */
  onFocus(): void {
    const q = (this.line.productKey || '').trim();
    if (q.length >= 2) {
      this.emitSearch(q);
    }
  }

  onOptionSelected(event: MatAutocompleteSelectedEvent): void {
    const product = event.option.value as ErpProduct;
    this.applyProduct(product);
    this.suggestions = [];
    this.lastQuery = '';
    this.searchFailed = false;
    this.productSelected.emit(product);
  }

  /** Used by [displayWith] — converts the option value back to a display string. */
  displayRef(value: ErpProduct | string | null): string {
    if (!value) return '';
    if (typeof value === 'string') return value;
    return this.stockProductKey(value);
  }

  /** Clé stock / lignes docs : code sans préfixe marque (ex. "FF Group 14293" → "14293"). */
  stockProductKey(product: ErpProduct): string {
    const raw = product.reference?.trim() || product.erpProductId || `#${product.id}`;
    return this.toStockProductKey(raw, product.brand);
  }

  /** Affichage liste : référence ERP complète. */
  productRef(product: ErpProduct): string {
    return product.reference?.trim() || this.stockProductKey(product);
  }

  productLabel(product: ErpProduct): string {
    const ref = this.productRef(product);
    const name = product.name?.trim();
    return name ? `${ref} — ${name}` : ref;
  }

  productPrice(product: ErpProduct): number | null {
    if (this.priceMode === 'purchase') {
      // Achat : coût catalogue (CPrice), pas le prix de vente.
      const price = product.cPrice ?? product.priceHT;
      return price != null ? price : null;
    }

    // Vente : même montant que « Prix vente » fiche / liste (UnitPrice, puis RPrice).
    // Ne pas prioriser PriceHT : souvent égal au coût Excel (ancien bug) et ≠ prix affiché.
    const price = product.unitPrice ?? product.rPrice ?? product.priceHT;
    return price != null ? price : null;
  }

  private emitSearch(q: string): void {
    this.search$.next({ q, supplierId: this.supplierId });
  }

  private applyProduct(product: ErpProduct): void {
    this.line.productKey = this.stockProductKey(product);
    this.line.description = product.name?.trim() || product.name2?.trim() || '';
    this.line.unitPrice = this.productPrice(product) ?? 0;
    this.line.vatRate = product.typeVatPerc ?? 21;
  }

  /**
   * Aligne la ref ERP sur la clé Stock.
   * Ex. Reference="FF Group 14293", Brand="FF Group - …" → "14293".
   */
  private toStockProductKey(reference: string, brand?: string | null): string {
    const ref = reference.trim();
    if (!ref) return ref;

    const brandCore = (brand || '')
      .split(/\s*-\s*/)[0]
      ?.trim();
    if (brandCore && ref.toLowerCase().startsWith(brandCore.toLowerCase() + ' ')) {
      return ref.substring(brandCore.length).trim();
    }

    const parts = ref.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      const last = parts[parts.length - 1];
      const head = parts.slice(0, -1).join(' ');
      // Préfixe type marque (lettres) + code final (souvent numérique)
      if (/^[A-Za-z][A-Za-z0-9 .&'/()-]*$/.test(head) && /^[A-Za-z0-9][A-Za-z0-9._/-]*$/.test(last)) {
        return last;
      }
    }

    return ref;
  }
}
