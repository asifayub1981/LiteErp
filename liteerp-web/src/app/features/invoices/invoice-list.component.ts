import { Component, Input, OnInit, signal } from '@angular/core';
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
interface InvoiceRow {
  id: number; invoiceNo: string; invoiceDate: string; partyName: string;
  grandTotal: number; paid: number; balance: number; status: string;
  postedVoucherNo: string | null;
}

@Component({
  selector: 'app-invoice-list',
  standalone: true,
  imports: [RouterModule, FormsModule, DecimalPipe, SelectModule, DatePickerModule,
    ButtonModule, TableModule, ToolbarModule, CardModule, InputTextModule, TagModule, TooltipModule],
  template: `
    <p-toolbar>
      <ng-template #start>
        <span class="font-bold text-lg">{{ kind === 'sales' ? 'Sales Invoices' : 'Purchase Invoices' }}</span>
      </ng-template>
      <ng-template #end>
        <p-button label="New Invoice" icon="pi pi-plus" [routerLink]="['/invoices/' + kind + '/new']"></p-button>
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
          <label class="text-sm text-500">From</label>
          <p-datepicker [(ngModel)]="f.from" dateFormat="yy-mm-dd" [showIcon]="true" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">To</label>
          <p-datepicker [(ngModel)]="f.to" dateFormat="yy-mm-dd" [showIcon]="true" />
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
        <p-button label="Run" icon="pi pi-play" (click)="load()"
          [loading]="busy()" [disabled]="!f.companyId"></p-button>
      </div>

      <p-table [value]="rows()" [loading]="busy()" styleClass="p-datatable-sm"
        [paginator]="true" [rows]="15">
        <ng-template #header>
          <tr><th>Invoice No</th><th>Date</th><th>{{ kind === 'sales' ? 'Customer' : 'Supplier' }}</th>
            <th class="text-right">Total</th><th class="text-right">Paid</th>
            <th class="text-right">Balance</th><th>Status</th><th>Voucher</th><th></th></tr>
        </ng-template>
        <ng-template #body let-r>
          <tr>
            <td class="font-bold">{{ r.invoiceNo }}</td>
            <td>{{ r.invoiceDate }}</td>
            <td>{{ r.partyName }}</td>
            <td class="text-right">{{ r.grandTotal | number:'1.2-2' }}</td>
            <td class="text-right">{{ r.paid | number:'1.2-2' }}</td>
            <td class="text-right font-bold" [class.text-red-500]="r.balance > 0">{{ r.balance | number:'1.2-2' }}</td>
            <td>
              <p-tag [value]="r.status" [severity]="r.status === 'Posted' ? 'success' : 'warn'"></p-tag>
              @if (r.status === 'Posted' && r.balance === 0) {
                <p-tag value="Paid" severity="info" styleClass="ml-1"></p-tag>
              } @else if (r.status === 'Posted' && r.paid > 0) {
                <p-tag value="Partial" severity="warn" styleClass="ml-1"></p-tag>
              }
            </td>
            <td class="text-sm">{{ r.postedVoucherNo || '—' }}</td>
            <td class="text-right">
              <p-button icon="pi pi-eye" [text]="true" [routerLink]="['/invoices/' + kind + '/' + r.id]"
                pTooltip="View"></p-button>
              @if (r.status === 'Draft') {
                <p-button icon="pi pi-pencil" [text]="true"
                  [routerLink]="['/invoices/' + kind + '/' + r.id]" [queryParams]="{ edit: 1 }"
                  pTooltip="Edit"></p-button>
              }
            </td>
          </tr>
        </ng-template>
        <ng-template #emptymessage>
          <tr><td colspan="9" class="text-center text-500">No invoices. Click "New Invoice".</td></tr>
        </ng-template>
      </p-table>
    </p-card>
  `,
})
export class InvoiceListComponent implements OnInit {
  @Input() kind: 'sales' | 'purchase' = 'sales';
  constructor(private http: HttpClient, private router: Router, private toast: MessageService) {}

  companies: Item[] = [];
  statuses = [{ label: 'Draft', value: 1 }, { label: 'Posted', value: 2 }];
  rows = signal<InvoiceRow[]>([]);
  busy = signal(false);
  f = { companyId: 0 as number, from: null as Date | null, to: null as Date | null,
        status: null as number | null, search: '' };

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(c => {
      this.companies = c;
      if (c.length === 1) { this.f.companyId = c[0].id; this.load(); }
    });
    const t = new Date();
    this.f.to = t;
    this.f.from = new Date(t.getFullYear(), t.getMonth() - 2, 1);
  }

  private dstr(d: Date | null): string {
    return d ? d.toISOString().slice(0, 10) : '';
  }

  load() {
    if (!this.f.companyId || !this.f.from || !this.f.to) return;
    this.busy.set(true);
    let url = `${API_URL}/api/invoices/${this.kind}?companyId=${this.f.companyId}` +
      `&from=${this.dstr(this.f.from)}&to=${this.dstr(this.f.to)}`;
    if (this.f.status) url += `&status=${this.f.status}`;
    if (this.f.search) url += `&search=${encodeURIComponent(this.f.search)}`;
    this.http.get<InvoiceRow[]>(url).subscribe({
      next: r => { this.rows.set(r); this.busy.set(false); },
      error: e => { this.busy.set(false);
        this.toast.add({ severity: 'error', summary: e.error?.message || 'Load failed.' }); },
    });
  }
}
