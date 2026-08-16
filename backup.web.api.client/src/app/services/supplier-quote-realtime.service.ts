import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { CompanyService } from './company.service';
import { SupplierQuotesResult } from '../models/supplier-quote';

@Injectable({ providedIn: 'root' })
export class SupplierQuoteRealtimeService implements OnDestroy {
  private connection: signalR.HubConnection | null = null;
  private watchingProductId: number | null = null;
  private starting = false;
  private readonly quotesSubject = new Subject<SupplierQuotesResult>();
  readonly quotes$ = this.quotesSubject.asObservable();

  constructor(
    private auth: AuthService,
    private company: CompanyService
  ) {}

  async watch(productId: number): Promise<void> {
    if (this.watchingProductId === productId
        && this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }
    await this.unwatch();
    this.watchingProductId = productId;
    await this.ensureConnected();
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('JoinProduct', productId);
      } catch {
        /* hub optional */
      }
    }
  }

  async unwatch(): Promise<void> {
    const id = this.watchingProductId;
    this.watchingProductId = null;
    if (id != null && this.connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('LeaveProduct', id);
      } catch {
        /* ignore */
      }
    }
  }

  ngOnDestroy(): void {
    void this.disconnect();
  }

  private async ensureConnected(): Promise<void> {
    if (this.starting) return;
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;
    this.starting = true;
    try {
      await this.disconnect();
      const hubUrl = environment.signalRSupplierQuotesHubUrl || '/hubs/supplier-quotes';
      const transport = environment.production
        ? signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling
        : signalR.HttpTransportType.WebSockets |
          signalR.HttpTransportType.ServerSentEvents |
          signalR.HttpTransportType.LongPolling;

      const companyId = this.company.activeCompanyId;
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => this.auth.token ?? '',
          withCredentials: false,
          transport,
          headers: companyId ? { 'X-Company-ID': companyId } : {}
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(environment.production ? signalR.LogLevel.Error : signalR.LogLevel.Warning)
        .build();

      this.connection.on('quotesUpdated', (payload: SupplierQuotesResult) => {
        if (payload?.productId === this.watchingProductId) {
          this.quotesSubject.next(payload);
        }
      });

      this.connection.onreconnected(async () => {
        if (this.watchingProductId != null) {
          try {
            await this.connection?.invoke('JoinProduct', this.watchingProductId);
          } catch {
            /* ignore */
          }
        }
      });

      await this.connection.start();
    } catch {
      /* polling via REST refresh remains available */
    } finally {
      this.starting = false;
    }
  }

  private async disconnect(): Promise<void> {
    const conn = this.connection;
    this.connection = null;
    if (!conn) return;
    try {
      await conn.stop();
    } catch {
      /* ignore */
    }
  }
}
