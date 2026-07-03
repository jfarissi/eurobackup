"""
Module pour construire des prompts IA avec le catalogue produit.
Permet d'inclure le catalogue dans les prompts pour l'IA afin qu'elle puisse
identifier et enrichir les produits extraits.
"""
from typing import List, Dict, Optional
from .product_catalog import get_catalog, ProductCatalogItem


def build_catalog_context_for_ai(max_products: int = 100) -> str:
    """
    Construit un contexte de catalogue pour les prompts IA
    
    Args:
        max_products: Nombre maximum de produits à inclure dans le contexte
    
    Returns:
        Chaîne de texte formatée avec le catalogue
    """
    catalog = get_catalog()
    if not catalog or not catalog.products:
        return ""
    
    context_lines = [
        "═══════════════════════════════════════════════════════════════════════════════",
        "⚠️ IMPORTANT: CE N'EST PAS UNE FACTURE À PARSER ⚠️",
        "═══════════════════════════════════════════════════════════════════════════════",
        "",
        "CATALOGUE PRODUIT (RÉFÉRENTIEL DE RÉFÉRENCE)",
        "Ceci est un catalogue de produits de référence pour MATCHER les produits extraits.",
        "NE PAS parser ce catalogue comme une facture.",
        "Utilise ce catalogue UNIQUEMENT pour identifier et enrichir les produits extraits de la facture.",
        "",
        "=" * 80,
        ""
    ]
    
    # Limiter le nombre de produits pour ne pas dépasser les limites de tokens
    products_to_include = catalog.products[:max_products]
    
    for i, product in enumerate(products_to_include, 1):
        if not product.is_active:
            continue
        
        product_info = [
            f"Produit #{i} (ID: {product.id})",
            f"  Reference: {product.reference or 'N/A'}",
            f"  SKU: {product.sku or 'N/A'}",
        ]
        
        # Identifiants
        identifiers = []
        if product.barcode:
            identifiers.append(f"Barcode: {product.barcode}")
        if product.gtin:
            identifiers.append(f"GTIN: {product.gtin}")
        if identifiers:
            product_info.append(f"  {' | '.join(identifiers)}")
        
        # Noms
        names = []
        if product.name_nl:
            names.append(f"NL: {product.name_nl}")
        if product.name_fr:
            names.append(f"FR: {product.name_fr}")
        if product.name_en:
            names.append(f"EN: {product.name_en}")
        if names:
            product_info.append(f"  Nom: {' | '.join(names)}")
        
        # Descriptions (tronquées)
        descriptions = []
        if product.description_nl:
            desc_nl = product.description_nl[:100] + "..." if len(product.description_nl) > 100 else product.description_nl
            descriptions.append(f"NL: {desc_nl}")
        if product.description_fr:
            desc_fr = product.description_fr[:100] + "..." if len(product.description_fr) > 100 else product.description_fr
            descriptions.append(f"FR: {desc_fr}")
        if descriptions:
            product_info.append(f"  Description: {' | '.join(descriptions)}")
        
        # Prix
        if product.selling_price:
            product_info.append(f"  Prix vente: {product.selling_price} EUR")
        
        # Variantes
        if product.variants:
            variant_skus = [v.get('Sku', 'N/A') for v in product.variants[:3]]
            product_info.append(f"  Variantes (SKU): {', '.join(variant_skus)}")
        
        context_lines.extend(product_info)
        context_lines.append("")
    
    if len(catalog.products) > max_products:
        context_lines.append(f"... et {len(catalog.products) - max_products} autres produits")
        context_lines.append("")
    
    context_lines.append("=" * 80)
    context_lines.append("")
    context_lines.append("═══════════════════════════════════════════════════════════════════════════════")
    context_lines.append("FIN DU CATALOGUE - Le texte de la facture suit ci-dessous")
    context_lines.append("═══════════════════════════════════════════════════════════════════════════════")
    context_lines.append("")
    context_lines.append("INSTRUCTIONS POUR LE MATCHING:")
    context_lines.append("1. D'ABORD, parse la FACTURE (texte qui suit) pour extraire les produits")
    context_lines.append("2. ENSUITE, pour chaque produit extrait de la facture, cherche-le dans le catalogue ci-dessus")
    context_lines.append("3. Utilise les identifiants (SKU, EAN, Barcode, GTIN) en priorité pour le matching")
    context_lines.append("4. Si aucun identifiant ne correspond, utilise la description pour un match approximatif")
    context_lines.append("5. Si un match est trouvé, ajoute les champs 'catalog_id', 'catalog_name_nl', 'catalog_price', etc.")
    context_lines.append("")
    context_lines.append("⚠️ NE PAS parser le catalogue comme une facture - c'est un référentiel de référence uniquement")
    context_lines.append("")
    
    return "\n".join(context_lines)


def build_catalog_summary_for_ai() -> str:
    """
    Construit un résumé du catalogue pour les prompts IA (plus léger)
    
    Returns:
        Résumé du catalogue
    """
    catalog = get_catalog()
    if not catalog or not catalog.products:
        return ""
    
    active_products = [p for p in catalog.products if p.is_active]
    
    summary = [
        "═══════════════════════════════════════════════════════════════════════════════",
        "⚠️ CATALOGUE PRODUIT (RÉFÉRENTIEL) - NE PAS PARSER COMME UNE FACTURE ⚠️",
        "═══════════════════════════════════════════════════════════════════════════════",
        "",
        f"RÉFÉRENTIEL: {len(active_products)} produits actifs disponibles dans le catalogue",
        f"Identifiants supportés: SKU, EAN (13 chiffres), Barcode, GTIN, Reference",
        f"Langues disponibles: NL, FR, EN",
        "",
        "UTILISATION DU CATALOGUE:",
        "1. D'ABORD, parse la FACTURE (texte fourni séparément) pour extraire les produits",
        "2. ENSUITE, pour chaque produit extrait, cherche-le dans ce catalogue",
        "3. Priorité de matching:",
        "   a) EAN (13 chiffres) - le plus fiable",
        "   b) SKU",
        "   c) Barcode",
        "   d) GTIN",
        "   e) Description (match partiel)",
        "",
        "Si un produit de la facture est trouvé dans le catalogue, ajouter:",
        "- catalog_id: ID du produit dans le catalogue",
        "- catalog_sku: SKU du catalogue",
        "- catalog_name_nl/fr/en: Noms du produit",
        "- catalog_description_nl/fr/en: Descriptions du produit",
        "- catalog_selling_price: Prix de vente",
        "- catalog_matched: true si trouvé, false sinon",
        "",
        "═══════════════════════════════════════════════════════════════════════════════",
        "FIN DU RÉSUMÉ CATALOGUE - Le texte de la facture suit ci-dessous",
        "═══════════════════════════════════════════════════════════════════════════════",
        ""
    ]
    
    return "\n".join(summary)


def add_catalog_to_prompt(base_prompt: str, use_full_catalog: bool = False) -> str:
    """
    Ajoute le contexte du catalogue à un prompt IA
    
    Args:
        base_prompt: Prompt de base
        use_full_catalog: Si True, inclut le catalogue complet (limité), sinon juste le résumé
    
    Returns:
        Prompt enrichi avec le catalogue (catalogue AVANT le prompt de base)
    """
    if use_full_catalog:
        catalog_context = build_catalog_context_for_ai(max_products=50)
    else:
        catalog_context = build_catalog_summary_for_ai()
    
    if not catalog_context:
        return base_prompt
    
    # Le catalogue est ajouté AVANT le prompt de base pour bien séparer
    # Structure: [CATALOGUE] -> [INSTRUCTIONS] -> [TEXTE FACTURE]
    return f"{catalog_context}\n\n{base_prompt}"
