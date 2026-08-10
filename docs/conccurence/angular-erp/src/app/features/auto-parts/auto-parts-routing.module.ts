// ============================================================
// src/app/features/auto-parts/auto-parts-routing.module.ts
// v2 — Ajout de la route /plate
// ============================================================

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ModuleGuard } from '../../core/guards/module.guard';
import { VehicleSearchComponent } from './components/vehicle-search/vehicle-search.component';
import { OemSearchComponent } from './components/oem-search/oem-search.component';
import { SyncPanelComponent } from './components/sync-panel/sync-panel.component';
import { PlateScanComponent } from './components/plate-scan/plate-scan.component';

const routes: Routes = [
  {
    path: '',
    canActivate: [ModuleGuard],
    data: { requiredModule: 'auto_parts' },
    children: [
      { path: 'plate', component: PlateScanComponent },
      { path: 'vehicle', component: VehicleSearchComponent },
      { path: 'oem', component: OemSearchComponent },
      { path: 'sync', component: SyncPanelComponent },
      { path: '', redirectTo: 'plate', pathMatch: 'full' },
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AutoPartsRoutingModule {}
