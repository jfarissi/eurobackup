export type SortDir = 'asc' | 'desc';

export type SortValue = string | number | boolean | Date | null | undefined;

/**
 * État de tri partagé pour les tableaux ERP (clic sur en-tête).
 */
export class TableSortState {
  key = '';
  dir: SortDir = 'asc';
  /** Incrémenté à chaque toggle pour forcer le rafraîchissement des getters / pipes. */
  version = 0;

  constructor(initialKey = '', initialDir: SortDir = 'asc') {
    this.key = initialKey;
    this.dir = initialDir;
  }

  toggle(key: string, defaultDir: SortDir = 'asc'): void {
    if (this.key === key) {
      this.dir = this.dir === 'asc' ? 'desc' : 'asc';
    } else {
      this.key = key;
      this.dir = defaultDir;
    }
    this.version++;
  }

  icon(key: string): string {
    if (this.key !== key) return 'unfold_more';
    return this.dir === 'asc' ? 'arrow_upward' : 'arrow_downward';
  }

  sort<T>(
    rows: T[] | null | undefined,
    getters: Record<string, (row: T) => SortValue>
  ): T[] {
    const list = rows ?? [];
    if (!this.key || !getters[this.key]) return list;
    const get = getters[this.key];
    const dir = this.dir === 'asc' ? 1 : -1;
    return [...list].sort((a, b) => compareSortValues(get(a), get(b)) * dir);
  }
}

export function compareSortValues(a: SortValue, b: SortValue): number {
  const na = normalizeSortValue(a);
  const nb = normalizeSortValue(b);
  if (na < nb) return -1;
  if (na > nb) return 1;
  return 0;
}

function normalizeSortValue(v: SortValue): string | number {
  if (v == null) return '';
  if (v instanceof Date) {
    const t = v.getTime();
    return Number.isFinite(t) ? t : 0;
  }
  if (typeof v === 'boolean') return v ? 1 : 0;
  if (typeof v === 'number') return Number.isFinite(v) ? v : 0;
  const s = String(v).trim();
  if (s === '') return '';
  // Dates ISO / parseables
  if (/^\d{4}-\d{2}-\d{2}/.test(s)) {
    const t = Date.parse(s);
    if (Number.isFinite(t)) return t;
  }
  // Nombres (y compris décimales)
  const n = Number(s.replace(',', '.').replace(/\s/g, ''));
  if (s !== '' && Number.isFinite(n) && /^-?\d+([.,]\d+)?$/.test(s.replace(/\s/g, ''))) {
    return n;
  }
  return s.toLowerCase();
}
