import { Component, OnInit, signal, computed } from '@angular/core';
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
import { VOUCHER_TYPES } from './voucher-list.component';

interface Item { id: number; name: string; extra?: string | null; }
interface Line {
  accountHeadId: number | null; costCenterId: number | null;
  debit: number | null; credit: number | null; narration: string;
}

/**
 * Voucher entry — one screen for all types (JV/BPV/CPV/PJV/SJV).
 * New: Save Draft (number assigned at save) or Save & Post.
 * Draft: editable. Posted: read-only, corrections via Reversal.
 */
@Component({
  selector: 'app-voucher-entry',
  standalone: true,
  imports: [RouterModule, FormsModule, DecimalPipe, SelectModule, DatePickerModule,
    ButtonModule, TableModule, ToolbarModule, CardModule, InputTextModule,
    InputNumberModule, TagModule, TooltipModule],
  template: `
    <p-toolbar>
      <ng-template pTemplate="start">
        <p-button  icon="pi pi-arrow-left" severity="secondary" class="mr-2"
          routerLink="/vouchers" pTooltip="Back to list"></p-button>
        <span class="font-bold text-lg">
          {{ isNew() ? 'New Voucher' : h.voucherNo }}
        </span>
        @if (!isNew()) {
          <p-tag [value]="h.status" [severity]="h.status === 'Posted' ? 'success' : 'warn'" styleClass="ml-2" />
        }
      </ng-template>
      <ng-template pTemplate="end">
        @if (readOnly()) {
          @if (h.canReverse) {
            <p-button  label="Reverse" icon="pi pi-replay" severity="help" (click)="reverse()"></p-button>
          }
        } @else {
          <p-button  label="Save Draft" icon="pi pi-save" severity="secondary"
            (click)="save(false)" [loading]="busy()" class="mr-2"></p-button>
          <p-button  [label]="isNew() ? 'Save & Post' : 'Post'" icon="pi pi-check"
            (click)="save(true)" [loading]="busy()"></p-button>
        }
      </ng-template>
    </p-toolbar>

    @if (!isNew() && h.reversalOfVoucherNo) {
      <p-card styleClass="mt-3">
        <span class="text-500 text-sm">This voucher reverses
          <b>{{ h.reversalOfVoucherNo }}</b> (amounts swapped).</span>
      </p-card>
    }

    <p-card styleClass="mt-3" header="Header">
      <div class="flex flex-wrap gap-3">
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Type</label>
          <p-select [options]="types" [(ngModel)]="h.type" optionLabel="label" optionValue="value"
            styleClass="w-14rem" [disabled]="readOnly() || !isNew()" (onChange)="onTypeChange()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Date</label>
          <p-datepicker [(ngModel)]="h.date" dateFormat="yy-mm-dd" [showIcon]="true" [disabled]="readOnly()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Company</label>
          <p-select [options]="companies" [(ngModel)]="h.companyId" optionLabel="name" optionValue="id"
            styleClass="w-14rem" [disabled]="readOnly() || !isNew()" (onChange)="onCompany()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Project (optional)</label>
          <p-select [options]="projects" [(ngModel)]="h.projectId" optionLabel="name" optionValue="id"
            placeholder="Company-level" [showClear]="true" styleClass="w-14rem" [disabled]="readOnly()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Currency</label>
          <p-select [options]="currencies" [(ngModel)]="h.currency" optionLabel="name" optionValue="extra"
            styleClass="w-10rem" [disabled]="readOnly()" />
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Ex. Rate</label>
          <p-inputnumber [(ngModel)]="h.rate" [minFractionDigits]="4" [maxFractionDigits]="6"
            styleClass="w-8rem" [disabled]="readOnly()" />
        </div>
        <div class="flex flex-column gap-1 flex-grow-1">
          <label class="text-sm text-500">Narration</label>
          <input pInputText [(ngModel)]="h.narration" [disabled]="readOnly()" class="w-full" />
        </div>
      </div>
    </p-card>

    <p-card styleClass="mt-3" header="Lines">
      <p-table [value]="lines" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th style="width:2.5rem">#</th>
            <th>Account head</th>
            <th>Cost center</th>
            <th class="text-right" style="width:10rem">Debit</th>
            <th class="text-right" style="width:10rem">Credit</th>
            <th>Narration</th>
            @if (!readOnly()) { <th style="width:3.5rem"></th> }
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-l let-i="rowIndex">
          <tr>
            <td>{{ i + 1 }}</td>
            <td>
              <p-select [options]="heads" [(ngModel)]="l.accountHeadId" optionLabel="name" optionValue="id"
                placeholder="Select head" [filter]="true" styleClass="w-full" [disabled]="readOnly()" />
            </td>
            <td>
              <p-select [options]="costCenters" [(ngModel)]="l.costCenterId" optionLabel="name" optionValue="id"
                placeholder="—" [showClear]="true" styleClass="w-full" [disabled]="readOnly()" />
            </td>
            <td>
              <p-inputnumber [(ngModel)]="l.debit" mode="decimal" [minFractionDigits]="2"
                styleClass="w-full" inputStyleClass="text-right w-full"
                [disabled]="readOnly()" (onInput)="l.credit = null" />
            </td>
            <td>
              <p-inputnumber [(ngModel)]="l.credit" mode="decimal" [minFractionDigits]="2"
                styleClass="w-full" inputStyleClass="text-right w-full"
                [disabled]="readOnly()" (onInput)="l.debit = null" />
            </td>
            <td><input pInputText [(ngModel)]="l.narration" [disabled]="readOnly()" class="w-full" /></td>
            @if (!readOnly()) {
              <td>
                <p-button  icon="pi pi-trash" severity="danger" size="small"
                  (click)="delLine(i)" [disabled]="lines.length <= 1"></p-button>
              </td>
            }
          </tr>
        </ng-template>
        <ng-template pTemplate="footer">
          <tr>
            <td colspan="3" class="text-right font-bold">Total</td>
            <td class="text-right font-bold">{{ totalDr() | number:'1.2-2' }}</td>
            <td class="text-right font-bold">{{ totalCr() | number:'1.2-2' }}</td>
            <td colspan="2" class="font-bold" [class.text-red-500]="diff() !== 0"
              [class.text-green-600]="diff() === 0">
              Diff: {{ diff() | number:'1.2-2' }}
            </td>
          </tr>
        </ng-template>
      </p-table>
      @if (!readOnly()) {
        <p-button label="Add Row" icon="pi pi-plus" severity="secondary"
          size="small" class="mt-2" (click)="addLine()"></p-button>
      }
    </p-card>

    @if (isAllocType()) {
      <p-card styleClass="mt-3" header="Invoice Allocation (optional)">
        <div class="flex flex-wrap gap-3 align-items-end mb-3">
          <div class="flex flex-column gap-1">
            <label class="text-sm text-500">{{ h.type === 6 || h.type === 7 ? 'Customer' : 'Supplier' }}</label>
            <p-select [options]="allocParties" [(ngModel)]="allocPartyId" optionLabel="name"
              optionValue="id" placeholder="Select party" [filter]="true" filterBy="name"
              [disabled]="readOnly()" styleClass="w-16rem" (onChange)="loadOpenInvoices()" />
          </div>
          <span class="text-sm text-500">Total allocated: <b>{{ allocTotal() | number:'1.2-2' }}</b></span>
        </div>
        @if (openInvoices().length > 0) {
          <p-table [value]="openInvoices()" styleClass="p-datatable-sm">
            <ng-template pTemplate="header">
              <tr><th>Invoice</th><th>Date</th><th class="text-right">Balance</th>
                <th class="text-right" style="width:12rem">Allocate</th></tr>
            </ng-template>
            <ng-template pTemplate="body" let-inv>
              <tr>
                <td class="font-bold">{{ inv.invoiceNo }}</td>
                <td>{{ inv.invoiceDate }}</td>
                <td class="text-right">{{ inv.balance | number:'1.2-2' }}</td>
                <td>
                  <p-inputnumber [(ngModel)]="inv.alloc" mode="decimal" [minFractionDigits]="2"
                    [max]="inv.balance" styleClass="w-full" inputStyleClass="text-right w-full"
                    [disabled]="readOnly()" placeholder="0.00" />
                </td>
              </tr>
            </ng-template>
          </p-table>
        } @else if (allocPartyId) {
          <p class="text-500 text-sm">No open invoices for this party.</p>
        }
        @if (readOnly() && savedAllocs.length > 0) {
          <p-table [value]="savedAllocs" styleClass="p-datatable-sm mt-2">
            <ng-template pTemplate="header">
              <tr><th>Invoice</th><th class="text-right">Allocated</th></tr>
            </ng-template>
            <ng-template pTemplate="body" let-a>
              <tr><td>{{ a.invoiceNo }}</td><td class="text-right">{{ a.amount | number:'1.2-2' }}</td></tr>
            </ng-template>
          </p-table>
        }
      </p-card>
    }
  `
})
export class VoucherEntryComponent implements OnInit {
  types = VOUCHER_TYPES;
  companies: Item[] = [];
  projects: Item[] = [];
  heads: Item[] = [];
  costCenters: Item[] = [];
  currencies: Item[] = [];

  isNew = signal(true);
  readOnly = signal(false);
  busy = signal(false);

  h = {
    id: 0, voucherNo: '', type: 1, date: new Date(),
    companyId: null as number | null, projectId: null as number | null,
    currency: 'PKR', rate: 1, narration: '',
    status: 'Draft', canReverse: false, reversalOfVoucherNo: null as string | null,
  };
  lines: Line[] = [];

  // Allocation (BRV/CRV = receipts from customers, BPV/CPV = payments to suppliers)
  allocParties: Item[] = [];
  allocPartyId: number | null = null;
  openInvoices = signal<any[]>([]);
  savedAllocs: { invoiceNo: string; amount: number }[] = [];
  isAllocType = computed(() => [2, 3, 6, 7].includes(this.h.type));
  allocTotal = computed(() => this.openInvoices().reduce((s, i) => s + (i.alloc || 0), 0));

  totalDr = computed(() => this.lines.reduce((s, l) => s + (l.debit || 0), 0));
  totalCr = computed(() => this.lines.reduce((s, l) => s + (l.credit || 0), 0));
  diff = computed(() => this.totalDr() - this.totalCr());

  constructor(private http: HttpClient, private route: ActivatedRoute,
    private router: Router, private toast: MessageService) {}

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(c => {
      this.companies = c;
      if (c.length === 1 && this.isNew()) { this.h.companyId = c[0].id; this.onCompany(); }
    });
    this.http.get<Item[]>(`${API_URL}/api/lookups/currencies`).subscribe(c => this.currencies = c);

    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isNew.set(false);
      this.load(+id);
    } else {
      this.addLine(); this.addLine();
    }
  }

  onCompany() {
    if (!this.h.companyId) return;
    this.http.get<Item[]>(`${API_URL}/api/lookups/projects?companyId=${this.h.companyId}`)
      .subscribe(p => this.projects = p);
    this.http.get<Item[]>(`${API_URL}/api/lookups/heads?companyId=${this.h.companyId}`)
      .subscribe(x => this.heads = x);
    this.http.get<Item[]>(`${API_URL}/api/lookups/cost-centers?companyId=${this.h.companyId}`)
      .subscribe(c => this.costCenters = c);
    this.h.projectId = null;
    this.loadAllocParties();
  }

  onTypeChange() { this.loadAllocParties(); }

  private loadAllocParties() {
    this.allocPartyId = null;
    this.openInvoices.set([]);
    if (!this.isAllocType() || !this.h.companyId) return;
    // BRV/CRV (6,7) = receipts from customers (1); BPV/CPV (2,3) = payments to suppliers (2)
    const ptype = (this.h.type === 6 || this.h.type === 7) ? 1 : 2;
    this.http.get<Item[]>(`${API_URL}/api/lookups/parties?companyId=${this.h.companyId}&type=${ptype}`)
      .subscribe(p => this.allocParties = p);
  }

  loadOpenInvoices() {
    this.openInvoices.set([]);
    if (!this.allocPartyId) return;
    this.http.get<any[]>(`${API_URL}/api/parties/${this.allocPartyId}/open-invoices`)
      .subscribe({
        next: invs => this.openInvoices.set(invs.map(i => ({ ...i, alloc: null as number | null }))),
        error: e => this.err(e),
      });
  }

  addLine() {
    this.lines.push({ accountHeadId: null, costCenterId: null, debit: null, credit: null, narration: '' });
  }

  delLine(i: number) { this.lines.splice(i, 1); }

  private load(id: number) {
    this.http.get<any>(`${API_URL}/api/vouchers/${id}`).subscribe({
      next: v => {
        const typeNum = VOUCHER_TYPES.find(t => t.label.startsWith(v.typePrefix))?.value ?? 1;
        Object.assign(this.h, {
          id: v.id, voucherNo: v.voucherNo, type: typeNum,
          date: new Date(v.voucherDate + 'T00:00:00'),
          companyId: v.companyId, projectId: v.projectId,
          currency: v.currencyCode, rate: v.exchangeRate, narration: v.narration,
          status: v.status, reversalOfVoucherNo: v.reversalOfVoucherNo,
        });
        this.readOnly.set(v.status === 'Posted');
        // canReverse comes from the list; refetch rule: posted & not a reversal itself
        this.h.canReverse = v.status === 'Posted' && !v.reversalOfVoucherId;
        this.lines = v.lines.map((l: any) => ({
          accountHeadId: l.accountHeadId, costCenterId: l.costCenterId,
          debit: l.debit || null, credit: l.credit || null, narration: l.narration,
        }));
        this.savedAllocs = (v.allocations || []).map((a: any) => ({
          invoiceNo: a.salesInvoiceNo || a.purchaseInvoiceNo || '?', amount: a.amount }));
        this.onCompanyKeepProject();
      },
      error: e => this.err(e)
    });
  }

  private onCompanyKeepProject() {
    if (!this.h.companyId) return;
    const pid = this.h.projectId;
    this.http.get<Item[]>(`${API_URL}/api/lookups/projects?companyId=${this.h.companyId}`)
      .subscribe(p => { this.projects = p; this.h.projectId = pid; });
    this.http.get<Item[]>(`${API_URL}/api/lookups/heads?companyId=${this.h.companyId}`)
      .subscribe(x => this.heads = x);
    this.http.get<Item[]>(`${API_URL}/api/lookups/cost-centers?companyId=${this.h.companyId}`)
      .subscribe(c => this.costCenters = c);
    this.loadAllocParties();
  }

  private iso(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private payload() {
    const isSalesAlloc = this.h.type === 6 || this.h.type === 7;
    return {
      companyId: this.h.companyId,
      projectId: this.h.projectId,
      voucherType: this.h.type,
      voucherDate: this.iso(this.h.date),
      currencyCode: this.h.currency,
      exchangeRate: this.h.rate || 1,
      narration: this.h.narration,
      lines: this.lines.map(l => ({
        accountHeadId: l.accountHeadId,
        costCenterId: l.costCenterId,
        debit: l.debit || 0,
        credit: l.credit || 0,
        taxRuleId: null,
        narration: l.narration,
      })),
      allocations: this.isAllocType()
        ? this.openInvoices()
            .filter(i => (i.alloc || 0) > 0)
            .map(i => ({
              salesInvoiceId: isSalesAlloc ? i.id : null,
              purchaseInvoiceId: isSalesAlloc ? null : i.id,
              amount: i.alloc,
            }))
        : [],
    };
  }

  private valid(): boolean {
    if (!this.h.companyId) { this.toast.add({ severity: 'warn', summary: 'Company select karo' }); return false; }
    if (!this.h.date) { this.toast.add({ severity: 'warn', summary: 'Date select karo' }); return false; }
    for (const l of this.lines) {
      if (!l.accountHeadId) { this.toast.add({ severity: 'warn', summary: 'Har line mein account head lazmi hai' }); return false; }
      const dr = l.debit || 0, cr = l.credit || 0;
      if ((dr <= 0 && cr <= 0) || (dr > 0 && cr > 0)) {
        this.toast.add({ severity: 'warn', summary: 'Har line mein ya to Debit ya Credit — aik side' });
        return false;
      }
    }
    return true;
  }

  /** andPost=false → sirf draft save. true → save ke baad foran post. */
  save(andPost: boolean) {
    if (!this.valid()) return;
    this.busy.set(true);
    const done = (id: number) => {
      if (!andPost) { this.busy.set(false); this.router.navigate(['/vouchers', id]); return; }
      this.http.post(`${API_URL}/api/vouchers/${id}/post`, {}).subscribe({
        next: () => {
          this.busy.set(false);
          this.toast.add({ severity: 'success', summary: 'Voucher posted' });
          this.router.navigate(['/vouchers', id]);
        },
        error: e => { this.busy.set(false); this.err(e); this.router.navigate(['/vouchers', id]); }
      });
    };
    if (this.isNew()) {
      this.http.post<any>(`${API_URL}/api/vouchers`, this.payload()).subscribe({
        next: v => {
          this.toast.add({ severity: 'success', summary: `Draft saved: ${v.voucherNo}` });
          done(v.id);
        },
        error: e => { this.busy.set(false); this.err(e); }
      });
    } else {
      this.http.put<any>(`${API_URL}/api/vouchers/${this.h.id}`, this.payload()).subscribe({
        next: () => {
          this.toast.add({ severity: 'success', summary: 'Draft updated' });
          done(this.h.id);
        },
        error: e => { this.busy.set(false); this.err(e); }
      });
    }
  }

  reverse() {
    if (!window.confirm(`Create a reversal of ${this.h.voucherNo}? (Dr/Cr swapped, saved as draft)`)) return;
    this.http.post<{ id: number }>(`${API_URL}/api/vouchers/${this.h.id}/reverse`, {}).subscribe({
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
