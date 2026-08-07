import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { SortDir, TableSortState } from '../../../utils/table-sort';

/**
 * En-tête de colonne cliquable pour trier un tableau.
 *
 * Usage:
 * ```html
 * <th appSortable [state]="sort" key="date" defaultDir="desc">{{ 'common.date' | t }}</th>
 * ```
 */
@Component({
  selector: 'th[appSortable]',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <button type="button" class="sortable-th-btn" (click)="onClick($event)">
      <span class="sortable-th-label"><ng-content></ng-content></span>
      <mat-icon class="sort-icon" aria-hidden="true">{{ state.icon(key) }}</mat-icon>
    </button>
  `,
  host: {
    class: 'sortable',
    '[class.sorted]': 'state.key === key'
  }
})
export class SortableThComponent {
  @Input({ required: true }) state!: TableSortState;
  @Input({ required: true }) key!: string;
  @Input() defaultDir: SortDir = 'asc';
  @Output() sorted = new EventEmitter<void>();

  onClick(ev: Event): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.state.toggle(this.key, this.defaultDir);
    this.sorted.emit();
  }
}
