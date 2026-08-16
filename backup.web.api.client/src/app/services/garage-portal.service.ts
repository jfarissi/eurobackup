import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { GarageMe, GarageOrder, GarageOrderDetail, GarageVehicle } from '../models/garage-portal';

@Injectable({ providedIn: 'root' })
export class GaragePortalService {
  private readonly baseUrl = `${environment.apiBaseUrl}/garage`;

  constructor(private http: HttpClient) {}

  me(): Observable<GarageMe> {
    return this.http.get<GarageMe>(`${this.baseUrl}/me`);
  }

  orders(): Observable<GarageOrder[]> {
    return this.http.get<GarageOrder[]>(`${this.baseUrl}/orders`);
  }

  order(id: number): Observable<GarageOrderDetail> {
    return this.http.get<GarageOrderDetail>(`${this.baseUrl}/orders/${id}`);
  }

  vehicles(): Observable<GarageVehicle[]> {
    return this.http.get<GarageVehicle[]>(`${this.baseUrl}/vehicles`);
  }
}
