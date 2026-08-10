// ============================================================
// src/app/app.routes.ts — v2 avec login et dashboard
// ============================================================

import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: 'login', loadComponent: () => import('./features/auth/components/login/login.component').then(c => c.LoginComponent) },
  { path: 'dashboard', loadComponent: () => import('./features/dashboard/components/dashboard/dashboard.component').then(c => c.DashboardComponent) },
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
