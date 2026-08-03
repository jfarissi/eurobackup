import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../../../material.module';
import { HelpContentService } from '../../../services/help-content.service';
import { TPipe } from '../../../pipes/t.pipe';

@Component({
  selector: 'app-help-walkthrough',
  standalone: true,
  imports: [CommonModule, MaterialModule, TPipe],
  templateUrl: './help-walkthrough.component.html',
  styleUrls: ['./help-walkthrough.component.css']
})
export class HelpWalkthroughComponent implements OnChanges {
  @Input({ required: true }) helpKey!: string;
  @Input() open = false;
  @Output() closed = new EventEmitter<void>();

  steps: string[] = [];
  index = 0;

  constructor(private help: HelpContentService) {}

  ngOnChanges(): void {
    const article = this.help.resolve(this.helpKey);
    this.steps = article.guideSteps || [];
    if (this.open) this.index = 0;
  }

  get current(): string {
    return this.steps[this.index] || '';
  }

  get progress(): string {
    return `${this.index + 1}/${this.steps.length}`;
  }

  next(): void {
    if (this.index < this.steps.length - 1) this.index++;
    else this.close();
  }

  prev(): void {
    if (this.index > 0) this.index--;
  }

  close(): void {
    this.closed.emit();
  }
}
