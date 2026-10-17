import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { API_URL } from '../../core/api';
import { PARTY_TYPES } from './party-list.component';

interface Item { id: number; name: string; }

@Component({
  selector: 'app-party-entry',
  standalone: true,
  imports: [RouterModule, FormsModule, SelectModule, ButtonModule, InputTextModule,
    InputNumberModule, CheckboxModule, CardModule, ToolbarModule, ToastModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <p-toolbar>
      <ng-template #start>
        <span class="font-bold text-lg">{{ isNew() ? 'New Party' : 'Edit Party' }}</span>
      </ng-template>
      <ng-template #end>
        <div class="flex gap-2">
          <p-button  label="Back to List" icon="pi pi-arrow-left" severity="secondary"
            routerLink="/parties"></p-button>
          <p-button  label="Save" icon="pi pi-check" (click)="save()" [loading]="saving()"></p-button>
        </div>
      </ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <div class="grid">
        <div class="col-12 md:col-6">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Company *</label>
            <p-select [options]="companies" [(ngModel)]="f.companyId" optionLabel="name"
              optionValue="id" placeholder="Select company" styleClass="w-full"
              [disabled]="!isNew()" (onChange)="onCompany()" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Type *</label>
            <p-select [options]="types" [(ngModel)]="f.partyType" optionLabel="label"
              optionValue="value" styleClass="w-full" [disabled]="!isNew()" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Code</label>
            <input pInputText [value]="isNew() ? '(auto at save)' : f.code" disabled class="w-full" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Name *</label>
            <input pInputText [(ngModel)]="f.name" class="w-full" placeholder="Party name" />
          </div>
        </div>
        <div class="col-12">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Account Head (AR/AP)</label>
            <p-select [options]="heads" [(ngModel)]="f.headId" optionLabel="name" optionValue="id"
              placeholder="(default control head)" [showClear]="true" [filter]="true"
              filterBy="name" styleClass="w-full" />
            <small class="text-500">Leave empty to use the default {{ f.partyType === 1 ? 'receivable' : 'payable' }} head.</small>
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Phone</label>
            <input pInputText [(ngModel)]="f.phone" class="w-full" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Tax No</label>
            <input pInputText [(ngModel)]="f.taxNo" class="w-full" />
          </div>
        </div>
        <div class="col-12">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Address</label>
            <input pInputText [(ngModel)]="f.address" class="w-full" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">Credit Limit</label>
            <p-inputnumber [(ngModel)]="f.creditLimit" mode="decimal"
              [minFractionDigits]="2" styleClass="w-full" />
          </div>
        </div>
        <div class="col-12 md:col-6">
          <div class="flex align-items-center gap-2 h-full pt-4">
            <p-checkbox [(ngModel)]="f.isActive" [binary]="true" inputId="act" />
            <label for="act" class="text-sm">Active</label>
          </div>
        </div>
      </div>
    </p-card>
  `,
})
export class PartyEntryComponent implements OnInit {
  isNew = signal(true);
  saving = signal(false);
  types = PARTY_TYPES;
  companies: Item[] = [];
  heads: Item[] = [];
  f = {
    companyId: null as number | null, partyType: 1, code: '',
    name: '', headId: null as number | null, phone: '', taxNo: '',
    address: '', creditLimit: null as number | null, isActive: true,
  };
  private id = 0;

  constructor(
    private http: HttpClient, private route: ActivatedRoute,
    private router: Router, private toast: MessageService,
  ) {}

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(cs => {
      this.companies = cs;
      if (cs.length === 1) { this.f.companyId = cs[0].id; this.onCompany(); }
    });
    this.route.paramMap.subscribe(p => {
      const idp = p.get('id');
      if (idp) { this.id = +idp; this.isNew.set(false); this.loadParty(); }
    });
  }

  onCompany() {
    if (!this.f.companyId) return;
    this.http.get<Item[]>(`${API_URL}/api/lookups/heads`, { params: { companyId: this.f.companyId } })
      .subscribe(hs => this.heads = hs);
  }

  private loadParty() {
    this.http.get<any>(`${API_URL}/api/parties/${this.id}`).subscribe(p => {
      this.f = {
        companyId: p.companyId, partyType: p.partyType, code: p.code, name: p.name,
        headId: p.accountHeadId, phone: p.phone ?? '', taxNo: p.taxNo ?? '',
        address: p.address ?? '', creditLimit: p.creditLimit, isActive: p.isActive,
      };
      this.onCompany();
    });
  }

  save() {
    if (!this.f.companyId || !this.f.name.trim()) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Company and Name are required.' });
      return;
    }
    this.saving.set(true);
    const body = {
      companyId: this.f.companyId, partyType: this.f.partyType, name: this.f.name.trim(),
      accountHeadId: this.f.headId, phone: this.f.phone || null, taxNo: this.f.taxNo || null,
      address: this.f.address || null, creditLimit: this.f.creditLimit, isActive: this.f.isActive,
    };
    const req = this.isNew()
      ? this.http.post(`${API_URL}/api/parties`, body)
      : this.http.put(`${API_URL}/api/parties/${this.id}`, body);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.add({ severity: 'success', summary: 'Saved' });
        this.router.navigate(['/parties']);
      },
      error: (e) => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Error', detail: e.error?.message ?? 'Save failed.' });
      }
    });
  }
}
