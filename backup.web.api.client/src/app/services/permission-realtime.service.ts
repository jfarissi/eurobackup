import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { Subscription } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class PermissionRealtimeService implements OnDestroy {
  private connection: signalR.HubConnection | null = null;
  private authSub: Subscription | null = null;
  private starting = false;

  constructor(private auth: AuthService) {
    this.authSub = this.auth.user$.subscribe(user => {
      if (user && this.auth.token) {
        void this.connect();
      } else {
        void this.disconnect();
      }
    });
  }

  ngOnDestroy(): void {
    this.authSub?.unsubscribe();
    void this.disconnect();
  }

  private async connect(): Promise<void> {
    if (this.starting) return;
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;

    this.starting = true;
    try {
      await this.disconnect();

      const hubUrl = environment.signalRHubUrl || '/hubs/permissions';
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => this.auth.token ?? '',
          withCredentials: false
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      this.connection.on('permissionsChanged', () => {
        this.auth.refreshSession(true);
      });

      this.connection.onreconnected(() => {
        this.auth.refreshSession(true);
      });

      await this.connection.start();
    } catch (err) {
      console.warn('[SignalR] permissions hub unavailable, using polling fallback', err);
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
