import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { API_URL } from '../../core/api';

interface Item { id: number; name: string; }

@Component({
  selector: 'app-party-statement',
  standalone: true,
  imports: [RouterModule, FormsModule, DecimalPipe, SelectModule, DatePickerModule,
    ButtonModule, TableModule, ToolbarModule, CardModule, TagModule],
  template: `
    <p-toolbar>
      <ng-template #start><span class="font-bold text-lg">Party Statement</span></ng-template>
    </p-toolbar>

    <div class="report-filters">
      <div class="report-filters-title">Report Parameters</div>
      <div class="report-filters-grid">
        <div class="report-field">
          <label>Company</label>
          <p-select [options]="companies" [(ngModel)]="f.companyId" optionLabel="name" optionValue="id"
            placeholder="Company" styleClass="w-14rem" (onChange)="onCompany()" />
        </div>
        <div class="report-field">
          <label>Party</label>
          <p-select [options]="parties" [(ngModel)]="f.partyId" optionLabel="name" optionValue="id"
            placeholder="Select party" [filter]="true" filterBy="name" styleClass="w-16rem" />
        </div>
        <div class="report-field">
          <label>From</label>
          <p-datepicker [(ngModel)]="f.from" dateFormat="yy-mm-dd" [showIcon]="true" />
        </div>
        <div class="report-field">
          <label>To</label>
          <p-datepicker [(ngModel)]="f.to" dateFormat="yy-mm-dd" [showIcon]="true" />
        </div>
        <div class="report-preview-btn">
          <p-button  label="Preview" icon="pi pi-eye" size="large"
            (click)="load()" [loading]="busy()" [disabled]="!f.partyId"></p-button>
        </div>
      </div>
    </div>

    @if (stmt()) {
      <p-card>
        <div class="mb-2">
          <span class="font-bold text-lg">{{ stmt()!.partyName }}</span>
          <span class="text-500 ml-2">{{ stmt()!.partyCode }}</span>
        </div>
        <p-table [value]="rows()" styleClass="p-datatable-sm">
          <ng-template #header>
            <tr><th>Date</th><th>Doc No</th><th>Description</th>
              <th class="text-right">Debit</th><th class="text-right">Credit</th>
              <th class="text-right">Balance</th></tr>
          </ng-template>
          <ng-template #body let-r>
            <tr>
              <td>{{ r.date }}</td><td class="font-bold">{{ r.docNo }}</td><td>{{ r.description }}</td>
              <td class="text-right">{{ r.debit | number:'1.2-2' }}</td>
              <td class="text-right">{{ r.credit | number:'1.2-2' }}</td>
              <td class="text-right font-bold">{{ r.balance | number:'1.2-2' }}</td>
            </tr>
          </ng-template>
          <ng-template #footer>
            <tr>
              <td colspan="3" class="text-right font-bold">Opening</td>
              <td colspan="2"></td>
              <td class="text-right font-bold">{{ stmt()!.openingBalance | number:'1.2-2' }}</td>
            </tr>
            <tr>
              <td colspan="3" class="text-right font-bold">Closing</td>
              <td class="text-right font-bold">{{ totalDr() | number:'1.2-2' }}</td>
              <td class="text-right font-bold">{{ totalCr() | number:'1.2-2' }}</td>
              <td class="text-right font-bold">{{ stmt()!.closingBalance | number:'1.2-2' }}</td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr><td colspan="6" class="text-center text-500">No transactions in this period.</td></tr>
          </ng-template>
        </p-table>
      </p-card>
    }
  `,
})
export class PartyStatementComponent implements OnInit {
  constructor(private http: HttpClient, private route: ActivatedRoute,
    private toast: MessageService) {}

  companies: Item[] = [];
  parties: Item[] = [];
  stmt = signal<any>(null);
  rows = signal<any[]>([]);
  busy = signal(false);
  f = { companyId: 0 as number, partyId: null as number | null,
        from: null as Date | null, to: null as Date | null };

  totalDr = signal(0);
  totalCr = signal(0);

  ngOnInit() {
    const t = new Date();
    this.f.to = t;
    this.f.from = new Date(t.getFullYear(), t.getMonth() - 2, 1);
    this.http.get<Item[]>(`${API_URL}/api/lookups/companies`).subscribe(c => {
      this.companies = c;
      if (c.length === 1) { this.f.companyId = c[0].id; this.onCompany(); }
    });
    this.route.queryParamMap.subscribe(q => {
      const pid = q.get('partyId');
      if (pid) {
        // Party deep-link: find its company first via detail, then load.
        this.http.get<any>(`${API_URL}/api/parties/${pid}`).subscribe(d => {
          this.f.companyId = d.companyId;
          this.onCompany(() => {
            this.f.partyId = d.id;
            this.load();
          });
        });
      }
    });
  }

  onCompany(after?: () => void) {
    if (!this.f.companyId) return;
    this.f.partyId = null;
    this.http.get<Item[]>(`${API_URL}/api/lookups/parties?companyId=${this.f.companyId}`)
      .subscribe(p => { this.parties = p; if (after) after(); });
  }

  private dstr(d: Date | null): string { return d ? d.toISOString().slice(0, 10) : ''; }

  load() {
    if (!this.f.partyId || !this.f.from || !this.f.to) return;
    this.busy.set(true);
    const url = `${API_URL}/api/parties/${this.f.partyId}/statement` +
      `?from=${this.dstr(this.f.from)}&to=${this.dstr(this.f.to)}`;
    this.http.get<any>(url).subscribe({
      next: s => {
        this.stmt.set(s);
        let bal = s.openingBalance;
        const rows = s.lines.map((l: any) => {
          bal += l.debit - l.credit;
          return { ...l, balance: bal };
        });
        this.rows.set(rows);
        this.totalDr.set(s.lines.reduce((x: number, l: any) => x + l.debit, 0));
        this.totalCr.set(s.lines.reduce((x: number, l: any) => x + l.credit, 0));
        this.busy.set(false);
      },
      error: e => { this.busy.set(false);
        this.toast.add({ severity: 'error', summary: e.error?.message || 'Load failed.' }); },
    });
  }
}
