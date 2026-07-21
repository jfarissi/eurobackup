import { Component, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  title = 'backup.web.api.client';
  showAdminShell = true;

  constructor(private router: Router) {}

  ngOnInit(): void {
    this.updateShell(this.router.url);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => this.updateShell(e.urlAfterRedirects));
  }

  private updateShell(url: string): void {
    this.showAdminShell = !url.startsWith('/assistant');
  }
}
