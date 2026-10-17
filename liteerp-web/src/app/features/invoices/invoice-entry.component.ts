import { Component, Input, OnInit, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { API_URL } from '../../core/api';

interface Item { id: number; name: string; extra?: string | null; }
interface Line {
  accountHeadId: number | null; description: string;
  qty: number | null; rate: number | null; taxRuleId: number | null;
}

@Component({
  selector: 'app-invoice-entry',
  standalone: true,
  imports: [RouterModule, FormsModule, DecimalPipe, SelectModule, DatePickerModule,
    ButtonModule, TableModule, ToolbarModule, CardModule, InputTextModule,
    InputNumberModule, TagModule, TooltipModule],
  template: `
    <p-toolbar>
      <ng-template pTemplate="start">
        <p-button icon="pi pi-arrow-left" severity="secondary" class="mr-2"
          routerLink="/invoices/{{ kind }}" pTooltip="Back to list"></p-button>
        <span class="font-bold text-lg">
          {{ isNew() ? 'New' : (readOnly() ? 'View' : 'Edit') }}
          {{ kind === 'sales' ? 'Sales' : 'Purchase' }} Invoice
          @if (h.invoiceNo) { <span class="text-500">· {{ h.invoiceNo }}</span> }
        </span>
      </ng-template>
      <ng-template pTemplate="end">
        @if (readOnly()) {
          <p-tag value="Posted" severity="success" styleClass="mr-2"></p-tag>
          @if (h.postedVoucherNo) {
            <span class="text-sm text-500 mr-2">Voucher: {{ h.postedVoucherNo }}</span>
          }
        } @else {
          <p-button label="Save Draft" icon="pi pi-save" severity="secondary"
            (click)="save(false)" [loading]="busy()" class="mr-2"></p-button>
          <p-button [label]="isNew() ? 'Save & Post' : 'Post'" icon="pi pi-check"
            (click)="save(true)" [loading]="busy()"></p-button>
        }
      </ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <div class="flex flex-wrap gap-3 align-items-end mb-3">
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Date *</label>
          <p-datepicker [(ngModel)]="h.date" dateFormat="yy-mm-dd" [showIcon]="true"
            [disabled]="readOnly()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">{{ kind === 'sales' ? 'Customer' : 'Supplier' }} *</label>
          <p-select [options]="parties" [(ngModel)]="h.partyId" optionLabel="name" optionValue="id"
            placeholder="Select party" [filter]="true" filterBy="name"
            [disabled]="readOnly() || !isNew()" styleClass="w-16rem" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Company *</label>
          <p-select [options]="companies" [(ngModel)]="h.companyId" optionLabel="name" optionValue="id"
            placeholder="Company" [disabled]="readOnly() || !isNew()" styleClass="w-14rem"
            (onChange)="onCompany()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Project</label>
          <p-select [options]="projects" [(ngModel)]="h.projectId" optionLabel="name" optionValue="id"
            placeholder="(none)" [showClear]="true" [disabled]="readOnly()" styleClass="w-14rem" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Currency</label>
          <p-select [options]="currencies" [(ngModel)]="h.currency" optionLabel="name" optionValue="extra"
            [disabled]="readOnly()" styleClass="w-10rem" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Ex. Rate</label>
          <p-inputnumber [(ngModel)]="h.rate" [minFractionDigits]="4" [maxFractionDigits]="6"
            styleClass="w-8rem" [disabled]="readOnly()" />
        </div>
      </div>
      <div class="flex flex-column gap-1 mb-1">
        <label class="text-sm text-500">Narration</label>
        <input pInputText [(ngModel)]="h.narration" class="w-full" [disabled]="readOnly()" />
      </div>
    </p-card>

    <p-card styleClass="mt-3" header="Lines">
      <p-table [value]="lines()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr><th style="width:22%">{{ kind === 'sales' ? 'Revenue' : 'Expense' }} Head *</th>
            <th style="width:22%">Description</th><th>Qty</th><th>Rate</th>
            <th>Tax</th><th class="text-right">Amount</th><th class="text-right">Tax Amt</th>
            @if (!readOnly()) { <th style="width:3rem"></th> } </tr>
        </ng-template>
        <ng-template pTemplate="body" let-l let-i="rowIndex">
          <tr>
            <td><p-select [options]="heads" [(ngModel)]="l.accountHeadId" optionLabel="name"
              optionValue="id" placeholder="Head" [filter]="true" filterBy="name"
              [disabled]="readOnly()" styleClass="w-full" appendTo="body" /></td>
            <td><input pInputText [(ngModel)]="l.description" class="w-full" [disabled]="readOnly()" /></td>
            <td><p-inputnumber [(ngModel)]="l.qty" mode="decimal" [minFractionDigits]="2"
              styleClass="w-6rem" [disabled]="readOnly()" /></td>
            <td><p-inputnumber [(ngModel)]="l.rate" mode="decimal" [minFractionDigits]="2"
              styleClass="w-7rem" [disabled]="readOnly()" /></td>
            <td><p-select [options]="taxRules" [(ngModel)]="l.taxRuleId" optionLabel="name"
              optionValue="id" placeholder="(none)" [showClear]="true"
              [disabled]="readOnly()" styleClass="w-9rem" appendTo="body" /></td>
            <td class="text-right">{{ lineAmount(l) | number:'1.2-2' }}</td>
            <td class="text-right">{{ lineTax(l) | number:'1.2-2' }}</td>
            @if (!readOnly()) {
              <td><p-button icon="pi pi-trash" [text]="true" severity="danger"
                (click)="delLine(i)"></p-button></td>
            }
          </tr>
        </ng-template>
      </p-table>
      @if (!readOnly()) {
        <p-button label="Add Line" icon="pi pi-plus" [text]="true" (click)="addLine()"
          styleClass="mt-2"></p-button>
      }
      <div class="flex justify-content-end mt-3">
        <table class="text-sm">
          <tr><td class="pr-4 text-500">Subtotal</td>
            <td class="text-right font-bold">{{ subTotal() | number:'1.2-2' }}</td></tr>
          <tr><td class="pr-4 text-500">Tax</td>
            <td class="text-right font-bold">{{ taxTotal() | number:'1.2-2' }}</td></tr>
          <tr class="text-lg"><td class="pr-4 font-bold">Grand Total</td>
            <td class="text-right font-bold">{{ grandTotal() | number:'1.2-2' }}</td></tr>
        </table>
      </div>
    </p-card>
  `,
})
export class InvoiceEntryComponent implements OnInit {
  @Input() kind: 'sales' | 'purchase' = 'sales';
  constructor(private http: HttpClient, private route: ActivatedRoute,
    private router: Router, private toast: MessageService) {}

  companies: Item[] = [];
  projects: Item[] = [];
  currencies: Item[] = [];
  parties: Item[] = [];
  heads: Item[] = [];
  taxRules: Item[] = [];

  h = { invoiceNo: '', companyId: 0 as number, projectId: null as number | null,
    date: new Date() as Date | null, partyId: null as number | null,
    currency: 'PKR', rate: 1, narration: '', postedVoucherNo: '' as string | null };
  lines = signal<Line[]>([]);
  busy = signal(false);
  isNew = signal(true);
  readOnly = signal(false);
  private id = 0;

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(c => {
      this.companies = c;
      if (c.length === 1 && this.isNew()) { this.h.companyId = c[0].id; this.onCompany(); }
    });
    this.http.get<Item[]>(`${API_URL}/api/lookups/currencies`).subscribe(c => {
      this.currencies = c;
      if (!this.h.currency && c.length) this.h.currency = c[0].extra || 'PKR';
    });
    this.http.get<Item[]>(`${API_URL}/api/lookups/tax-rules`).subscribe(t => this.taxRules = t);

    this.route.paramMap.subscribe(p => {
      const idp = p.get('id');
      if (idp === 'new') { this.isNew.set(true); this.readOnly.set(false); this.addLine(); }
      else {
        this.id = +idp!; this.isNew.set(false);
        this.route.queryParamMap.subscribe(q => {
          this.readOnly.set(!q.get('edit'));
          this.load();
        });
      }
    });
  }

  onCompany() {
    if (!this.h.companyId) return;
    this.http.get<Item[]>(`${API_URL}/api/lookups/projects?companyId=${this.h.companyId}`)
      .subscribe(p => this.projects = p);
    const ptype = this.kind === 'sales' ? 1 : 2;
    this.http.get<Item[]>(`${API_URL}/api/lookups/parties?companyId=${this.h.companyId}&type=${ptype}`)
      .subscribe(p => this.parties = p);
    this.http.get<Item[]>(`${API_URL}/api/lookups/heads?companyId=${this.h.companyId}`)
      .subscribe(all => {
        // extra carries AccountType: sales → Revenue(4); purchase → Expense(5)/Asset(1)
        this.heads = all.filter(x => this.kind === 'sales'
          ? x.extra === '4' : (x.extra === '5' || x.extra === '1'));
      });
  }

  load() {
    this.http.get<any>(`${API_URL}/api/invoices/${this.kind}/${this.id}`).subscribe({
      next: d => {
        this.h = { invoiceNo: d.invoiceNo, companyId: d.companyId,
          projectId: d.projectId, date: new Date(d.invoiceDate + 'T00:00:00'),
          partyId: d.partyId, currency: d.currencyCode, rate: d.exchangeRate,
          narration: d.narration, postedVoucherNo: d.postedVoucherNo };
        this.onCompany();
        this.lines.set(d.lines.map((l: any) => ({
          accountHeadId: l.accountHeadId, description: l.description,
          qty: l.qty, rate: l.rate, taxRuleId: l.taxRuleId })));
      },
      error: e => this.toast.add({ severity: 'error', summary: e.error?.message || 'Load failed.' }),
    });
  }

  addLine() { this.lines.update(v => [...v, {
    accountHeadId: null, description: '', qty: 1, rate: null, taxRuleId: null }]); }
  delLine(i: number) { this.lines.update(v => v.filter((_, x) => x !== i)); }

  lineAmount(l: Line): number { return (l.qty || 0) * (l.rate || 0); }
  lineTax(l: Line): number {
    if (!l.taxRuleId) return 0;
    const r = this.taxRules.find(t => t.id === l.taxRuleId);
    const rate = r?.extra ? parseFloat(r.extra) : 0;
    return this.lineAmount(l) * rate / 100;
  }
  subTotal = computed(() => this.lines().reduce((s, l) => s + this.lineAmount(l), 0));
  taxTotal = computed(() => this.lines().reduce((s, l) => s + this.lineTax(l), 0));
  grandTotal = computed(() => this.subTotal() + this.taxTotal());

  private dstr(d: Date | null): string { return d ? d.toISOString().slice(0, 10) : ''; }

  private payload() {
    return {
      companyId: this.h.companyId, projectId: this.h.projectId,
      invoiceDate: this.dstr(this.h.date), partyId: this.h.partyId,
      currencyCode: this.h.currency, exchangeRate: this.h.rate || 1,
      narration: this.h.narration,
      lines: this.lines().map(l => ({
        accountHeadId: l.accountHeadId, description: l.description || '',
        qty: l.qty || 0, rate: l.rate || 0, taxRuleId: l.taxRuleId })),
    };
  }

  save(post: boolean) {
    if (!this.h.companyId) { this.warn('Select a company.'); return; }
    if (!this.h.partyId) { this.warn(this.kind === 'sales' ? 'Select a customer.' : 'Select a supplier.'); return; }
    if (!this.h.date) { this.warn('Select a date.'); return; }
    if (this.lines().length === 0) { this.warn('Add at least one line.'); return; }
    for (const l of this.lines()) {
      if (!l.accountHeadId) { this.warn('Every line needs an account head.'); return; }
      if (!(l.qty || 0)) { this.warn('Qty must be greater than zero.'); return; }
    }
    this.busy.set(true);
    const done = (id: number, no: string) => {
      if (!post) {
        this.busy.set(false);
        this.toast.add({ severity: 'success', summary: `Draft saved: ${no}` });
        this.router.navigate(['/invoices/' + this.kind + '/' + id]);
      } else {
        this.http.post(`${API_URL}/api/invoices/${this.kind}/${id}/post`, {})
          .subscribe({
            next: () => { this.busy.set(false);
              this.toast.add({ severity: 'success', summary: `Posted: ${no}` });
              this.router.navigate(['/invoices/' + this.kind + '/' + id]); },
            error: e => { this.busy.set(false); this.err(e); },
          });
      }
    };
    if (this.isNew()) {
      this.http.post<any>(`${API_URL}/api/invoices/${this.kind}`, this.payload()).subscribe({
        next: v => done(v.id, v.invoiceNo),
        error: e => { this.busy.set(false); this.err(e); },
      });
    } else {
      this.http.put<any>(`${API_URL}/api/invoices/${this.kind}/${this.id}`, this.payload()).subscribe({
        next: v => done(v.id, v.invoiceNo),
        error: e => { this.busy.set(false); this.err(e); },
      });
    }
  }

  private warn(m: string) { this.toast.add({ severity: 'warn', summary: m }); }
  private err(e: any) {
    this.toast.add({ severity: 'error', summary: e.error?.message || 'Request failed.' });
  }
}
