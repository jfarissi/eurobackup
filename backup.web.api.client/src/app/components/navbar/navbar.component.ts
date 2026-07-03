import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';
import { filter } from 'rxjs';

interface NavItem {
  path: string;
  label: string;
  tabLabel: string;
  icon: string;
  title: string;
  exact?: boolean;
}

@Component({
  selector: 'app-navbar',
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule]
})
export class NavbarComponent {
  mobileNavOpen = false;

  readonly mainNavItems: NavItem[] = [
    { path: '/upload', label: 'Upload', tabLabel: 'Upload', icon: 'cloud_upload', title: 'Gestion Documents' },
    { path: '/compare', label: 'Association', tabLabel: 'Association', icon: 'link', title: 'Association' },
    { path: '/stock', label: 'Stock', tabLabel: 'Stock', icon: 'inventory_2', title: 'Gestion Documents' },
  ];

  readonly navItems: NavItem[] = [
    ...this.mainNavItems,
    { path: '/python-test', label: 'Python / Ollama', tabLabel: 'Settings', icon: 'science', title: 'Python / Ollama' },
  ];

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
