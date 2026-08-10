// ============================================================
// src/app/app-routing.module.ts
// ============================================================

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  {
    path: 'products',
    loadChildren: () => import('./features/products/products-routing.module').then(m => m.ProductsRoutingModule)
  },
  {
    path: 'auto-parts',
    loadChildren: () => import('./features/auto-parts/auto-parts-routing.module').then(m => m.AutoPartsRoutingModule)
  },
  { path: 'unauthorized', loadComponent: () => import('./shared/components/unauthorized/unauthorized.component').then(c => c.UnauthorizedComponent) },
  { path: '**', redirectTo: '/dashboard' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}
