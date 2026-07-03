import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/**
 * Interface pour les items extraits par le parser Python
 */
export interface ParsedItem {
  sku?: string | null;
  ean?: string | null;
  barcode_raw?: string | null;
  description?: string | null;
  qty?: number | null;
  unit?: string | null;
  unit_price?: number | null;
  line_total?: number | null;
}

/**
 * Interface pour les métadonnées extraites
 */
export interface ParsedMetadata {
  doc_type?: string | null;
  /** Alias courant renvoyé par le parseur (ex. facture / catalogue) */
  type?: string | null;
  number?: string | null;
  client?: string | null;
  supplier?: string | null;
  date?: string | null;
  /** Méthode d’extraction (ex. ollama, openai) */
  method?: string | null;
  supplier_code?: string | null;
  supplier_address?: string | null;
  supplier_phone?: string | null;
  supplier_email?: string | null;
  supplier_contact?: string | null;
  supplier_payment_terms?: string | null;
  /** Somme des regelbedrag / line_total (parser Pardaen) */
  lines_total_excl_vat?: number | null;
  /** Subtotaal HT pied de facture */
  invoice_total_excl_vat?: number | null;
  total_discrepancy?: number | null;
}

/**
 * Interface pour la réponse complète du parser Python
 */
export interface ParseResult {
  items: ParsedItem[];
  metadata: ParsedMetadata;
}

/** Paramètres optionnels pour Ollama sur /parse */
export interface OllamaParseOptions {
  host?: string;
  profile?: string;
  model?: string;
}

/**
 * Service pour tester directement le service Python extractor
 * Utilise le nouvel endpoint /parse de la structure auto_invoice_parser
 */
@Injectable({
  providedIn: 'root'
})
export class PythonExtractorService {
  // Dev: URL absolue backend (upload PDF fiable). Prod: /api/python via même origine.
  private pythonServiceUrl = environment.pythonServiceUrl ?? '/api/python';

  constructor(private http: HttpClient) {}

  /**
   * Parse un document PDF via le nouvel endpoint /parse
   * Retourne les items ET les métadonnées en une seule requête
   * @param file Fichier PDF à parser
   * @param useAi Si true, utilise l'IA pour l'extraction
   * @param aiProvider Fournisseur IA: 'openai', 'gemini' ou 'ollama'
   * @param forceCatalog Si true, force le parsing en tant que catalogue (ignore la détection automatique)
   * @param returnSql Si true et que c'est un catalogue, retourne un script SQL au lieu du JSON
   * @param ollamaOptions Si aiProvider === 'ollama', envoie host / profil / modèle en query
   */
  parsePdf(
    file: File,
    useAi: boolean = false,
    aiProvider: 'openai' | 'gemini' | 'ollama' = 'openai',
    forceCatalog: boolean = false,
    returnSql: boolean = false,
    ollamaOptions?: OllamaParseOptions
  ): Observable<ParseResult | string> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    
    let url = `${this.pythonServiceUrl}/parse`;
    const params = new URLSearchParams();
    if (useAi) {
      params.append('use_ai', 'true');
      params.append('ai_provider', aiProvider);
    }
    if (useAi && aiProvider === 'ollama' && ollamaOptions) {
      if (ollamaOptions.host?.trim()) {
        params.append('ollama_host', ollamaOptions.host.trim());
      }
      if (ollamaOptions.profile?.trim()) {
        params.append('ollama_profile', ollamaOptions.profile.trim());
      }
      if (ollamaOptions.model?.trim()) {
        params.append('ollama_model', ollamaOptions.model.trim());
      }
    }
    if (forceCatalog) {
      params.append('force_catalog', 'true');
    }
    if (returnSql) {
      params.append('return_sql', 'true');
    }
    if (params.toString()) {
      url += '?' + params.toString();
    }
    
    // Si returnSql=true, on attend du texte, sinon du JSON
    if (returnSql) {
      return this.http.post(url, formData, { responseType: 'text' });
    } else {
      return this.http.post<ParseResult>(url, formData);
    }
  }

  /**
   * Health check du service Python
   */
  healthCheck(): Observable<{ status: string; service: string }> {
    return this.http.get<{ status: string; service: string }>(`${this.pythonServiceUrl}/health`);
  }

  /** Modèles Ollama installés (interrogation via le service Python). */
  listOllamaModels(ollamaHost?: string): Observable<{ models: string[]; error: string | null }> {
    let url = `${this.pythonServiceUrl}/ollama/models`;
    const h = ollamaHost?.trim();
    if (h) {
      url += `?ollama_host=${encodeURIComponent(h)}`;
    }
    return this.http.get<{ models: string[]; error: string | null }>(url);
  }

  /**
   * Extrait uniquement les produits (endpoint /extract - existant)
   */
  extractProducts(file: File, useAi: boolean = true): Observable<any[]> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    
    return this.http.post<any[]>(`${this.pythonServiceUrl}/extract?use_ai=${useAi}`, formData);
  }

  /**
   * Extrait uniquement les métadonnées (endpoint /inspect - existant)
   */
  inspectMetadata(file: File, useAi: boolean = true): Observable<ParsedMetadata> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    
    return this.http.post<ParsedMetadata>(`${this.pythonServiceUrl}/inspect?use_ai=${useAi}`, formData);
  }
}

