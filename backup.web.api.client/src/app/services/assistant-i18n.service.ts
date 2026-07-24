import { Injectable } from '@angular/core';

export type AssistantLang = 'fr' | 'nl' | 'en';

const STORAGE_KEY = 'store_assistant_lang';

const DICT: Record<AssistantLang, Record<string, string>> = {
  fr: {
    title: 'Assistant magasin',
    subtitle: 'Conseils produits, devis et commande',
    project: 'Projet',
    budget: 'Budget',
    cart: 'Panier',
    cartEmpty: 'Aucun produit dans le panier.',
    close: 'Fermer',
    remove: 'Retirer',
    welcome: 'Bonjour ! Je suis l’assistant magasin. Demandez un produit, une marque ou un projet (peinture, électricité…).',
    placeholder: 'Ex. peinture blanche 10L, ampoule LED, perceuse…',
    send: 'Envoyer',
    quote: 'Demander un devis',
    order: 'Commander',
    downloadQuote: 'Télécharger le devis',
    downloadInvoice: 'Télécharger la facture',
    payCard: 'Payer par carte',
    product: 'Produit',
    price: 'Prix',
    qty: 'Qté',
    error: 'Désolé, une erreur est survenue. Réessayez.',
    newProject: 'Nouveau projet',
    photo: 'Joindre une photo',
    listening: 'Écoute en cours…',
    lang: 'Langue'
  },
  nl: {
    title: 'Winkelassistent',
    subtitle: 'Productadvies, offerte en bestelling',
    project: 'Project',
    budget: 'Budget',
    cart: 'Winkelwagen',
    cartEmpty: 'Geen producten in de winkelwagen.',
    close: 'Sluiten',
    remove: 'Verwijderen',
    welcome: 'Hallo! Ik ben de winkelassistent. Vraag een product, merk of project (verf, elektriciteit…).',
    placeholder: 'Bv. witte muurverf 10L, LED-lamp, boormachine…',
    send: 'Verzenden',
    quote: 'Offerte vragen',
    order: 'Bestellen',
    downloadQuote: 'Offerte downloaden',
    downloadInvoice: 'Factuur downloaden',
    payCard: 'Betalen met kaart',
    product: 'Product',
    price: 'Prijs',
    qty: 'Aantal',
    error: 'Sorry, er is een fout opgetreden. Probeer opnieuw.',
    newProject: 'Nieuw project',
    photo: 'Foto toevoegen',
    listening: 'Luisteren…',
    lang: 'Taal'
  },
  en: {
    title: 'Store assistant',
    subtitle: 'Product advice, quote and order',
    project: 'Project',
    budget: 'Budget',
    cart: 'Cart',
    cartEmpty: 'No products in the cart.',
    close: 'Close',
    remove: 'Remove',
    welcome: 'Hello! I am the store assistant. Ask for a product, brand or project (paint, electrical…).',
    placeholder: 'E.g. white wall paint 10L, LED bulb, drill…',
    send: 'Send',
    quote: 'Request quote',
    order: 'Order',
    downloadQuote: 'Download quote',
    downloadInvoice: 'Download invoice',
    payCard: 'Pay by card',
    product: 'Product',
    price: 'Price',
    qty: 'Qty',
    error: 'Sorry, something went wrong. Please try again.',
    newProject: 'New project',
    photo: 'Attach a photo',
    listening: 'Listening…',
    lang: 'Language'
  }
};

@Injectable({ providedIn: 'root' })
export class AssistantI18nService {
  readonly languages: { code: AssistantLang; label: string }[] = [
    { code: 'fr', label: 'FR' },
    { code: 'nl', label: 'NL' },
    { code: 'en', label: 'EN' }
  ];

  private _lang: AssistantLang = this.readStored();

  get lang(): AssistantLang {
    return this._lang;
  }

  setLang(lang: AssistantLang): void {
    this._lang = lang;
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      /* ignore */
    }
  }

  t(key: string): string {
    return DICT[this._lang][key] ?? DICT.fr[key] ?? key;
  }

  speechLocale(): string {
    switch (this._lang) {
      case 'nl': return 'nl-BE';
      case 'en': return 'en-GB';
      default: return 'fr-FR';
    }
  }

  private readStored(): AssistantLang {
    try {
      const raw = (localStorage.getItem(STORAGE_KEY) || 'fr').toLowerCase();
      if (raw === 'nl' || raw === 'en' || raw === 'fr') return raw;
    } catch {
      /* ignore */
    }
    return 'fr';
  }
}
