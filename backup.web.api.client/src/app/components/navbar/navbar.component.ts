import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { filter } from 'rxjs';
import { environment } from '../../../environments/environment';

interface NavItem {
  path: string;
  label: string;
  tabLabel: string;
  icon: string;
  title: string;
  exact?: boolean;
}

const MAIN_NAV_ITEMS: NavItem[] = [
  { path: '/upload', label: 'Upload', tabLabel: 'Upload', icon: 'cloud_upload', title: 'Gestion Documents' },
  { path: '/recherche', label: 'Recherche', tabLabel: 'Recherche', icon: 'search', title: 'Recherche' },
  { path: '/compare', label: 'Association', tabLabel: 'Association', icon: 'link', title: 'Association' },
  { path: '/stock', label: 'Stock', tabLabel: 'Stock', icon: 'inventory_2', title: 'Gestion Documents' },
  { path: '/erp-products', label: 'Produits', tabLabel: 'Produits', icon: 'category', title: 'Produits ERP' },
  { path: '/erp-changes', label: 'Changements', tabLabel: 'Changements', icon: 'sync_alt', title: 'Changements ERP' },
];

const PYTHON_TEST_NAV_ITEM: NavItem = {
  path: '/python-test',
  label: 'Python / Ollama',
  tabLabel: 'Dev',
  icon: 'science',
  title: 'Python / Ollama',
};

@Component({
  selector: 'app-navbar',
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule]
})
export class NavbarComponent {
  mobileNavOpen = false;
  readonly enablePythonTest = environment.enablePythonTest;
  readonly mainNavItems = MAIN_NAV_ITEMS;
  readonly navItems: NavItem[] = environment.enablePythonTest
    ? [...MAIN_NAV_ITEMS, PYTHON_TEST_NAV_ITEM]
    : MAIN_NAV_ITEMS;

  pageTitle = 'Gestion Documents';

  constructor(private router: Router) {
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      this.updateTitle();
      this.mobileNavOpen = false;
    });
    this.updateTitle();
  }

  private updateTitle(): void {
    const url = this.router.url.split('?')[0];
    const item = this.navItems.find(n => url.startsWith(n.path));
    this.pageTitle = item?.title ?? 'Gestion Documents';
  }
}
