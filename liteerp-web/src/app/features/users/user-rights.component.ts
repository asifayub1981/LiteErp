import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { API_URL } from '../../core/api';

interface RightRow {
  formCode: string; formLabel: string;
  canView: boolean; canAdd: boolean; canEdit: boolean; canDelete: boolean;
}

@Component({
  selector: 'app-user-rights',
  standalone: true,
  imports: [RouterModule, FormsModule, ButtonModule, TableModule, ToolbarModule,
    CardModule, CheckboxModule, ToastModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <p-toolbar>
      <ng-template #start>
        <span class="font-bold text-lg">User Rights — {{ userName() }}</span>
      </ng-template>
      <ng-template #end>
        <div class="flex gap-2">
          <p-button  label="Back to Users" icon="pi pi-arrow-left" severity="secondary"
            routerLink="/users"></p-button>
          <p-button  label="Save Rights" icon="pi pi-check"
            (click)="save()" [loading]="saving()"></p-button>
        </div>
      </ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <p class="text-500 text-sm mt-0">
        Tick what this user is allowed to do on each form.
        <a class="cursor-pointer text-primary" (click)="grantAll(true)">Grant all</a> /
        <a class="cursor-pointer text-primary" (click)="grantAll(false)">Revoke all</a>
      </p>
      <p-table [value]="rows()" styleClass="p-datatable-sm">
        <ng-template #header>
          <tr>
            <th>Form</th>
            <th style="text-align:center">View</th>
            <th style="text-align:center">Add</th>
            <th style="text-align:center">Edit</th>
            <th style="text-align:center">Delete</th>
          </tr>
        </ng-template>
        <ng-template #body let-r>
          <tr>
            <td class="font-bold">{{ r.formLabel }}</td>
            <td style="text-align:center"><p-checkbox [(ngModel)]="r.canView" [binary]="true" /></td>
            <td style="text-align:center"><p-checkbox [(ngModel)]="r.canAdd" [binary]="true" /></td>
            <td style="text-align:center"><p-checkbox [(ngModel)]="r.canEdit" [binary]="true" /></td>
            <td style="text-align:center"><p-checkbox [(ngModel)]="r.canDelete" [binary]="true" /></td>
          </tr>
        </ng-template>
      </p-table>
    </p-card>
  `,
})
export class UserRightsComponent implements OnInit {
  rows = signal<RightRow[]>([]);
  userName = signal('');
  saving = signal(false);
  private id = '';

  constructor(
    private http: HttpClient, private route: ActivatedRoute,
    private toast: MessageService,
  ) {}

  ngOnInit() {
    this.route.paramMap.subscribe(p => {
      this.id = p.get('id') ?? '';
      this.load();
    });
  }

  private load() {
    this.http.get<any>(`${API_URL}/api/users/${this.id}`)
      .subscribe(u => this.userName.set(u.displayName));
    this.http.get<RightRow[]>(`${API_URL}/api/users/${this.id}/rights`)
      .subscribe(r => this.rows.set(r));
  }

  grantAll(v: boolean) {
    this.rows.update(rows => rows.map(r => ({
      ...r, canView: v, canAdd: v, canEdit: v, canDelete: v,
    })));
  }

  save() {
    this.saving.set(true);
    const body = this.rows().map(r => ({
      formCode: r.formCode, canView: r.canView, canAdd: r.canAdd,
      canEdit: r.canEdit, canDelete: r.canDelete,
    }));
    this.http.put(`${API_URL}/api/users/${this.id}/rights`, body).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.add({ severity: 'success', summary: 'Rights saved' });
      },
      error: () => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Save failed.' });
      }
    });
  }
}
