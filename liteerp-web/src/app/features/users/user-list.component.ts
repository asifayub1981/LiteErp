import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToolbarModule } from 'primeng/toolbar';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { API_URL } from '../../core/api';

interface UserRow {
  id: string; userName: string; displayName: string;
  companyId: number | null; companyName: string | null; isActive: boolean;
}

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [RouterModule, FormsModule, ButtonModule, TableModule, ToolbarModule,
    CardModule, InputTextModule, TagModule],
  template: `
    <p-toolbar>
      <ng-template #start><span class="font-bold text-lg">Users</span></ng-template>
      <ng-template #end>
        <p-button  label="New User" icon="pi pi-plus" routerLink="/users/new"></p-button>
      </ng-template>
    </p-toolbar>

    <p-card styleClass="mt-3">
      <div class="flex flex-wrap gap-3 align-items-end mb-3">
        <div class="report-field">
          <label>Search</label>
          <input pInputText [(ngModel)]="search" placeholder="Username / name"
            (keyup.enter)="load()" class="w-16rem" />
        </div>
        <p-button  label="Search" icon="pi pi-search" severity="secondary" (click)="load()"></p-button>
      </div>

      <p-table [value]="rows()" styleClass="p-datatable-sm">
        <ng-template #header>
          <tr>
            <th>Username</th><th>Display Name</th><th>Company</th><th>Status</th>
            <th style="width:8rem">Actions</th>
          </tr>
        </ng-template>
        <ng-template #body let-r>
          <tr>
            <td class="font-bold">{{ r.userName }}</td>
            <td>{{ r.displayName }}</td>
            <td>{{ r.companyName ?? '—' }}</td>
            <td><p-tag [value]="r.isActive ? 'Active' : 'Inactive'"
              [severity]="r.isActive ? 'success' : 'danger'" /></td>
            <td>
              <p-button  icon="pi pi-pencil" [text]="true" size="small"
                pTooltip="Edit" [routerLink]="['/users', r.id]"></p-button>
              <p-button  icon="pi pi-key" [text]="true" size="small"
                pTooltip="Rights" [routerLink]="['/users', r.id, 'rights']"></p-button>
            </td>
          </tr>
        </ng-template>
        <ng-template #emptymessage>
          <tr><td colspan="5" class="text-center text-500">No users found.</td></tr>
        </ng-template>
      </p-table>
    </p-card>
  `,
})
export class UserListComponent implements OnInit {
  rows = signal<UserRow[]>([]);
  search = '';

  constructor(private http: HttpClient) {}

  ngOnInit() { this.load(); }

  load() {
    const params: any = {};
    if (this.search.trim()) params.search = this.search.trim();
    this.http.get<UserRow[]>(`${API_URL}/api/users`, { params })
      .subscribe(r => this.rows.set(r));
  }
}
