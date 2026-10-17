import { Component } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterModule, MenubarModule, ButtonModule],
  template: `
    <p-menubar [model]="menu">
      <ng-template pTemplate="end">
        <div class="flex align-items-center gap-2">
          <span class="text-600 text-sm">{{ auth.displayName() }}</span>
          <button pButton icon="pi pi-sign-out" label="Logout" severity="secondary"
            size="small" (click)="auth.logout()"></button>
        </div>
      </ng-template>
    </p-menubar>
    <div class="p-3">
      <router-outlet />
    </div>
  `
})
export class ShellComponent {
  menu: MenuItem[] = [
    { label: 'COA Definition', icon: 'pi pi-sitemap', routerLink: '/coa' },
    {
      label: 'Reports', icon: 'pi pi-file',
      items: [
        { label: 'General Ledger', icon: 'pi pi-book', routerLink: '/reports/gl' },
        { label: 'Trial Balance', icon: 'pi pi-table', routerLink: '/reports/trial-balance' }
      ]
    }
  ];

  constructor(public auth: AuthService, private router: Router) {}
}
