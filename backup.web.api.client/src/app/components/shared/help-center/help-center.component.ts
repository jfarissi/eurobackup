import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription, combineLatest } from 'rxjs';
import { MaterialModule } from '../../../material.module';
import { HelpArticle, HelpContentService } from '../../../services/help-content.service';
import { TPipe } from '../../../pipes/t.pipe';

@Component({
  selector: 'app-help-center',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe],
  templateUrl: './help-center.component.html',
  styleUrls: ['./help-center.component.css']
})
export class HelpCenterComponent implements OnInit, OnDestroy {
  open = false;
  query = '';
  results: HelpArticle[] = [];
  glossary: { code: string; label: string }[] = [];
  selected: HelpArticle | null = null;

  private sub?: Subscription;

  constructor(private readonly help: HelpContentService) {}

  ngOnInit(): void {
    this.sub = combineLatest([this.help.centerOpen$, this.help.searchQuery$]).subscribe(([open, query]) => {
      this.open = open;
      this.query = query;
      if (open) this.runSearch();
      else this.selected = null;
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'F1') {
      event.preventDefault();
      this.help.toggleCenter();
      return;
    }
    if (event.key === 'Escape' && this.open) {
      this.close();
    }
  }

  close(): void {
    this.help.closeCenter();
  }

  onQueryChange(q: string): void {
    this.help.setSearchQuery(q);
  }

  select(article: HelpArticle): void {
    this.selected = article;
  }

  private runSearch(): void {
    this.results = this.help.search(this.query);
    this.glossary = this.help.searchGlossary(this.query);
    if (this.selected && !this.results.some(r => r.key === this.selected!.key)) {
      this.selected = this.results[0] || null;
    } else if (!this.selected) {
      this.selected = this.results[0] || null;
    }
  }
}
