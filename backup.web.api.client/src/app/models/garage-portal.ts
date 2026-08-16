export interface GarageMe {
  customerId: number;
  customerCode: string;
  name: string;
  status: string;
  email?: string | null;
  phone?: string | null;
  balance: number;
  creditLimit: number;
}

export interface GarageOrder {
  id: number;
  orderNumber: string;
  date: string;
  status: string;
  totalTTC: number;
  lineCount: number;
}

export interface GarageOrderLine {
  lineNumber: number;
  productKey: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalTTC: number;
}

export interface GarageOrderDetail extends GarageOrder {
  totalHT: number;
  lines: GarageOrderLine[];
}

export interface GarageVehicle {
  id: string;
  plateNumber: string;
  country: string;
  vin?: string | null;
  kType?: string | null;
  make?: string | null;
  model?: string | null;
  year?: number | null;
}
