import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { SelectButtonModule } from 'primeng/selectbutton';
import { API_URL } from '../../core/api';

interface TbRow {
  accountHeadId: number; code: string; name: string; level: number; isTransactional: boolean;
  openingDr: number; openingCr: number; periodDr: number; periodCr: number; closingDr: number; closingCr: number;
}
interface Item { id: number; name: string; }

@Component({
  selector: 'app-trial-balance',
  standalone: true,
  imports: [FormsModule, DecimalPipe, SelectModule, DatePickerModule, ButtonModule, TableModule, ToolbarModule, CardModule, SelectButtonModule],
  template: `
    <p-toolbar>
      <ng-template #start><span class="font-bold text-lg">Trial Balance</span></ng-template>
    </p-toolbar>

    <div class="report-filters">
      <div class="report-filters-title">Report Parameters</div>
      <div class="report-filters-grid">
        <div class="report-field">
          <label>Company</label>
          <p-select [options]="companies" [(ngModel)]="f.companyId" optionLabel="name" optionValue="id"
            placeholder="Select company" styleClass="w-14rem" />
        </div>
        <div class="report-field">
          <label>From</label>
          <p-datepicker [(ngModel)]="f.from" dateFormat="yy-mm-dd" [showIcon]="true" />
        </div>
        <div class="report-field">
          <label>To</label>
          <p-datepicker [(ngModel)]="f.to" dateFormat="yy-mm-dd" [showIcon]="true" />
        </div>
        <div class="report-field">
          <label>Columns</label>
          <p-selectbutton [options]="colOptions" [(ngModel)]="f.columns" optionLabel="label" optionValue="value" />
        </div>
        <div class="report-preview-btn">
          <p-button  label="Preview" icon="pi pi-eye" size="large"
            (click)="run()" [loading]="busy()" [disabled]="!f.companyId"></p-button>
        </div>
      </div>
    </div>

    @if (rows().length) {
      <p-card styleClass="mt-3">
        <p-table [value]="rows()" styleClass="p-datatable-sm" [scrollable]="true" scrollHeight="60vh">
          <ng-template #header>
            <tr>
              <th>Code</th><th>Account</th>
              @if (f.columns === 6) {
                <th class="text-right">Opening Dr</th><th class="text-right">Opening Cr</th>
                <th class="text-right">Period Dr</th><th class="text-right">Period Cr</th>
              }
              <th class="text-right">{{ f.columns === 6 ? 'Closing Dr' : 'Debit' }}</th>
              <th class="text-right">{{ f.columns === 6 ? 'Closing Cr' : 'Credit' }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-r>
            <tr [class.font-bold]="!r.isTransactional">
              <td class="font-mono text-sm">{{ r.code }}</td>
              <td [style.padding-left.rem]="0.5 + r.level * 1.5">{{ r.name }}</td>
              @if (f.columns === 6) {
                <td class="text-right">{{ r.openingDr | number:'1.2-2' }}</td>
                <td class="text-right">{{ r.openingCr | number:'1.2-2' }}</td>
                <td class="text-right">{{ r.periodDr | number:'1.2-2' }}</td>
                <td class="text-right">{{ r.periodCr | number:'1.2-2' }}</td>
              }
              <td class="text-right">{{ r.closingDr | number:'1.2-2' }}</td>
              <td class="text-right">{{ r.closingCr | number:'1.2-2' }}</td>
            </tr>
          </ng-template>
          <ng-template #footer>
            <tr>
              <td colspan="2" class="text-right font-bold">Total</td>
              @if (f.columns === 6) {
                <td class="text-right font-bold">{{ sum('openingDr') | number:'1.2-2' }}</td>
                <td class="text-right font-bold">{{ sum('openingCr') | number:'1.2-2' }}</td>
                <td class="text-right font-bold">{{ sum('periodDr') | number:'1.2-2' }}</td>
                <td class="text-right font-bold">{{ sum('periodCr') | number:'1.2-2' }}</td>
              }
              <td class="text-right font-bold">{{ sum('closingDr') | number:'1.2-2' }}</td>
              <td class="text-right font-bold">{{ sum('closingCr') | number:'1.2-2' }}</td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    }
  `
})
export class TrialBalanceComponent implements OnInit {
  companies: Item[] = [];
  f = { companyId: null as number | null, from: new Date(2026, 6, 1), to: new Date(), columns: 2 };
  colOptions = [{ label: '2-Column', value: 2 }, { label: '6-Column', value: 6 }];
  rows = signal<TbRow[]>([]);
  busy = signal(false);

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(cs => {
      this.companies = cs;
      if (cs.length === 1) { this.f.companyId = cs[0].id; this.run(); }
    });
  }

  sum(k: keyof TbRow) { return this.rows().reduce((a, r) => a + (r[k] as number), 0); }

  private iso(d: Date) { return d.toISOString().slice(0, 10); }

  run() {
    if (!this.f.companyId) return;
    this.busy.set(true);
    this.http.get<TbRow[]>(`${API_URL}/api/reports/trial-balance`, {
      params: { companyId: this.f.companyId, from: this.iso(this.f.from), to: this.iso(this.f.to), columns: this.f.columns }
    }).subscribe({
      next: r => { this.rows.set(r); this.busy.set(false); },
      error: () => this.busy.set(false)
    });
  }
}
