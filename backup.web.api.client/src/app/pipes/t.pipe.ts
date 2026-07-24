import { Pipe, PipeTransform } from '@angular/core';
import { AppI18nService } from '../services/app-i18n.service';

/** Impure so language switches in navbar refresh all visible labels. */
@Pipe({ name: 't', standalone: true, pure: false })
export class TPipe implements PipeTransform {
  constructor(private i18n: AppI18nService) {}

  transform(key: string, params?: Record<string, string | number>): string {
    return this.i18n.t(key, params);
  }
}
