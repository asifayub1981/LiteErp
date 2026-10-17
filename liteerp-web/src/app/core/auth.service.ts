import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { API_URL } from './api';

export interface LoginResponse {
  token: string;
  displayName: string;
  roles: string[];
  expiresOn: string;
}

const TOKEN_KEY = 'liteerp_token';
const USER_KEY = 'liteerp_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly displayName = signal<string | null>(localStorage.getItem(USER_KEY));

  constructor(private http: HttpClient, private router: Router) {}

  get token(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  get isLoggedIn(): boolean {
    return !!this.token;
  }

  login(username: string, password: string) {
    return this.http.post<LoginResponse>(`${API_URL}/api/auth/login`, { username, password }).pipe(
      tap(res => {
        localStorage.setItem(TOKEN_KEY, res.token);
        localStorage.setItem(USER_KEY, res.displayName);
        this.displayName.set(res.displayName);
      })
    );
  }

  logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.displayName.set(null);
    this.router.navigate(['/login']);
  }
}
