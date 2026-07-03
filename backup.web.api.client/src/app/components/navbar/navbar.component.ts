import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../material.module';

@Component({
  selector: 'app-navbar',
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule]
})
export class NavbarComponent {
  navItems = [
    { path: '/upload', label: '📤 Upload', icon: 'cloud_upload' },
    { path: '/compare', label: '🔗 Association', icon: 'link' },
    { path: '/stock', label: '📦 Stock', icon: 'inventory_2' },
    { path: '/python-test', label: '🐍 Python / Ollama', icon: 'science' }
  ];
}

