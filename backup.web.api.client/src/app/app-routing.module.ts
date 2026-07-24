import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { UploadComponent } from './components/upload/upload.component';
import { CompareComponent } from './components/compare/compare.component';
import { StockComponent } from './components/stock/stock.component';
import { DocumentSearchComponent } from './components/search/document-search.component';
import { ErpChangesComponent } from './components/erp-changes/erp-changes.component';
import { ErpProductsComponent } from './components/erp-products/erp-products.component';
import { LoginComponent } from './components/login/login.component';
import { authGuard } from './guards/auth.guard';
import { environment } from '../environments/environment';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', redirectTo: '/upload', pathMatch: 'full' },
  {
    path: 'assistant',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/store-assistant/store-assistant.component').then(m => m.StoreAssistantComponent)
  },
  { path: 'upload', component: UploadComponent, canActivate: [authGuard] },
  { path: 'recherche', component: DocumentSearchComponent, canActivate: [authGuard] },
  { path: 'compare', component: CompareComponent, canActivate: [authGuard] },
  { path: 'stock', component: StockComponent, canActivate: [authGuard] },
  { path: 'erp-products', component: ErpProductsComponent, canActivate: [authGuard] },
  { path: 'erp-changes', component: ErpChangesComponent, canActivate: [authGuard] },
];

if (environment.enablePythonTest) {
  routes.push({
    path: 'python-test',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/python-test/python-test.component').then(m => m.PythonTestComponent),
  });
}

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
