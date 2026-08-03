import { Component, ElementRef, EventEmitter, HostListener, Input, OnChanges, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../../../material.module';
import { AppI18nService } from '../../../services/app-i18n.service';
import { HelpArticle, HelpContentService } from '../../../services/help-content.service';
import { TPipe } from '../../../pipes/t.pipe';

@Component({
  selector: 'app-form-help',
  standalone: true,
  imports: [CommonModule, MaterialModule, TPipe],
  templateUrl: './form-help.component.html',
  styleUrls: ['./form-help.component.css']
})
export class FormHelpComponent implements OnInit, OnChanges {
  /** Clé sans préfixe help. — ex. purchases.comptabiliserLot */
  @Input({ required: true }) helpKey!: string;
  /** Codes glossaire — ex. ['BL','FAC'] */
  @Input() abbrs: string[] = [];
  /** Statut document pour aide contextuelle (RG-UX2) */
  @Input() status: string | null = null;
  @Output() walkthrough = new EventEmitter<string>();

  open = false;
  showGuide = false;
  article: HelpArticle | null = null;
  feedback: 'up' | 'down' | null = null;

  constructor(
    private readonly host: ElementRef<HTMLElement>,
    private readonly i18n: AppI18nService,
    private readonly help: HelpContentService
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  ngOnChanges(): void {
    this.refresh();
  }

  get glossaryEntries(): { code: string; label: string }[] {
    return (this.abbrs || [])
      .map(code => {
        const key = `glossary.${code}`;
        const label = this.i18n.t(key);
        return { code, label: label === key ? code : label };
      })
      .filter(e => !!e.code);
  }

  toggle(event: Event): void {
    event.stopPropagation();
    this.open = !this.open;
    if (this.open) {
      this.refresh();
      this.help.track(this.helpKey, 'open');
    }
  }

  close(): void {
    this.open = false;
    this.showGuide = false;
  }

  toggleGuide(): void {
    this.showGuide = !this.showGuide;
    if (this.showGuide) this.help.track(this.helpKey, 'guide');
  }

  openInCenter(): void {
    this.help.openCenter(this.article?.title || this.helpKey);
    this.close();
  }

  openWalkthrough(): void {
    this.walkthrough.emit(this.helpKey);
    this.help.track(this.helpKey, 'guide');
    this.close();
  }

  vote(value: 'up' | 'down', event: Event): void {
    event.stopPropagation();
    this.help.setFeedback(this.helpKey, value);
    this.feedback = value;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.open) return;
    const target = event.target as Node | null;
    if (target && !this.host.nativeElement.contains(target)) {
      this.close();
    }
  }

  private refresh(): void {
    if (!this.helpKey) return;
    this.article = this.help.resolve(this.helpKey, this.status);
    this.feedback = this.help.getFeedback(this.helpKey);
  }
}
