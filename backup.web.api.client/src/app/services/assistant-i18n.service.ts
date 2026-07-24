import { Injectable } from '@angular/core';
import { AppI18nService, AppLang } from './app-i18n.service';

/** @deprecated Use AppI18nService — kept as thin alias for assistant migration. */
export type AssistantLang = AppLang;

@Injectable({ providedIn: 'root' })
export class AssistantI18nService {
  constructor(private app: AppI18nService) {}

  get languages() {
    return this.app.languages;
  }

  get lang(): AppLang {
    return this.app.lang;
  }

  setLang(lang: AppLang): void {
    this.app.setLang(lang);
  }

  t(key: string, params?: Record<string, string | number>): string {
    return this.app.ta(key, params);
  }

  speechLocale(): string {
    return this.app.speechLocale();
  }

  numberLocale(): string {
    return this.app.numberLocale();
  }
}
