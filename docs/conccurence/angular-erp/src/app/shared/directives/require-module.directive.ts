// ============================================================
// src/app/shared/directives/require-module.directive.ts
// Masque un élément DOM si le module n'est pas actif
// Usage: <div *requireModule="'auto_parts'">...</div>
// ============================================================

import { Directive, Input, TemplateRef, ViewContainerRef, OnInit } from '@angular/core';
import { ModuleService } from '../../core/services/module.service';

@Directive({ selector: '[requireModule]' })
export class RequireModuleDirective implements OnInit {
  @Input('requireModule') moduleCode!: string;

  constructor(
    private templateRef: TemplateRef<any>,
    private viewContainer: ViewContainerRef,
    private moduleService: ModuleService
  ) {}

  ngOnInit(): void {
    this.moduleService.hasModule(this.moduleCode).subscribe(hasIt => {
      if (hasIt) {
        this.viewContainer.createEmbeddedView(this.templateRef);
      } else {
        this.viewContainer.clear();
      }
    });
  }
}
