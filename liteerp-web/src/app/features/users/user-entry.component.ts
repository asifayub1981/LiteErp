import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { API_URL } from '../../core/api';

interface Item { id: number; name: string; }

@Component({
  selector: 'app-user-entry',
  standalone: true,
  imports: [RouterModule, FormsModule, SelectModule, ButtonModule, InputTextModule,
    PasswordModule, CheckboxModule, CardModule, ToolbarModule, ToastModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <p-toolbar>
      <ng-template #start>
        <span class="font-bold text-lg">{{ isNew() ? 'New User' : 'Edit User' }}</span>
      </ng-template>
      <ng-template #end>
        <div class="flex gap-2">
          <p-button  label="Back to List" icon="pi pi-arrow-left" severity="secondary"
            routerLink="/users"></p-button>
          @if (!isNew()) {
            <p-button  label="Rights" icon="pi pi-key" severity="secondary"
              [routerLink]="['/users', id, 'rights']"></p-button>
          }
          <p-button  label="Save" icon="pi pi-check" (click)="save()" [loading]="saving()"></p-button>
        </div>
      </ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <div class="grid">
        <div class="col-12 md:col-6">
          <div class="report-field">
            <label>Username *</label>
            <input pInputText [(ngModel)]="f.userName" class="w-full"
              placeholder="e.g. ahmed.khan" [disabled]="!isNew()" autocomplete="off" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="report-field">
            <label>Display Name *</label>
            <input pInputText [(ngModel)]="f.displayName" class="w-full"
              placeholder="e.g. Ahmed Khan" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="report-field">
            <label>{{ isNew() ? 'Password *' : 'New Password (leave empty to keep)' }}</label>
            <p-password [(ngModel)]="f.password" [feedback]="false" [toggleMask]="true"
              styleClass="w-full" inputStyleClass="w-full" autocomplete="new-password" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="report-field">
            <label>Default Company</label>
            <p-select [options]="companies" [(ngModel)]="f.companyId" optionLabel="name"
              optionValue="id" placeholder="(none)" [showClear]="true" styleClass="w-full" />
          </div>
        </div>
        <div class="col-12">
          <div class="flex align-items-center gap-2">
            <p-checkbox [(ngModel)]="f.isActive" [binary]="true" inputId="act" />
            <label for="act" class="text-sm">Active</label>
          </div>
        </div>
      </div>
      @if (isNew()) {
        <small class="text-500">New users get full rights to all forms by default — adjust them on the Rights page after saving.</small>
      }
    </p-card>
  `,
})
export class UserEntryComponent implements OnInit {
  isNew = signal(true);
  saving = signal(false);
  id = '';
  companies: Item[] = [];
  f = { userName: '', displayName: '', password: '', companyId: null as number | null, isActive: true };

  constructor(
    private http: HttpClient, private route: ActivatedRoute,
    private router: Router, private toast: MessageService,
  ) {}

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`)
      .subscribe(cs => this.companies = cs);
    this.route.paramMap.subscribe(p => {
      const idp = p.get('id');
      if (idp && idp !== 'new') { this.id = idp; this.isNew.set(false); this.loadUser(); }
    });
  }

  private loadUser() {
    this.http.get<any>(`${API_URL}/api/users/${this.id}`).subscribe(u => {
      this.f = { userName: u.userName, displayName: u.displayName, password: '',
        companyId: u.companyId, isActive: u.isActive };
    });
  }

  save() {
    if (!this.f.userName.trim() || !this.f.displayName.trim()) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Username and Display Name are required.' });
      return;
    }
    if (this.isNew() && !this.f.password) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Password is required for new users.' });
      return;
    }
    this.saving.set(true);
    const body = {
      userName: this.f.userName.trim(), displayName: this.f.displayName.trim(),
      password: this.f.password || null, companyId: this.f.companyId, isActive: this.f.isActive,
    };
    const req = this.isNew()
      ? this.http.post(`${API_URL}/api/users`, body)
      : this.http.put(`${API_URL}/api/users/${this.id}`, body);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.add({ severity: 'success', summary: 'Saved' });
        this.router.navigate(['/users']);
      },
      error: (e) => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Error', detail: e.error?.message ?? 'Save failed.' });
      }
    });
  }
}
