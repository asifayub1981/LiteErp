import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { API_URL } from '../../core/api';

interface Item { id: number; name: string; }
interface VoucherRow {
  id: number; voucherNo: string; voucherDate: string; voucherType: string;
  typePrefix: string; projectName: string | null; narration: string;
  totalDr: number; totalCr: number; status: string; canReverse: boolean;
}

// Numeric values must match the VoucherType enum on the API (JV=1..CRV=7)
export const VOUCHER_TYPES = [
  { label: 'JV — Regular Journal', value: 1 },
  { label: 'BPV — Bank Payment', value: 2 },
  { label: 'CPV — Cash Payment', value: 3 },
  { label: 'PJV — Purchase Journal', value: 4 },
  { label: 'SJV — Sales Journal', value: 5 },
  { label: 'BRV — Bank Receipt', value: 6 },
  { label: 'CRV — Cash Receipt', value: 7 },
];

@Component({
  selector: 'app-voucher-list',
  standalone: true,
  imports: [RouterModule, FormsModule, DecimalPipe, SelectModule, DatePickerModule,
    ButtonModule, TableModule, ToolbarModule, CardModule, InputTextModule, TagModule, TooltipModule],
  template: `
    <p-toolbar>
      <ng-template pTemplate="start"><span class="font-bold text-lg">Vouchers</span></ng-template>
      <ng-template pTemplate="end">
        <p-button  label="New Voucher" icon="pi pi-plus" routerLink="/vouchers/new"></p-button>
      </ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <div class="flex flex-wrap gap-3 align-items-end">
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Company</label>
          <p-select [options]="companies" [(ngModel)]="f.companyId" optionLabel="name" optionValue="id"
            placeholder="Company" styleClass="w-14rem" (onChange)="load()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">From</label>
          <p-datepicker [(ngModel)]="f.from" dateFormat="yy-mm-dd" [showIcon]="true" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">To</label>
          <p-datepicker [(ngModel)]="f.to" dateFormat="yy-mm-dd" [showIcon]="true" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Type</label>
          <p-select [options]="types" [(ngModel)]="f.type" optionLabel="label" optionValue="value"
            placeholder="All types" [showClear]="true" styleClass="w-14rem" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Status</label>
          <p-select [options]="statuses" [(ngModel)]="f.status" optionLabel="label" optionValue="value"
            placeholder="All" [showClear]="true" styleClass="w-10rem" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Search</label>
          <input pInputText [(ngModel)]="f.search" placeholder="No / narration"
            (keyup.enter)="load()" class="w-14rem" />
        </div>
        <p-button  label="Run" icon="pi pi-play" (click)="load()"
          [loading]="busy()" [disabled]="!f.companyId"></p-button>
      </div>
    </p-card>

    <p-card styleClass="mt-3">
      <p-table [value]="rows()" [loading]="busy()" styleClass="p-datatable-sm"
        [scrollable]="true" scrollHeight="60vh">
        <ng-template pTemplate="header">
          <tr>
            <th>Voucher No</th><th>Date</th><th>Type</th><th>Project</th><th>Narration</th>
            <th class="text-right">Debit</th><th class="text-right">Credit</th>
            <th>Status</th><th style="width:11rem">Actions</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-r>
          <tr>
            <td class="font-semibold">{{ r.voucherNo }}</td>
            <td>{{ r.voucherDate }}</td>
            <td><p-tag [value]="r.typePrefix" severity="info" /></td>
            <td>{{ r.projectName ?? '—' }}</td>
            <td>{{ r.narration }}</td>
            <td class="text-right">{{ r.totalDr | number:'1.2-2' }}</td>
            <td class="text-right">{{ r.totalCr | number:'1.2-2' }}</td>
            <td>
              <p-tag [value]="r.status" [severity]="r.status === 'Posted' ? 'success' : 'warn'" />
            </td>
            <td>
              <div class="flex gap-1">
                <p-button  icon="pi pi-eye" size="small" severity="secondary"
                  pTooltip="View" [routerLink]="['/vouchers', r.id]"></p-button>
                @if (r.status === 'Draft') {
                  <p-button  icon="pi pi-pencil" size="small"
                    pTooltip="Edit" [routerLink]="['/vouchers', r.id]"></p-button>
                }
                @if (r.canReverse) {
                  <p-button  icon="pi pi-replay" size="small" severity="help"
                    pTooltip="Reverse" (click)="reverse(r)"></p-button>
                }
              </div>
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="9" class="text-center text-500">No vouchers — adjust filters or create one.</td></tr>
        </ng-template>
      </p-table>
    </p-card>
  `
})
export class VoucherListComponent implements OnInit {
  companies: Item[] = [];
  types = VOUCHER_TYPES;
  statuses = [{ label: 'Draft', value: 'Draft' }, { label: 'Posted', value: 'Posted' }];
  rows = signal<VoucherRow[]>([]);
  busy = signal(false);

  f = {
    companyId: null as number | null,
    from: this.firstOfFy(), to: new Date(),
    type: null as number | null, status: null as string | null, search: ''
  };

  constructor(private http: HttpClient, private router: Router, private toast: MessageService) {}

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(c => {
      this.companies = c;
      if (c.length === 1) { this.f.companyId = c[0].id; this.load(); }
    });
  }

  private firstOfFy(): Date {
    const now = new Date();
    // FY is Jul–Jun in the demo data; default the filter to the current FY start.
    return new Date(now.getMonth() >= 6 ? now.getFullYear() : now.getFullYear() - 1, 6, 1);
  }

  private iso(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  load() {
    if (!this.f.companyId) return;
    this.busy.set(true);
    let url = `${API_URL}/api/vouchers?companyId=${this.f.companyId}&from=${this.iso(this.f.from)}&to=${this.iso(this.f.to)}`;
    if (this.f.type) url += `&type=${this.f.type}`;
    if (this.f.status) url += `&status=${this.f.status}`;
    if (this.f.search.trim()) url += `&search=${encodeURIComponent(this.f.search.trim())}`;
    this.http.get<VoucherRow[]>(url).subscribe({
      next: r => { this.rows.set(r); this.busy.set(false); },
      error: e => { this.busy.set(false); this.err(e); }
    });
  }

  reverse(r: VoucherRow) {
    if (!window.confirm(`Create a reversal of ${r.voucherNo}? (Dr/Cr swapped, saved as draft)`)) return;
    this.http.post<{ id: number }>(`${API_URL}/api/vouchers/${r.id}/reverse`, {}).subscribe({
      next: res => {
        this.toast.add({ severity: 'success', summary: 'Reversal draft created' });
        this.router.navigate(['/vouchers', res.id]);
      },
      error: e => this.err(e)
    });
  }

  private err(e: any) {
    this.toast.add({ severity: 'error', summary: 'Error',
      detail: e?.error?.message ?? 'Something went wrong.' });
  }
}
