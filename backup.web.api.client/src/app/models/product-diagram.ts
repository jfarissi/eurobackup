export interface DiagramHotspot {
  id: string;
  label: string;
  shape: string;
  x: number;
  y: number;
  w: number;
  h: number;
  targetProductId: number;
  targetName?: string | null;
  targetReference?: string | null;
}

export interface ProductDiagram {
  id: string;
  productId: number;
  title: string;
  imageUrl: string;
  mediaKind: string;
  source: string;
  hotspots: DiagramHotspot[];
}
