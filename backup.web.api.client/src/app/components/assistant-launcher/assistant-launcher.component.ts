import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../../material.module';
import { environment } from '../../../environments/environment';
import { TPipe } from '../../pipes/t.pipe';

@Component({
  selector: 'app-assistant-launcher',
  standalone: true,
  imports: [CommonModule, MaterialModule, TPipe],
  template: `
    <div class="launcher">
      <mat-spinner diameter="40"></mat-spinner>
      <p>{{ 'assistant.redirecting' | t }}</p>
    </div>
  `,
  styles: [`
    .launcher {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      min-height: 40vh;
      color: var(--text-muted, #666);
    }
  `],
})
export class AssistantLauncherComponent implements OnInit {
  ngOnInit(): void {
    const url = environment.chatbotPublicUrl?.trim();
    if (!url) {
      console.error('chatbotPublicUrl is not configured');
      return;
    }
    window.location.replace(url);
  }
}
