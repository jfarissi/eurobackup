import { Component, ElementRef, HostListener, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../../../material.module';
import { HelpArticle, HelpContentService } from '../../../services/help-content.service';
import { TPipe } from '../../../pipes/t.pipe';

@Component({
  selector: 'app-field-help',
  standalone: true,
  imports: [CommonModule, MaterialModule, TPipe],
  templateUrl: './field-help.component.html',
  styleUrls: ['./field-help.component.css']
})
export class FieldHelpComponent implements OnChanges {
  /** Clé sans préfixe help. — ex. field.sales.customer */
  @Input({ required: true }) helpKey!: string;

  open = false;
  article: HelpArticle | null = null;

  constructor(
    private readonly host: ElementRef<HTMLElement>,
    private readonly help: HelpContentService
  ) {}

  ngOnChanges(): void {
    this.article = this.help.resolve(this.helpKey);
  }

  toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.open = !this.open;
    if (this.open) this.article = this.help.resolve(this.helpKey);
  }

  close(): void {
    this.open = false;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.open) return;
    const target = event.target as Node | null;
    if (target && !this.host.nativeElement.contains(target)) {
      this.open = false;
    }
  }
}
