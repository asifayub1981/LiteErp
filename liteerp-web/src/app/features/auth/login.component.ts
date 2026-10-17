import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, CardModule, InputTextModule, PasswordModule, ButtonModule, MessageModule],
  template: `
    <div class="flex align-items-center justify-content-center min-h-screen surface-ground">
      <p-card header="Lite ERP" subheader="Sign in to continue" styleClass="w-full" [style]="{ maxWidth: '380px' }">
        <div class="flex flex-column gap-3">
          <span class="p-float-label">
            <input pInputText id="username" [(ngModel)]="username" class="w-full" autocomplete="username" />
            <label for="username">Username</label>
          </span>
          <span class="p-float-label">
            <p-password [(ngModel)]="password" [feedback]="false" [toggleMask]="true"
              inputId="password" styleClass="w-full" inputStyleClass="w-full"
              (keydown.enter)="doLogin()" />
            <label for="password">Password</label>
          </span>
          @if (error()) {
            <p-message severity="error" styleClass="w-full">{{ error() }}</p-message>
          }
          <button pButton label="Login" class="w-full" [loading]="busy()"
            (click)="doLogin()"></button>
          <small class="text-500">Demo: admin&#64;liteerp.local / Admin123!</small>
        </div>
      </p-card>
    </div>
  `
})
export class LoginComponent {
  username = '';
  password = '';
  busy = signal(false);
  error = signal<string | null>(null);

  constructor(private auth: AuthService, private router: Router) {}

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
