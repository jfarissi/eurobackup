import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { filter } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';
import { AuthUser } from '../../models/auth';
import { AppI18nService, AppLang } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';

interface NavItem {
  path: string;
  labelKey: string;
  tabLabelKey: string;
  icon: string;
  titleKey: string;
  exact?: boolean;
  /** When set, shown as-is (no i18n) — e.g. dev tools */
  literal?: boolean;
}

const MAIN_NAV_ITEMS: NavItem[] = [
  { path: '/upload', labelKey: 'nav.upload', tabLabelKey: 'nav.upload', icon: 'cloud_upload', titleKey: 'nav.title.upload' },
  { path: '/recherche', labelKey: 'nav.search', tabLabelKey: 'nav.search', icon: 'search', titleKey: 'nav.title.search' },
  { path: '/compare', labelKey: 'nav.compare', tabLabelKey: 'nav.compare', icon: 'link', titleKey: 'nav.title.compare' },
  { path: '/stock', labelKey: 'nav.stock', tabLabelKey: 'nav.stock', icon: 'inventory_2', titleKey: 'nav.title.stock' },
  { path: '/erp-products', labelKey: 'nav.erpProducts', tabLabelKey: 'nav.erpProducts', icon: 'category', titleKey: 'nav.title.erpProducts' },
  { path: '/erp-changes', labelKey: 'nav.erpChanges', tabLabelKey: 'nav.erpChanges', icon: 'sync_alt', titleKey: 'nav.title.erpChanges' },
  { path: '/assistant', labelKey: 'nav.assistant', tabLabelKey: 'nav.assistantTab', icon: 'smart_toy', titleKey: 'nav.title.assistant' },
];

const PYTHON_TEST_NAV_ITEM: NavItem = {
  path: '/python-test',
  labelKey: 'Python / Ollama',
  tabLabelKey: 'Dev',
  icon: 'science',
  titleKey: 'Python / Ollama',
  literal: true,
};

@Component({
  selector: 'app-navbar',
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule, TPipe]
})
export class NavbarComponent {
  mobileNavOpen = false;
  isLoginPage = false;
  user: AuthUser | null = null;
  readonly enablePythonTest = environment.enablePythonTest;
  readonly mainNavItems = MAIN_NAV_ITEMS;
  readonly navItems: NavItem[] = environment.enablePythonTest
    ? [...MAIN_NAV_ITEMS, PYTHON_TEST_NAV_ITEM]
    : MAIN_NAV_ITEMS;

  pageTitleKey = 'nav.title.default';
  pageTitleLiteral = false;

  constructor(
    private router: Router,
    private auth: AuthService,
    public i18n: AppI18nService
  ) {
    this.auth.user$.subscribe(u => this.user = u);
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => {
      this.updateTitle();
      this.mobileNavOpen = false;
      this.isLoginPage = this.router.url.startsWith('/login');
    });
    this.updateTitle();
    this.isLoginPage = this.router.url.startsWith('/login');
  }

  setLanguage(lang: AppLang): void {
    this.i18n.setLang(lang);
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }

  displayName(): string {
    if (!this.user) return '';
    const name = [this.user.firstName, this.user.lastName].filter(Boolean).join(' ');
    return name || this.user.username;
  }

  private updateTitle(): void {
    const url = this.router.url.split('?')[0];
    const item = this.navItems.find(n => url.startsWith(n.path));
    this.pageTitleKey = item?.titleKey ?? 'nav.title.default';
    this.pageTitleLiteral = !!item?.literal;
  }
}
