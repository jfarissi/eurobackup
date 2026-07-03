# Utils module
from .product_catalog import (
    ProductCatalog,
    ProductCatalogItem,
    get_catalog,
    load_catalog,
    enrich_products_with_catalog
)
from .catalog_prompt_builder import (
    build_catalog_context_for_ai,
    build_catalog_summary_for_ai,
    add_catalog_to_prompt
)

__all__ = [
    'ProductCatalog',
    'ProductCatalogItem',
    'get_catalog',
    'load_catalog',
    'enrich_products_with_catalog',
    'build_catalog_context_for_ai',
    'build_catalog_summary_for_ai',
    'add_catalog_to_prompt'
]
