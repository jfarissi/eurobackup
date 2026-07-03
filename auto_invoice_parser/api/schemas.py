from pydantic import BaseModel
from typing import List, Optional

class Item(BaseModel):
    sku: Optional[str]
    ean: Optional[str]
    description: Optional[str]
    qty: Optional[float]
    unit_price: Optional[float]
    line_total: Optional[float]

class ParseResult(BaseModel):
    items: List[Item]
    metadata: Optional[dict]
