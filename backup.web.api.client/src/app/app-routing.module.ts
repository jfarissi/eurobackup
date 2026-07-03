import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { UploadComponent } from './components/upload/upload.component';
import { CompareComponent } from './components/compare/compare.component';
import { PythonTestComponent } from './components/python-test/python-test.component';
import { StockComponent } from './components/stock/stock.component';

const routes: Routes = [
  { path: '', redirectTo: '/upload', pathMatch: 'full' },
  { path: 'upload', component: UploadComponent },
  { path: 'compare', component: CompareComponent },
  { path: 'python-test', component: PythonTestComponent },
  { path: 'stock', component: StockComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
