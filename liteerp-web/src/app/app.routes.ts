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
      { path: '', redirectTo: 'coa', pathMatch: 'full' },
      {
        path: 'coa',
        loadComponent: () => import('./features/coa/coa-tree.component').then(m => m.CoaTreeComponent)
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
