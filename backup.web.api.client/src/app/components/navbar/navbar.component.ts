import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { filter } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { CompanyService } from '../../services/company.service';
import { Company } from '../../models/company';
import { AuthUser } from '../../models/auth';
import { AppI18nService, AppLang } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { HelpContentService } from '../../services/help-content.service';
import { HelpCenterComponent } from '../shared/help-center/help-center.component';

import { PermissionService } from '../../services/permission.service';
import { RoutePermissions, Permissions } from '../../constants/permissions';

interface NavItem {
  path: string;
  labelKey: string;
  tabLabelKey: string;
  icon: string;
  titleKey: string;
  exact?: boolean;
  literal?: boolean;
}

const MAIN_NAV_ITEMS: NavItem[] = [
  { path: '/dashboard', labelKey: 'nav.dashboard', tabLabelKey: 'nav.dashboard', icon: 'dashboard', titleKey: 'nav.title.dashboard' },
  { path: '/sales', labelKey: 'nav.sales', tabLabelKey: 'nav.sales', icon: 'point_of_sale', titleKey: 'nav.title.sales' },
  { path: '/purchases', labelKey: 'nav.purchases', tabLabelKey: 'nav.purchases', icon: 'shopping_cart', titleKey: 'nav.title.purchases' },
  { path: '/cash', labelKey: 'nav.cash', tabLabelKey: 'nav.cash', icon: 'receipt_long', titleKey: 'nav.title.cash' },
  { path: '/accounting', labelKey: 'nav.accounting', tabLabelKey: 'nav.accounting', icon: 'menu_book', titleKey: 'nav.title.accounting' },
  { path: '/numbering', labelKey: 'nav.numbering', tabLabelKey: 'nav.numbering', icon: 'tag', titleKey: 'nav.title.numbering' },
  { path: '/upload', labelKey: 'nav.upload', tabLabelKey: 'nav.upload', icon: 'cloud_upload', titleKey: 'nav.title.upload' },
  { path: '/recherche', labelKey: 'nav.search', tabLabelKey: 'nav.search', icon: 'search', titleKey: 'nav.title.search' },
  { path: '/compare', labelKey: 'nav.compare', tabLabelKey: 'nav.compare', icon: 'link', titleKey: 'nav.title.compare' },
  { path: '/stock', labelKey: 'nav.stock', tabLabelKey: 'nav.stock', icon: 'inventory_2', titleKey: 'nav.title.stock' },
  { path: '/erp-products', labelKey: 'nav.erpProducts', tabLabelKey: 'nav.erpProducts', icon: 'category', titleKey: 'nav.title.erpProducts' },
  { path: '/plate-scan', labelKey: 'nav.plateScan', tabLabelKey: 'nav.plateScan', icon: 'directions_car', titleKey: 'nav.title.plateScan' },
  { path: '/erp-brands', labelKey: 'nav.erpBrands', tabLabelKey: 'nav.erpBrands', icon: 'storefront', titleKey: 'nav.title.erpBrands' },
  { path: '/erp-categories', labelKey: 'nav.erpCategories', tabLabelKey: 'nav.erpCategories', icon: 'account_tree', titleKey: 'nav.title.erpCategories' },
  { path: '/erp-changes', labelKey: 'nav.erpChanges', tabLabelKey: 'nav.erpChanges', icon: 'sync_alt', titleKey: 'nav.title.erpChanges' },
  { path: '/assistant', labelKey: 'nav.assistant', tabLabelKey: 'nav.assistantTab', icon: 'smart_toy', titleKey: 'nav.title.assistant' },
];

const ADMIN_NAV_ITEM: NavItem = {
  path: '/admin',
  labelKey: 'nav.admin',
  tabLabelKey: 'nav.admin',
  icon: 'admin_panel_settings',
  titleKey: 'nav.title.admin',
};

@Component({
  selector: 'app-navbar',
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule, TPipe, FormsModule, HelpCenterComponent]
})
export class NavbarComponent {
  mobileNavOpen = false;
  isLoginPage = false;
  user: AuthUser | null = null;
  companies: Company[] = [];
  selectedCompanyId: string | null = null;
  switchingCompany = false;
  globalSearchQuery = '';
  readonly Permissions = Permissions;

  get canGlobalSearch(): boolean {
    return this.permissionService.hasAny(Permissions.DocumentRead);
  }

  get navItems(): NavItem[] {
    return this.visibleItems([...MAIN_NAV_ITEMS, ADMIN_NAV_ITEM]);
  }

  pageTitleKey = 'nav.title.default';
  pageTitleLiteral = false;

  constructor(
    private router: Router,
    private auth: AuthService,
    private companyService: CompanyService,
    public permissionService: PermissionService,
    public i18n: AppI18nService,
    private help: HelpContentService
  ) {
    this.auth.user$.subscribe(u => {
      this.user = u;
      if (u?.companies?.length) {
        this.companyService.setCompanies(u.companies, u.companyId ?? undefined);
      }
    });
    this.companyService.companies$.subscribe(c => this.companies = c);
    this.companyService.activeCompanyId$.subscribe(id => this.selectedCompanyId = id);
    if (this.auth.isLoggedIn) {
      this.companyService.loadAvailable().subscribe();
      const u = this.auth.currentUser;
      if (u && !u.isAdmin && (!u.permissions || u.permissions.length === 0)) {
        this.auth.me().subscribe();
      }
    }
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      this.updateTitle();
      this.syncSearchFromUrl();
      this.mobileNavOpen = false;
      this.isLoginPage = this.router.url.startsWith('/login');
      if (!this.isLoginPage && this.auth.isLoggedIn) {
        this.auth.refreshSession();
      }
    });
    this.updateTitle();
    this.syncSearchFromUrl();
    this.isLoginPage = this.router.url.startsWith('/login');
  }

  setLanguage(lang: AppLang): void {
    this.i18n.setLang(lang);
  }

  openHelpCenter(): void {
    this.help.openCenter();
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }

  submitGlobalSearch(): void {
    const q = this.globalSearchQuery.trim();
    if (!q || !this.canGlobalSearch) return;
    void this.router.navigate(['/recherche'], { queryParams: { q } });
  }

  clearGlobalSearch(): void {
    this.globalSearchQuery = '';
    if (this.router.url.startsWith('/recherche')) {
      void this.router.navigate(['/recherche']);
    }
  }

  onCompanyChange(companyId: string): void {
    if (!companyId || companyId === this.selectedCompanyId) return;
    this.switchingCompany = true;
    this.auth.switchCompany(companyId).subscribe({
      next: () => {
        this.switchingCompany = false;
        this.reloadCurrentRoute();
      },
      error: () => {
        this.switchingCompany = false;
      }
    });
  }

  /** Force le remount du composant actif (même URL) pour recharger les données société. */
  private reloadCurrentRoute(): void {
    const url = this.router.url;
    void this.router.navigateByUrl('/__reload', { skipLocationChange: true }).then(() => {
      void this.router.navigateByUrl(url);
    });
  }

  displayName(): string {
    if (!this.user) return '';
    const name = [this.user.firstName, this.user.lastName].filter(Boolean).join(' ');
    return name || this.user.username;
  }

  private syncSearchFromUrl(): void {
    const tree = this.router.parseUrl(this.router.url);
    if (tree.root.children['primary']?.segments[0]?.path === 'recherche') {
      const q = tree.queryParams['q'];
      this.globalSearchQuery = typeof q === 'string' ? q : '';
    }
  }

  private updateTitle(): void {
    const url = this.router.url.split('?')[0];
    const item = this.navItems.find(n => url.startsWith(n.path));
    this.pageTitleKey = item?.titleKey ?? 'nav.title.default';
    this.pageTitleLiteral = false;
  }

  private visibleItems(items: NavItem[]): NavItem[] {
    void this.user;
    return items.filter(item => {
      if (item.path === '/erp-changes' && !this.companyService.hasErpCatalogSync) {
        return false;
      }
      if (item.path === '/plate-scan' && this.companyService.modules.length > 0 && !this.companyService.hasAutoParts) {
        return false;
      }
      const perms = RoutePermissions[item.path];
      if (!perms?.length) return true;
      return this.permissionService.hasAny(...perms);
    });
  }
}
