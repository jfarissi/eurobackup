import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../../../material.module';
import { HelpAlert } from '../../../services/help-alerts';

@Component({
  selector: 'app-help-alerts',
  standalone: true,
  imports: [CommonModule, MaterialModule],
  template: `
    <div class="help-alerts" *ngIf="alerts?.length">
      <div class="help-alert" *ngFor="let a of alerts" [attr.data-severity]="a.severity">
        <mat-icon>{{ a.severity === 'block' ? 'block' : a.severity === 'warn' ? 'warning_amber' : 'info' }}</mat-icon>
        <div>
          <code *ngIf="a.rgId">{{ a.rgId }}</code>
          <span>{{ a.message }}</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .help-alerts { display: flex; flex-direction: column; gap: 8px; margin: 10px 0 14px; }
    .help-alert {
      display: flex; gap: 10px; align-items: flex-start;
      padding: 10px 12px; border-radius: 10px; font-size: 13px; line-height: 1.4;
      border: 1px solid #cbd5e1; background: #f8fafc; color: #334155;
    }
    .help-alert[data-severity='warn'] { background: #fffbeb; border-color: #f59e0b; color: #92400e; }
    .help-alert[data-severity='block'] { background: #fef2f2; border-color: #ef4444; color: #991b1b; }
    .help-alert[data-severity='info'] { background: #eff6ff; border-color: #60a5fa; color: #1e3a8a; }
    .help-alert mat-icon { flex-shrink: 0; }
    .help-alert code {
      display: inline-block; margin-right: 6px; padding: 0 4px; border-radius: 4px;
      background: rgba(15,23,42,.08); font-size: 11px; font-weight: 700;
    }
  `]
})
export class HelpAlertsComponent {
  @Input() alerts: HelpAlert[] = [];
}
