import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { UploadComponent } from './components/upload/upload.component';
import { CompareComponent } from './components/compare/compare.component';
import { StockComponent } from './components/stock/stock.component';
import { DocumentSearchComponent } from './components/search/document-search.component';
import { ErpChangesComponent } from './components/erp-changes/erp-changes.component';
import { ErpProductsComponent } from './components/erp-products/erp-products.component';
import { SalesComponent } from './components/sales/sales.component';
import { CashRegisterComponent } from './components/cash-register/cash-register.component';
import { AccountingComponent } from './components/accounting/accounting.component';
import { PurchasesComponent } from './components/purchases/purchases.component';
import { NumberingSettingsComponent } from './components/numbering-settings/numbering-settings.component';
import { LoginComponent } from './components/login/login.component';
import { authGuard } from './guards/auth.guard';
import { permissionGuard } from './guards/permission.guard';
import { Permissions } from './constants/permissions';
import { environment } from '../environments/environment';
import { homeRedirectGuard } from './guards/home-redirect.guard';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '__reload',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/route-reload/route-reload.component').then(m => m.RouteReloadComponent)
  },
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authGuard, homeRedirectGuard],
    loadComponent: () => import('./components/access-denied/access-denied.component').then(m => m.AccessDeniedComponent)
  },
  {
    path: 'access-denied',
    canActivate: [authGuard],
    loadComponent: () => import('./components/access-denied/access-denied.component').then(m => m.AccessDeniedComponent)
  },
  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [authGuard]
  },
  {
    path: 'assistant',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/assistant-launcher/assistant-launcher.component').then(m => m.AssistantLauncherComponent)
  },
  { path: 'upload', component: UploadComponent, canActivate: [authGuard, permissionGuard(Permissions.DocumentUpload)] },
  { path: 'recherche', component: DocumentSearchComponent, canActivate: [authGuard, permissionGuard(Permissions.DocumentRead)] },
  { path: 'compare', component: CompareComponent, canActivate: [authGuard, permissionGuard(Permissions.DocumentLink)] },
  { path: 'sales', component: SalesComponent, canActivate: [authGuard, permissionGuard(Permissions.CustomerRead, Permissions.QuoteRead, Permissions.OrderRead, Permissions.InvoiceRead, Permissions.DeliveryNoteRead)] },
  { path: 'purchases', component: PurchasesComponent, canActivate: [authGuard, permissionGuard(Permissions.SupplierRead, Permissions.PurchaseOrderRead, Permissions.ReceiptRead, Permissions.SupplierInvoiceRead)] },
  { path: 'cash', component: CashRegisterComponent, canActivate: [authGuard, permissionGuard(Permissions.CashRead, Permissions.CashManage)] },
  { path: 'accounting', component: AccountingComponent, canActivate: [authGuard, permissionGuard(Permissions.AccountingRead, Permissions.AccountingCreate)] },
  { path: 'numbering', component: NumberingSettingsComponent, canActivate: [authGuard, permissionGuard(Permissions.NumberingManage)] },
  { path: 'stock', component: StockComponent, canActivate: [authGuard, permissionGuard(Permissions.StockRead)] },
  { path: 'erp-products', component: ErpProductsComponent, canActivate: [authGuard, permissionGuard(Permissions.ProductRead)] },
  { path: 'erp-changes', component: ErpChangesComponent, canActivate: [authGuard, permissionGuard(Permissions.ErpChangeRead)] },
  {
    path: 'admin',
    canActivate: [authGuard, permissionGuard(Permissions.UserRead, Permissions.RoleRead)],
    loadComponent: () => import('./components/admin/admin.component').then(m => m.AdminComponent)
  },
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
  imports: [RouterModule.forRoot(routes, { onSameUrlNavigation: 'reload' })],
  exports: [RouterModule]
})
export class AppRoutingModule { }
