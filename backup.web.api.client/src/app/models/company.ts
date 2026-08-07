export interface Company {
  id: string;
  name: string;
  tenantId?: string;
  isActive?: boolean;
  defaultLanguageCode?: string;
  defaultCurrencyCode?: string;
  enableErpCatalogSync?: boolean;
}
