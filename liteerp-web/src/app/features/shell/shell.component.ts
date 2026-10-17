import { Component, signal } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../../core/auth.service';

interface NavItem {
  label: string;
  icon: string;
  link?: string;
  children?: NavItem[];
  open?: boolean;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterModule, ButtonModule, ToastModule],
  template: `
    <p-toast position="top-right" />
    <div class="shell">
      <!-- Sidebar -->
      <aside class="sidebar" [class.collapsed]="collapsed()">
        <div class="sidebar-brand" (click)="go('/')">
          <span class="brand-logo">L</span>
          @if (!collapsed()) { <span class="brand-name">LiteERP</span> }
        </div>
        <nav class="sidebar-nav">
          @for (item of nav(); track item.label) {
            @if (item.children) {
              <div class="nav-group">
                <button class="nav-item" (click)="item.open = !item.open"
                  [title]="item.label">
                  <i [class]="item.icon"></i>
                  @if (!collapsed()) {
                    <span class="nav-label">{{ item.label }}</span>
                    <i class="pi pi-chevron-down nav-chev" [class.rot]="item.open"></i>
                  }
                </button>
                @if (item.open && !collapsed()) {
                  <div class="nav-children">
                    @for (c of item.children; track c.label) {
                      <a class="nav-item nav-child" [routerLink]="c.link"
                        routerLinkActive="active" [title]="c.label">
                        <i [class]="c.icon"></i>
                        <span class="nav-label">{{ c.label }}</span>
                      </a>
                    }
                  </div>
                }
              </div>
            } @else {
              <a class="nav-item" [routerLink]="item.link"
                routerLinkActive="active" [routerLinkActiveOptions]="{ exact: item.link === '/' }"
                [title]="item.label">
                <i [class]="item.icon"></i>
                @if (!collapsed()) { <span class="nav-label">{{ item.label }}</span> }
              </a>
            }
          }
        </nav>
        <div class="sidebar-foot">
          @if (!collapsed()) {
            <div class="user-chip">
              <span class="user-avatar">{{ auth.displayName()?.charAt(0) ?? 'U' }}</span>
              <span class="user-name">{{ auth.displayName() }}</span>
            </div>
          }
          <p-button  [icon]="collapsed() ? 'pi pi-angle-right' : 'pi pi-angle-left'"
            severity="secondary" [text]="true" size="small"
            (click)="collapsed.set(!collapsed())"></p-button>
        </div>
      </aside>
      <!-- Main -->
      <div class="main">
        <header class="topbar">
          <div class="topbar-company">
            <i class="pi pi-building"></i>
            <span>Demo Traders (Pvt) Ltd</span>
          </div>
          <div class="topbar-right">
            <span class="topbar-clock">{{ clock() }}</span>
            <p-button  icon="pi pi-sign-out" label="Logout" severity="secondary"
              size="small" (click)="auth.logout()"></p-button>
          </div>
        </header>
        <main class="content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [`
    .shell { display: flex; min-height: 100vh; }
    .sidebar {
      width: 248px; flex-shrink: 0; background: var(--lite-sidebar);
      border-right: 1px solid var(--lite-border);
      display: flex; flex-direction: column; position: sticky; top: 0; height: 100vh;
      transition: width .2s ease;
    }
    .sidebar.collapsed { width: 68px; }
    .sidebar-brand {
      display: flex; align-items: center; gap: .6rem; padding: 1.1rem 1rem;
      cursor: pointer; border-bottom: 1px solid var(--lite-border);
    }
    .brand-logo {
      width: 36px; height: 36px; border-radius: 10px; background: var(--lite-navy);
      color: #fff; font-weight: 800; display: flex; align-items: center; justify-content: center;
      flex-shrink: 0;
    }
    .brand-name { font-weight: 800; font-size: 1.15rem; color: var(--lite-navy); }
    .sidebar-nav { flex: 1; overflow-y: auto; padding: .7rem .6rem; display: flex; flex-direction: column; gap: 2px; }
    .nav-item {
      display: flex; align-items: center; gap: .7rem; padding: .62rem .75rem;
      border-radius: 10px; color: #475569; font-size: .92rem; font-weight: 500;
      text-decoration: none; border: none; background: none; width: 100%;
      cursor: pointer; text-align: left; font-family: inherit;
    }
    .nav-item:hover { background: #f1f5f9; color: var(--lite-navy); }
    .nav-item.active { background: var(--lite-purple-bg); color: var(--lite-purple); font-weight: 600; }
    .nav-item i:first-child { font-size: 1.05rem; width: 20px; text-align: center; flex-shrink: 0; }
    .nav-chev { margin-left: auto; font-size: .75rem; transition: transform .15s; }
    .nav-chev.rot { transform: rotate(180deg); }
    .nav-children { margin-left: 1.1rem; border-left: 2px solid var(--lite-border); padding-left: .4rem; }
    .nav-child { font-size: .87rem; padding: .5rem .75rem; }
    .sidebar.collapsed .nav-item { justify-content: center; padding: .62rem 0; }
    .sidebar-foot {
      border-top: 1px solid var(--lite-border); padding: .7rem;
      display: flex; align-items: center; justify-content: space-between; gap: .5rem;
    }
    .user-chip { display: flex; align-items: center; gap: .55rem; min-width: 0; }
    .user-avatar {
      width: 32px; height: 32px; border-radius: 50%; background: var(--lite-navy);
      color: #fff; font-weight: 700; font-size: .85rem;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }
    .user-name { font-size: .85rem; font-weight: 600; color: #334155; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .main { flex: 1; min-width: 0; display: flex; flex-direction: column; }
    .topbar {
      height: 60px; background: #fff; border-bottom: 1px solid var(--lite-border);
      display: flex; align-items: center; justify-content: space-between;
      padding: 0 1.25rem; position: sticky; top: 0; z-index: 10;
    }
    .topbar-company { display: flex; align-items: center; gap: .55rem; font-weight: 600; color: var(--lite-navy); }
    .topbar-company i { color: var(--lite-purple); }
    .topbar-right { display: flex; align-items: center; gap: .9rem; }
    .topbar-clock { font-size: .85rem; color: var(--lite-text-soft); font-variant-numeric: tabular-nums; }
    .content { flex: 1; }
  `]
})
export class ShellComponent {
  collapsed = signal(false);
  clock = signal('');

  nav = signal<NavItem[]>([
    { label: 'Dashboard', icon: 'pi pi-home', link: '/' },
    { label: 'COA Definition', icon: 'pi pi-sitemap', link: '/coa' },
    { label: 'Vouchers', icon: 'pi pi-receipt', link: '/vouchers' },
    {
      label: 'APAR', icon: 'pi pi-users', open: true, children: [
        { label: 'Parties', icon: 'pi pi-id-card', link: '/parties' },
        { label: 'Sales Invoices', icon: 'pi pi-file-arrow-up', link: '/invoices/sales' },
        { label: 'Purchase Invoices', icon: 'pi pi-file-arrow-down', link: '/invoices/purchase' },
      ]
    },
    {
      label: 'Reports', icon: 'pi pi-chart-bar', children: [
        { label: 'General Ledger', icon: 'pi pi-book', link: '/reports/gl' },
        { label: 'Trial Balance', icon: 'pi pi-table', link: '/reports/trial-balance' },
        { label: 'Party Statement', icon: 'pi pi-address-book', link: '/reports/party-statement' },
      ]
    },
  ]);

  constructor(public auth: AuthService, private router: Router) {
    const tick = () => {
      this.clock.set(new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }));
    };
    tick();
    setInterval(tick, 30000);
  }

  go(link: string) {
    this.router.navigate([link]);
  }
}
