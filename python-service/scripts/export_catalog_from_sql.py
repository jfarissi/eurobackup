"""
Script pour exporter le catalogue produit depuis SQL Server vers JSON.
Utilise les tables Products, ProductVariants, ProductImages, ProductAttributeValues.
"""
import json
import os
import sys
from pathlib import Path

# Ajouter le répertoire parent au path pour les imports
sys.path.insert(0, str(Path(__file__).parent.parent))

try:
    import pyodbc
except ImportError:
    try:
        import pymssql
        USE_PYMSSQL = True
    except ImportError:
        print("ERREUR: pyodbc ou pymssql est requis pour exporter depuis SQL")
        print("Installez avec: pip install pyodbc ou pip install pymssql")
        sys.exit(1)
else:
    USE_PYMSSQL = False


def get_connection():
    """Crée une connexion à la base de données SQL Server"""
    # Configuration depuis les variables d'environnement
    server = os.getenv('SQL_SERVER', 'localhost')
    database = os.getenv('SQL_DATABASE', 'PulseERP')
    username = os.getenv('SQL_USERNAME', '')
    password = os.getenv('SQL_PASSWORD', '')
    
    if USE_PYMSSQL:
        return pymssql.connect(server, username, password, database)
    else:
        connection_string = (
            f"DRIVER={{ODBC Driver 17 for SQL Server}};"
            f"SERVER={server};"
            f"DATABASE={database};"
            f"UID={username};"
            f"PWD={password}"
        )
        return pyodbc.connect(connection_string)


def export_products():
    """Exporte tous les produits depuis SQL vers JSON"""
    print("Connexion à la base de données...")
    conn = get_connection()
    cursor = conn.cursor()
    
    print("Export des produits...")
    
    # Requête pour les produits
    products_query = """
    SELECT 
        [Id], [Reference], [Sku], [Barcode], [Gtin],
        [Name_EN], [Name_FR], [Name_NL],
        [Description_EN], [Description_FR], [Description_NL],
        [SellingPrice], [CostPrice], [StockQuantity], [WeightKg],
        [BrandId], [CategoryId], [IsActive]
    FROM [PulseERP].[dbo].[Products]
    WHERE [IsActive] = 1
    ORDER BY [Id]
    """
    
    cursor.execute(products_query)
    columns = [column[0] for column in cursor.description]
    
    products = []
    for row in cursor.fetchall():
        product = dict(zip(columns, row))
        product_id = product['Id']
        
        # Récupérer les variantes
        variants_query = """
        SELECT 
            [Id], [ProductId], [Sku], [Barcode], [PriceOverride],
            [StockQuantity], [Weight], [Length], [Width], [Height], [IsActive]
        FROM [PulseERP].[dbo].[ProductVariants]
        WHERE [ProductId] = ? AND [IsActive] = 1
        """
        cursor.execute(variants_query, (product_id,))
        variant_columns = [column[0] for column in cursor.description]
        variants = []
        for variant_row in cursor.fetchall():
            variants.append(dict(zip(variant_columns, variant_row)))
        
        product['variants'] = variants
        products.append(product)
    
    print(f"{len(products)} produits exportés")
    
    # Sauvegarder en JSON
    output_path = Path(__file__).parent.parent / 'app' / 'data' / 'product_catalog.json'
    output_path.parent.mkdir(parents=True, exist_ok=True)
    
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(products, f, indent=2, ensure_ascii=False, default=str)
    
    print(f"Catalogue sauvegardé dans: {output_path}")
    print(f"Taille du fichier: {output_path.stat().st_size / 1024:.2f} KB")
    
    conn.close()
    return products


if __name__ == '__main__':
    try:
        export_products()
        print("Export terminé avec succès!")
    except Exception as e:
        print(f"ERREUR lors de l'export: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
