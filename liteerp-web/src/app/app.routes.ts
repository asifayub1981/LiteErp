import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/shell/shell.component').then(m => m.ShellComponent),
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'coa',
        loadComponent: () => import('./features/coa/coa-tree.component').then(m => m.CoaTreeComponent)
      },
      {
        path: 'vouchers',
        loadComponent: () => import('./features/vouchers/voucher-list.component').then(m => m.VoucherListComponent)
      },
      {
        path: 'vouchers/new',
        loadComponent: () => import('./features/vouchers/voucher-entry.component').then(m => m.VoucherEntryComponent)
      },
      {
        path: 'vouchers/:id',
        loadComponent: () => import('./features/vouchers/voucher-entry.component').then(m => m.VoucherEntryComponent)
      },
      {
        path: 'parties',
        loadComponent: () => import('./features/parties/party-list.component').then(m => m.PartyListComponent),
      },
      {
        path: 'parties/new',
        loadComponent: () => import('./features/parties/party-entry.component').then(m => m.PartyEntryComponent),
      },
      {
        path: 'parties/:id',
        loadComponent: () => import('./features/parties/party-entry.component').then(m => m.PartyEntryComponent),
      },
      {
        path: 'users',
        loadComponent: () => import('./features/users/user-list.component').then(m => m.UserListComponent),
      },
      {
        path: 'users/new',
        loadComponent: () => import('./features/users/user-entry.component').then(m => m.UserEntryComponent),
      },
      {
        path: 'users/:id',
        loadComponent: () => import('./features/users/user-entry.component').then(m => m.UserEntryComponent),
      },
      {
        path: 'users/:id/rights',
        loadComponent: () => import('./features/users/user-rights.component').then(m => m.UserRightsComponent)
      },
      {
        path: 'invoices/sales',
        loadComponent: () => import('./features/invoices/invoice-list.component').then(m => m.InvoiceListComponent),
        data: { kind: 'sales' }
      },
      {
        path: 'invoices/sales/new',
        loadComponent: () => import('./features/invoices/invoice-entry.component').then(m => m.InvoiceEntryComponent),
        data: { kind: 'sales' }
      },
      {
        path: 'invoices/sales/:id',
        loadComponent: () => import('./features/invoices/invoice-entry.component').then(m => m.InvoiceEntryComponent),
        data: { kind: 'sales' }
      },
      {
        path: 'invoices/purchase',
        loadComponent: () => import('./features/invoices/invoice-list.component').then(m => m.InvoiceListComponent),
        data: { kind: 'purchase' }
      },
      {
        path: 'invoices/purchase/new',
        loadComponent: () => import('./features/invoices/invoice-entry.component').then(m => m.InvoiceEntryComponent),
        data: { kind: 'purchase' }
      },
      {
        path: 'invoices/purchase/:id',
        loadComponent: () => import('./features/invoices/invoice-entry.component').then(m => m.InvoiceEntryComponent),
        data: { kind: 'purchase' }
      },
      {
        path: 'reports/party-statement',
        loadComponent: () => import('./features/reports/party-statement.component').then(m => m.PartyStatementComponent)
      },
      {
        path: 'reports/gl',
        loadComponent: () => import('./features/reports/gl-report.component').then(m => m.GlReportComponent)
      },
      {
        path: 'reports/trial-balance',
        loadComponent: () => import('./features/reports/trial-balance.component').then(m => m.TrialBalanceComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
