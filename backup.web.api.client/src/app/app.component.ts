import { Component } from '@angular/core';
import { PermissionRealtimeService } from './services/permission-realtime.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'backup.web.api.client';

  constructor(_permissionsRealtime: PermissionRealtimeService) {
    // Injecté pour démarrer la connexion SignalR dès le bootstrap.
  }
}
