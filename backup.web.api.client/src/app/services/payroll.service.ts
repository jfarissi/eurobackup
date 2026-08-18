import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  PayrollEmployee,
  PayrollEmployeeForm,
  PayrollPeriodSummary,
  PayrollPostResult
} from '../models/accounting';

/** Accès API paie / CNSS (api/payroll). */
@Injectable({
  providedIn: 'root'
})
export class PayrollApiService {
  constructor(private http: HttpClient) {}

  listEmployees(): Observable<PayrollEmployee[]> {
    return this.http.get<PayrollEmployee[]>('/api/payroll/employees');
  }

  createEmployee(form: PayrollEmployeeForm): Observable<PayrollEmployee> {
    return this.http.post<PayrollEmployee>('/api/payroll/employees', form);
  }

  updateEmployee(id: number, form: PayrollEmployeeForm): Observable<PayrollEmployee> {
    return this.http.put<PayrollEmployee>(`/api/payroll/employees/${id}`, form);
  }

  listPayslips(year: number, month: number): Observable<PayrollPeriodSummary> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<PayrollPeriodSummary>('/api/payroll/payslips', { params });
  }

  calculate(year: number, month: number, employeeId?: number): Observable<unknown> {
    return this.http.post('/api/payroll/payslips/calculate', {
      employeeId: employeeId ?? null,
      year,
      month
    });
  }

  postMonth(year: number, month: number): Observable<PayrollPostResult> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.post<PayrollPostResult>('/api/payroll/payslips/post', {}, { params });
  }

  downloadCnss(year: number, month: number, format?: string) {
    let params = new HttpParams().set('year', year).set('month', month);
    if (format) params = params.set('format', format);
    return this.http.get('/api/payroll/cnss', { params, responseType: 'blob', observe: 'response' as const });
  }
}
