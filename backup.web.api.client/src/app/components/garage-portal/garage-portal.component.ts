import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../../material.module';
import { TPipe } from '../../pipes/t.pipe';
import { AppI18nService } from '../../services/app-i18n.service';
import { GaragePortalService } from '../../services/garage-portal.service';
import { GarageMe, GarageOrder, GarageOrderDetail, GarageVehicle } from '../../models/garage-portal';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-garage-portal',
  standalone: true,
  imports: [CommonModule, MaterialModule, TPipe],
  templateUrl: './garage-portal.component.html',
  styleUrls: ['./garage-portal.component.css']
})
export class GaragePortalComponent implements OnInit {
  loading = true;
  error = '';
  me: GarageMe | null = null;
  orders: GarageOrder[] = [];
  vehicles: GarageVehicle[] = [];
  selected: GarageOrderDetail | null = null;
  detailLoading = false;

  constructor(
    private api: GaragePortalService,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.error = '';
    forkJoin({
      me: this.api.me(),
      orders: this.api.orders(),
      vehicles: this.api.vehicles()
    }).subscribe({
      next: ({ me, orders, vehicles }) => {
        this.me = me;
        this.orders = orders;
        this.vehicles = vehicles;
        this.loading = false;
      },
      error: err => {
        this.error = err?.error?.error || this.i18n.t('garage.loadError');
        this.loading = false;
      }
    });
  }

  openOrder(order: GarageOrder): void {
    if (this.selected?.id === order.id) {
      this.selected = null;
      return;
    }
    this.detailLoading = true;
    this.api.order(order.id).subscribe({
      next: detail => {
        this.selected = detail;
        this.detailLoading = false;
      },
      error: () => {
        this.detailLoading = false;
      }
    });
  }

  formatMoney(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString(this.i18n.numberLocale(), {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  vehicleLabel(v: GarageVehicle): string {
    return [v.make, v.model, v.year].filter(Boolean).join(' ') || '—';
  }
}
