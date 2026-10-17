import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { API_URL } from '../../core/api';

interface TbRow {
  accountHeadId: number; code: string; name: string;
  closingDr: number; closingCr: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterModule, CardModule, TableModule, TagModule, ButtonModule],
  template: `
    <div class="lite-page">
      <h1 class="lite-page-title">Dashboard</h1>

      @if (busy()) {
        <p><i class="pi pi-spin pi-spinner"></i> Loading…</p>
      } @else {
        <div class="grid">
          <div class="col-12 md:col-3">
            <div class="stat-card stat-green">
              <div class="stat-label">Receivables</div>
              <div class="stat-value">{{ fmt(receivables()) }}</div>
              <div class="stat-sub">Trade Debtors</div>
            </div>
          </div>
          <div class="col-12 md:col-3">
            <div class="stat-card stat-orange">
              <div class="stat-label">Payables</div>
              <div class="stat-value">{{ fmt(payables()) }}</div>
              <div class="stat-sub">Trade Creditors</div>
            </div>
          </div>
          <div class="col-12 md:col-3">
            <div class="stat-card stat-blue">
              <div class="stat-label">Cash &amp; Bank</div>
              <div class="stat-value">{{ fmt(cashBank()) }}</div>
              <div class="stat-sub">Liquid balance</div>
            </div>
          </div>
          <div class="col-12 md:col-3">
            <div class="stat-card stat-purple">
              <div class="stat-label">Vouchers (FY)</div>
              <div class="stat-value">{{ voucherCount() }}</div>
              <div class="stat-sub">{{ postedCount() }} posted</div>
            </div>
          </div>
        </div>

        <div class="grid mt-2">
          <div class="col-12">
            <p-card header="Recent Vouchers">
              <p-table [value]="recent()" [rows]="8" styleClass="p-datatable-sm">
                <ng-template #header>
                  <tr>
                    <th>Voucher No</th><th>Date</th><th>Type</th>
                    <th style="text-align:right">Debit</th>
                    <th style="text-align:right">Credit</th>
                    <th>Status</th><th></th>
                  </tr>
                </ng-template>
                <ng-template #body let-v>
                  <tr>
                    <td>{{ v.voucherNo }}</td>
                    <td>{{ v.voucherDate }}</td>
                    <td>{{ v.voucherType }}</td>
                    <td style="text-align:right">{{ fmt(v.totalDebit) }}</td>
                    <td style="text-align:right">{{ fmt(v.totalCredit) }}</td>
                    <td><p-tag [value]="v.status" [severity]="v.status === 'Posted' ? 'success' : 'warn'" /></td>
                    <td><p-button  icon="pi pi-eye" [text]="true" size="small"
                      [routerLink]="['/vouchers', v.id]"></p-button></td>
                  </tr>
                </ng-template>
                <ng-template #emptymessage>
                  <tr><td colspan="7" class="text-center">No vouchers yet.</td></tr>
                </ng-template>
              </p-table>
              <div class="mt-3 flex gap-2">
                <p-button  label="New Voucher" icon="pi pi-plus" size="small"
                  routerLink="/vouchers/new"></p-button>
                <p-button  label="New Sales Invoice" icon="pi pi-file-arrow-up"
                  severity="secondary" size="small" routerLink="/invoices/sales/new"></p-button>
              </div>
            </p-card>
          </div>
        </div>
      }
    </div>
  `,
})
export class DashboardComponent implements OnInit {
  busy = signal(true);
  receivables = signal(0);
  payables = signal(0);
  cashBank = signal(0);
  voucherCount = signal(0);
  postedCount = signal(0);
  recent = signal<any[]>([]);

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.http.get<any[]>(`${API_URL}/api/lookups/companies`).subscribe(cs => {
      if (!cs.length) { this.busy.set(false); return; }
      const companyId = cs[0].id;
      const from = '2026-07-01';
      const to = new Date().toISOString().slice(0, 10);

      this.http.get<TbRow[]>(`${API_URL}/api/reports/trial-balance`,
        { params: { companyId, from, to, columns: 2 } }).subscribe(rows => {
        const bal = (code: string) => {
          const r = rows.find(x => x.code === code);
          return r ? r.closingDr - r.closingCr : 0;
        };
        this.receivables.set(bal('1-02'));
        this.payables.set(-bal('2-01'));
        this.cashBank.set(bal('1-01-001') + bal('1-01-002'));
      });

      this.http.get<any[]>(`${API_URL}/api/vouchers`,
        { params: { companyId, from, to } }).subscribe(vs => {
        const sorted = [...vs].sort((a, b) =>
          b.voucherDate.localeCompare(a.voucherDate) || b.id - a.id);
        this.voucherCount.set(vs.length);
        this.postedCount.set(vs.filter(v => v.status === 'Posted').length);
        this.recent.set(sorted.slice(0, 8));
        this.busy.set(false);
      });
    });
  }

  fmt(n: number) {
    return n.toLocaleString('en-PK', { maximumFractionDigits: 0 });
  }
}
