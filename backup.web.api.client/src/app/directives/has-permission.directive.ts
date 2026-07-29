import { Directive, Input, OnDestroy, TemplateRef, ViewContainerRef } from '@angular/core';
import { Subscription } from 'rxjs';
import { PermissionService } from '../services/permission.service';
import { AuthService } from '../services/auth.service';

@Directive({
  selector: '[appHasPermission]',
  standalone: true
})
export class HasPermissionDirective implements OnDestroy {
  private sub?: Subscription;
  private permissions: string[] = [];
  private mode: 'any' | 'all' = 'any';

  constructor(
    private template: TemplateRef<unknown>,
    private container: ViewContainerRef,
    private permissionService: PermissionService,
    private auth: AuthService
  ) {}

  @Input()
  set appHasPermission(value: string | string[]) {
    this.permissions = Array.isArray(value) ? value : [value];
    this.updateView();
  }

  @Input()
  set appHasPermissionMode(mode: 'any' | 'all') {
    this.mode = mode;
    this.updateView();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private updateView(): void {
    this.sub?.unsubscribe();
    this.sub = this.auth.user$.subscribe(() => this.render());
    this.render();
  }

  private render(): void {
    const allowed = this.mode === 'all'
      ? this.permissionService.hasAll(...this.permissions)
      : this.permissionService.hasAny(...this.permissions);

    this.container.clear();
    if (allowed) {
      this.container.createEmbeddedView(this.template);
    }
  }
}
