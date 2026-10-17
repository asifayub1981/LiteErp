import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { API_URL } from '../../core/api';

interface Item { id: number; name: string; }
interface PartyRow {
  id: number; code: string; name: string; partyType: string; headName: string;
  phone: string | null; isActive: boolean; balance: number;
}

export const PARTY_TYPES = [
  { label: 'Customer', value: 1 },
  { label: 'Supplier', value: 2 },
];

@Component({
  selector: 'app-party-list',
  standalone: true,
  imports: [RouterModule, FormsModule, DecimalPipe, SelectModule, ButtonModule,
    TableModule, ToolbarModule, CardModule, InputTextModule, InputNumberModule,
    DialogModule, CheckboxModule, TagModule],
  template: `
    <p-toolbar>
      <ng-template pTemplate="start"><span class="font-bold text-lg">Parties</span></ng-template>
      <ng-template pTemplate="end">
        <p-button label="New Party" icon="pi pi-plus" (click)="openNew()"></p-button>
      </ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <div class="flex flex-wrap gap-3 align-items-end mb-3">
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Company</label>
          <p-select [options]="companies" [(ngModel)]="f.companyId" optionLabel="name" optionValue="id"
            placeholder="Company" styleClass="w-14rem" (onChange)="load()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Type</label>
          <p-select [options]="types" [(ngModel)]="f.type" optionLabel="label" optionValue="value"
            placeholder="All" [showClear]="true" styleClass="w-10rem" (onChange)="load()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Search</label>
          <input pInputText [(ngModel)]="f.search" placeholder="Code / name"
            (keyup.enter)="load()" class="w-14rem" />
        </div>
        <p-button label="Run" icon="pi pi-play" (click)="load()"
          [loading]="busy()" [disabled]="!f.companyId"></p-button>
      </div>

      <p-table [value]="rows()" [loading]="busy()" styleClass="p-datatable-sm" [paginator]="true" [rows]="15">
        <ng-template pTemplate="header">
          <tr><th>Code</th><th>Name</th><th>Type</th><th>Head</th><th>Phone</th>
              <th class="text-right">Balance</th><th>Status</th><th></th></tr>
        </ng-template>
        <ng-template pTemplate="body" let-r>
          <tr>
            <td class="font-bold">{{ r.code }}</td>
            <td>{{ r.name }}</td>
            <td><p-tag [value]="r.partyType" [severity]="r.partyType === 'Customer' ? 'info' : 'warn'"></p-tag></td>
            <td class="text-sm">{{ r.headName }}</td>
            <td>{{ r.phone || '—' }}</td>
            <td class="text-right font-bold">{{ r.balance | number:'1.2-2' }}</td>
            <td><p-tag [value]="r.isActive ? 'Active' : 'Inactive'"
              [severity]="r.isActive ? 'success' : 'danger'"></p-tag></td>
            <td class="text-right">
              <p-button icon="pi pi-pencil" [text]="true" (click)="openEdit(r)"
                pTooltip="Edit"></p-button>
              <p-button icon="pi pi-book" [text]="true" severity="info"
                [routerLink]="['/reports/party-statement']" [queryParams]="{ partyId: r.id }"
                pTooltip="Statement"></p-button>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="8" class="text-center text-500">No parties. Click "New Party".</td></tr>
        </ng-template>
      </p-table>
    </p-card>

    <p-dialog [header]="dlg.isNew ? 'New Party' : 'Edit Party'" [(visible)]="dlg.show"
      [modal]="true" [style]="{ width: '34rem' }">
      <div class="flex flex-column gap-3">
        <div class="flex gap-3">
          <div class="flex flex-column gap-1 flex-1">
            <label class="text-sm text-500">Type *</label>
            <p-select [options]="types" [(ngModel)]="dlg.partyType" optionLabel="label"
              optionValue="value" [disabled]="!dlg.isNew" styleClass="w-full" />
          </div>
          <div class="flex flex-column gap-1 flex-1">
            <label class="text-sm text-500">Code</label>
            <input pInputText [value]="dlg.isNew ? '(auto at save)' : dlg.code" disabled class="w-full" />
          </div>
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Name *</label>
          <input pInputText [(ngModel)]="dlg.name" class="w-full" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Account Head (AR/AP)</label>
          <p-select [options]="heads" [(ngModel)]="dlg.headId" optionLabel="name" optionValue="id"
            placeholder="(default control head)" [showClear]="true" [filter]="true"
            filterBy="name" styleClass="w-full" />
          <small class="text-500">Leave empty to use the default {{ dlg.partyType === 1 ? 'receivable' : 'payable' }} head.</small>
        </div>
        <div class="flex gap-3">
          <div class="flex flex-column gap-1 flex-1">
            <label class="text-sm text-500">Phone</label>
            <input pInputText [(ngModel)]="dlg.phone" class="w-full" />
          </div>
          <div class="flex flex-column gap-1 flex-1">
            <label class="text-sm text-500">Tax No</label>
            <input pInputText [(ngModel)]="dlg.taxNo" class="w-full" />
          </div>
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Address</label>
          <input pInputText [(ngModel)]="dlg.address" class="w-full" />
        </div>
        <div class="flex gap-3 align-items-end">
          <div class="flex flex-column gap-1 flex-1">
            <label class="text-sm text-500">Credit Limit</label>
            <p-inputnumber [(ngModel)]="dlg.creditLimit" mode="decimal"
              [minFractionDigits]="2" styleClass="w-full" />
          </div>
          <div class="flex align-items-center gap-2 pb-2">
            @if (!dlg.isNew) {
              <p-checkbox [(ngModel)]="dlg.isActive" [binary]="true" inputId="pa" />
              <label for="pa" class="text-sm">Active</label>
            }
          </div>
        </div>
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" severity="secondary" [text]="true" (click)="dlg.show = false"></p-button>
        <p-button label="Save" icon="pi pi-check" (click)="save()" [loading]="saving()"></p-button>
      </ng-template>
    </p-dialog>
  `,
})
export class PartyListComponent implements OnInit {
  constructor(private http: HttpClient, private toast: MessageService) {}

  companies: Item[] = [];
  heads: Item[] = [];
  types = PARTY_TYPES;
  rows = signal<PartyRow[]>([]);
  busy = signal(false);
  saving = signal(false);
  f = { companyId: 0 as number, type: null as number | null, search: '' };

  dlg = {
    show: false, isNew: true, id: 0, code: '',
    partyType: 1, name: '', headId: null as number | null,
    phone: '', taxNo: '', address: '', creditLimit: 0, isActive: true,
  };

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(c => {
      this.companies = c;
      if (c.length === 1) { this.f.companyId = c[0].id; this.load(); }
    });
  }

  loadHeads() {
    if (!this.f.companyId) return;
    this.http.get<Item[]>(`${API_URL}/api/lookups/heads?companyId=${this.f.companyId}`)
      .subscribe(h => this.heads = h);
  }

  load() {
    if (!this.f.companyId) return;
    this.busy.set(true);
    this.loadHeads();
    let url = `${API_URL}/api/parties?companyId=${this.f.companyId}`;
    if (this.f.type) url += `&type=${this.f.type}`;
    if (this.f.search) url += `&search=${encodeURIComponent(this.f.search)}`;
    this.http.get<PartyRow[]>(url).subscribe({
      next: r => { this.rows.set(r); this.busy.set(false); },
      error: e => { this.busy.set(false); this.err(e); },
    });
  }

  openNew() {
    this.dlg = { show: true, isNew: true, id: 0, code: '', partyType: 1, name: '',
      headId: null, phone: '', taxNo: '', address: '', creditLimit: 0, isActive: true };
  }

  openEdit(r: PartyRow) {
    this.http.get<any>(`${API_URL}/api/parties/${r.id}`).subscribe(d => {
      this.dlg = { show: true, isNew: false, id: d.id, code: d.code,
        partyType: d.partyType === 'Customer' ? 1 : 2, name: d.name, headId: d.accountHeadId,
        phone: d.phone || '', taxNo: d.taxNo || '', address: d.address || '',
        creditLimit: d.creditLimit, isActive: d.isActive };
    });
  }

  save() {
    if (!this.dlg.name.trim()) { this.warn('Name is required.'); return; }
    this.saving.set(true);
    const body = {
      companyId: this.f.companyId, partyType: this.dlg.partyType, name: this.dlg.name.trim(),
      accountHeadId: this.dlg.headId, phone: this.dlg.phone || null,
      address: this.dlg.address || null, taxNo: this.dlg.taxNo || null,
      creditLimit: this.dlg.creditLimit || 0, isActive: this.dlg.isActive,
    };
    const req = this.dlg.isNew
      ? this.http.post(`${API_URL}/api/parties`, body)
      : this.http.put(`${API_URL}/api/parties/${this.dlg.id}`, body);
    req.subscribe({
      next: () => { this.saving.set(false); this.dlg.show = false; this.load();
        this.toast.add({ severity: 'success', summary: 'Party saved.' }); },
      error: e => { this.saving.set(false); this.err(e); },
    });
  }

  private warn(m: string) { this.toast.add({ severity: 'warn', summary: m }); }
  private err(e: any) {
    this.toast.add({ severity: 'error', summary: e.error?.message || 'Request failed.' });
  }
}
