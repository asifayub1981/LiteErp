import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { API_URL } from '../../core/api';

interface GlLine { date: string; voucherNo: string; voucherType: string; narration: string; debit: number; credit: number; runningBalance: number; }
interface GlReport { accountHeadId: number; code: string; name: string; openingBalance: number; lines: GlLine[]; closingBalance: number; }
interface Item { id: number; name: string; }

@Component({
  selector: 'app-gl-report',
  standalone: true,
  imports: [FormsModule, DecimalPipe, SelectModule, DatePickerModule, CheckboxModule, ButtonModule, TableModule, ToolbarModule, CardModule],
  template: `
    <p-toolbar>
      <ng-template pTemplate="start"><span class="font-bold text-lg">General Ledger</span></ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <div class="flex flex-wrap gap-3 align-items-end">
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Company</label>
          <p-select [options]="companies" [(ngModel)]="f.companyId" optionLabel="name" optionValue="id"
            placeholder="Company" styleClass="w-14rem" (onChange)="onCompany()" />
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
          <label class="text-sm text-500">Account head</label>
          <p-select [options]="heads" [(ngModel)]="f.headId" optionLabel="name" optionValue="id"
            placeholder="Head" [filter]="true" styleClass="w-16rem" />
        </div>
        <div class="flex align-items-center gap-2 pb-2">
          <p-checkbox [(ngModel)]="f.includeSubHeads" [binary]="true" inputId="sub" />
          <label for="sub" class="text-sm">Include sub-heads</label>
        </div>
        <div class="flex flex-column gap-1">
          <label class="text-sm text-500">Project (optional)</label>
          <p-select [options]="projects" [(ngModel)]="f.projectId" optionLabel="name" optionValue="id"
            placeholder="All projects" [showClear]="true" styleClass="w-14rem" />
        </div>
        <button pButton label="Run" icon="pi pi-play" (click)="run()" [loading]="busy()"
          [disabled]="!f.companyId || !f.headId"></button>
      </div>
    </p-card>

    @if (report()) {
      <p-card styleClass="mt-3" [header]="report()!.code + ' - ' + report()!.name">
        <p-table [value]="report()!.lines" styleClass="p-datatable-sm" [scrollable]="true" scrollHeight="60vh">
          <ng-template pTemplate="header">
            <tr>
              <th>Date</th><th>Voucher No</th><th>Type</th><th>Narration</th>
              <th class="text-right">Debit</th><th class="text-right">Credit</th><th class="text-right">Balance</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-l>
            <tr>
              <td>{{ l.date }}</td><td>{{ l.voucherNo }}</td><td>{{ l.voucherType }}</td><td>{{ l.narration }}</td>
              <td class="text-right">{{ l.debit | number:'1.2-2' }}</td>
              <td class="text-right">{{ l.credit | number:'1.2-2' }}</td>
              <td class="text-right font-semibold">{{ l.runningBalance | number:'1.2-2' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="footer">
            <tr>
              <td colspan="4" class="text-right font-bold">Opening: {{ report()!.openingBalance | number:'1.2-2' }}</td>
              <td colspan="3" class="text-right font-bold">Closing: {{ report()!.closingBalance | number:'1.2-2' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="7" class="text-center text-500">No transactions in this period.</td></tr>
          </ng-template>
        </p-table>
      </p-card>
    }
  `
})
export class GlReportComponent implements OnInit {
  companies: Item[] = [];
  projects: Item[] = [];
  heads: Item[] = [];
  f = { companyId: null as number | null, from: new Date(2026, 6, 1), to: new Date(), headId: null as number | null, includeSubHeads: true, projectId: null as number | null };
  report = signal<GlReport | null>(null);
  busy = signal(false);

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(cs => {
      this.companies = cs;
      if (cs.length === 1) { this.f.companyId = cs[0].id; this.onCompany(); }
    });
    this.http.get<Item[]>(`${API_URL}/api/lookups/heads`).subscribe(h => this.heads = h);
  }

  onCompany() {
    this.projects = [];
    if (this.f.companyId)
      this.http.get<Item[]>(`${API_URL}/api/lookups/projects`, { params: { companyId: this.f.companyId } })
        .subscribe(p => this.projects = p);
  }

  private iso(d: Date) { return d.toISOString().slice(0, 10); }

  run() {
    this.busy.set(true);
    const params: any = {
      companyId: this.f.companyId!, from: this.iso(this.f.from), to: this.iso(this.f.to),
      accountHeadId: this.f.headId!, includeSubHeads: this.f.includeSubHeads
    };
    if (this.f.projectId) params.projectId = this.f.projectId;
    this.http.get<GlReport>(`${API_URL}/api/reports/general-ledger`, { params }).subscribe({
      next: r => { this.report.set(r); this.busy.set(false); },
      error: () => this.busy.set(false)
    });
  }
}
