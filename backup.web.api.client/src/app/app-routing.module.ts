import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { UploadComponent } from './components/upload/upload.component';
import { CompareComponent } from './components/compare/compare.component';
import { StockComponent } from './components/stock/stock.component';
import { DocumentSearchComponent } from './components/search/document-search.component';
import { environment } from '../environments/environment';

const routes: Routes = [
  { path: '', redirectTo: '/upload', pathMatch: 'full' },
  { path: 'upload', component: UploadComponent },
  { path: 'recherche', component: DocumentSearchComponent },
  { path: 'compare', component: CompareComponent },
  { path: 'stock', component: StockComponent },
];

if (environment.enablePythonTest) {
  routes.push({
    path: 'python-test',
    loadComponent: () =>
      import('./components/python-test/python-test.component').then(m => m.PythonTestComponent),
  });
}

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
