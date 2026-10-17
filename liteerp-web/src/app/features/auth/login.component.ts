import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, InputTextModule, PasswordModule, ButtonModule, MessageModule],
  template: `
    <div class="login-wrap">
      <!-- Left: form -->
      <div class="login-form-pane">
        <div class="login-form-inner">
          <div class="login-brand">
            <span class="login-logo">L</span>
            <span class="login-brand-name">LiteERP</span>
          </div>
          <h1>Sign In</h1>
          <p class="login-greet">{{ greeting() }}, please sign in to continue.</p>
          <div class="login-fields">
            <label for="username">Username <span class="req">*</span></label>
            <input pInputText id="username" [(ngModel)]="username"
              placeholder="Enter your username" autocomplete="username" />
            <label for="password">Password <span class="req">*</span></label>
            <p-password [(ngModel)]="password" [feedback]="false" [toggleMask]="true"
              inputId="password" placeholder="Enter your password"
              styleClass="w-full" inputStyleClass="w-full"
              (keydown.enter)="doLogin()" />
            @if (error()) {
              <p-message severity="error" styleClass="w-full">{{ error() }}</p-message>
            }
            <p-button  label="Sign in" class="w-full" [loading]="busy()"
              (click)="doLogin()"></p-button>
            <small class="login-demo">Demo: admin&#64;liteerp.local / Admin123!</small>
          </div>
        </div>
      </div>
      <!-- Right: branding -->
      <div class="login-brand-pane">
        <div class="login-clock">{{ clock() }}</div>
        <div class="login-hero">
          <div class="login-hero-logo">LiteERP</div>
          <h2>Welcome Back</h2>
          <p>One platform. Every voucher, invoice and report — under your control.</p>
        </div>
        <div class="login-skyline"></div>
      </div>
    </div>
  `,
  styles: [`
    .login-wrap { display: flex; min-height: 100vh; }
    .login-form-pane {
      flex: 1; display: flex; align-items: center; justify-content: center;
      background: #fff; padding: 2rem;
    }
    .login-form-inner { width: 100%; max-width: 380px; }
    .login-brand { display: flex; align-items: center; gap: .6rem; margin-bottom: 2.2rem; }
    .login-logo {
      width: 42px; height: 42px; border-radius: 12px; background: var(--lite-navy);
      color: #fff; font-weight: 800; font-size: 1.4rem;
      display: flex; align-items: center; justify-content: center;
    }
    .login-brand-name { font-size: 1.35rem; font-weight: 800; color: var(--lite-navy); }
    h1 { font-size: 1.9rem; margin: 0 0 .3rem; color: #0f172a; }
    .login-greet { color: var(--lite-text-soft); margin: 0 0 1.6rem; }
    .login-fields { display: flex; flex-direction: column; gap: .55rem; }
    .login-fields label { font-size: .85rem; font-weight: 600; color: #334155; margin-top: .5rem; }
    .req { color: #ef4444; }
    .login-demo { color: var(--lite-text-soft); text-align: center; margin-top: .4rem; }
    .login-brand-pane {
      flex: 1; background: linear-gradient(160deg, var(--lite-navy) 0%, var(--lite-navy-2) 100%);
      color: #fff; display: flex; flex-direction: column; padding: 2rem;
      position: relative; overflow: hidden;
    }
    .login-clock { font-size: 1.6rem; font-weight: 700; letter-spacing: .02em; }
    .login-hero { margin: auto 0; text-align: center; z-index: 1; }
    .login-hero-logo {
      font-size: 2.6rem; font-weight: 800; letter-spacing: .02em; margin-bottom: 1rem;
    }
    .login-hero h2 { font-size: 1.5rem; margin: 0 0 .6rem; }
    .login-hero p { color: #c7d2fe; max-width: 340px; margin: 0 auto; line-height: 1.6; }
    .login-skyline {
      height: 120px; margin: 0 -2rem -2rem;
      background: linear-gradient(to top, rgba(129,140,248,.25), transparent);
      clip-path: polygon(0 100%, 0 55%, 6% 55%, 6% 35%, 12% 35%, 12% 60%, 20% 60%, 20% 25%, 27% 25%, 27% 50%, 35% 50%, 35% 40%, 43% 40%, 43% 65%, 52% 65%, 52% 30%, 60% 30%, 60% 55%, 68% 55%, 68% 42%, 76% 42%, 76% 60%, 84% 60%, 84% 38%, 92% 38%, 92% 58%, 100% 58%, 100% 100%);
    }
    @media (max-width: 768px) {
      .login-brand-pane { display: none; }
    }
  `]
})
export class LoginComponent {
  username = '';
password = '';
  busy = signal(false);
  error = signal<string | null>(null);
  clock = signal('');
  greeting = signal('Good day');

  constructor(private auth: AuthService, private router: Router) {
    const h = new Date().getHours();
    this.greeting.set(h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening');
    const tick = () => {
      const d = new Date();
      this.clock.set(d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' }));
    };
    tick();
    setInterval(tick, 1000);
  }

  doLogin() {
    this.error.set(null);
    this.busy.set(true);
    this.auth.login(this.username.trim(), this.password).subscribe({
      next: () => this.router.navigate(['/']),
      error: (e) => {
        this.error.set(e.error?.message ?? 'Login failed.');
        this.busy.set(false);
      }
    });
  }
}
