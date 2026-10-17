import { Component, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { TreeModule } from 'primeng/tree';
import { TreeNode } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { ToolbarModule } from 'primeng/toolbar';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { API_URL } from '../../core/api';

interface CoaNodeDto {
  id: number; code: string; name: string; accountType: number;
  isTransactional: boolean; isActive: boolean; activeForCompany: boolean;
  children: CoaNodeDto[];
}

const ACCOUNT_TYPES = [
  { label: 'Asset', value: 1 }, { label: 'Liability', value: 2 },
  { label: 'Capital', value: 3 }, { label: 'Revenue', value: 4 },
  { label: 'Expense', value: 5 }
];

@Component({
  selector: 'app-coa-tree',
  standalone: true,
  imports: [FormsModule, TreeModule, ButtonModule, DialogModule, InputTextModule,
    CheckboxModule, SelectModule, ToolbarModule, TagModule, ToastModule],
  providers: [MessageService],
  template: `
    <p-toast />
    <p-toolbar>
      <ng-template pTemplate="start">
        <span class="font-bold text-lg">COA Definition</span>
      </ng-template>
      <ng-template pTemplate="center">
        <p-select [options]="companies" [(ngModel)]="companyId" optionLabel="name"
          optionValue="id" placeholder="Select company" (onChange)="load()"
          styleClass="w-15rem" />
      </ng-template>
      <ng-template pTemplate="end">
        <div class="flex gap-2">
          <button pButton icon="pi pi-plus" label="Root Head" severity="secondary" size="small"
            (click)="openDialog(null, 'root')" [disabled]="!companyId"></button>
          <button pButton icon="pi pi-angle-double-down" label="Expand all" severity="secondary"
            size="small" (click)="expandAll()"></button>
          <button pButton icon="pi pi-angle-double-up" label="Collapse all" severity="secondary"
            size="small" (click)="collapseAll()"></button>
        </div>
      </ng-template>
    </p-toolbar>

    <p-tree [value]="nodes()" selectionMode="single" [(selection)]="selected"
      styleClass="mt-3" emptyMessage="Select a company to load the chart of accounts.">
      <ng-template let-node pTemplate="default">
        <div class="flex align-items-center gap-2">
          <span class="font-mono text-500 text-sm">{{ node.data.code }}</span>
          <span [class.text-400]="!node.data.isActive">{{ node.label }}</span>
          <p-tag [value]="typeLabel(node.data.accountType)" severity="info" />
          @if (node.data.isTransactional) {
            <p-tag value="Transactional" severity="success" />
          } @else {
            <p-tag value="Group" severity="warn" />
          }
          @if (!node.data.isActive) { <p-tag value="Inactive" severity="danger" /> }
          @if (!node.data.activeForCompany) { <p-tag value="Not for company" severity="secondary" /> }
          <span class="flex gap-1 ml-2">
            <button pButton icon="pi pi-plus" size="small" severity="secondary" pTooltip="Add child"
              (click)="openDialog(node, 'child'); $event.stopPropagation()"></button>
            <button pButton icon="pi pi-pencil" size="small" severity="secondary" pTooltip="Edit"
              (click)="openDialog(node, 'edit'); $event.stopPropagation()"></button>
            <button pButton icon="pi pi-trash" size="small" severity="danger" pTooltip="Delete"
              (click)="remove(node); $event.stopPropagation()"></button>
          </span>
        </div>
      </ng-template>
    </p-tree>

    <p-dialog [header]="dialogTitle" [(visible)]="dialogVisible" [modal]="true" [style]="{ width: '28rem' }">
      <div class="flex flex-column gap-3">
        <span class="p-float-label">
          <input pInputText id="code" [(ngModel)]="form.code" class="w-full" />
          <label for="code">Code</label>
        </span>
        <span class="p-float-label">
          <input pInputText id="name" [(ngModel)]="form.name" class="w-full" />
          <label for="name">Name</label>
        </span>
        <p-select [options]="accountTypes" [(ngModel)]="form.accountType" optionLabel="label"
          optionValue="value" placeholder="Account type" styleClass="w-full" />
        <div class="flex gap-4">
          <p-checkbox [(ngModel)]="form.isTransactional" [binary]="true" label="Transactional (leaf)" inputId="txn" />
          <p-checkbox [(ngModel)]="form.isActive" [binary]="true" label="Active" inputId="act" />
        </div>
      </div>
      <ng-template pTemplate="footer">
        <button pButton label="Cancel" severity="secondary" (click)="dialogVisible = false"></button>
        <button pButton label="Save" (click)="save()" [loading]="saving()"></button>
      </ng-template>
    </p-dialog>
  `
})
export class CoaTreeComponent implements OnInit {
  nodes = signal<TreeNode[]>([]);
  companies: { id: number; name: string }[] = [];
  companyId: number | null = null;
  selected: TreeNode | null = null;
  accountTypes = ACCOUNT_TYPES;

  dialogVisible = false;
  dialogTitle = '';
  dialogMode: 'root' | 'child' | 'edit' = 'root';
  dialogNode: TreeNode | null = null;
  saving = signal(false);
  form = { code: '', name: '', accountType: 1, isTransactional: true, isActive: true };

  constructor(private http: HttpClient, private toast: MessageService) {}

  ngOnInit() {
    this.http.get<{ id: number; name: string }[]>(`${API_URL}/api/lookups/companies`)
      .subscribe(cs => {
        this.companies = cs;
        if (cs.length === 1) { this.companyId = cs[0].id; this.load(); }
      });
  }

  typeLabel(v: number): string { return ACCOUNT_TYPES.find(t => t.value === v)?.label ?? String(v); }

  load() {
    if (!this.companyId) return;
    this.http.get<CoaNodeDto[]>(`${API_URL}/api/coa/tree`, { params: { companyId: this.companyId } })
      .subscribe(dtos => this.nodes.set(dtos.map(d => this.toTreeNode(d))));
  }

  private toTreeNode(d: CoaNodeDto): TreeNode {
    return {
      key: String(d.id), label: d.name, data: d, expanded: false,
      children: d.children.map(c => this.toTreeNode(c))
    };
  }

  expandAll() { this.nodes().forEach(n => this.setExpanded(n, true)); }
  collapseAll() { this.nodes().forEach(n => this.setExpanded(n, false)); }
  private setExpanded(n: TreeNode, v: boolean) {
    n.expanded = v;
    n.children?.forEach(c => this.setExpanded(c, v));
  }

  openDialog(node: TreeNode | null, mode: 'root' | 'child' | 'edit') {
    this.dialogMode = mode;
    this.dialogNode = node;
    if (mode === 'edit' && node) {
      const d = node.data as CoaNodeDto;
      this.dialogTitle = `Edit head ${d.code}`;
      this.form = { code: d.code, name: d.name, accountType: d.accountType, isTransactional: d.isTransactional, isActive: d.isActive };
    } else {
      this.dialogTitle = mode === 'root' ? 'New root head' : `New child of ${(node!.data as CoaNodeDto).code}`;
      const parentType = mode === 'child' ? (node!.data as CoaNodeDto).accountType : 1;
      this.form = { code: '', name: '', accountType: parentType, isTransactional: true, isActive: true };
    }
    this.dialogVisible = true;
  }

  save() {
    if (!this.form.code.trim() || !this.form.name.trim()) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Code and Name are required.' });
      return;
    }
    this.saving.set(true);
    const body = {
      parentId: this.dialogMode === 'child' ? (this.dialogNode!.data as CoaNodeDto).id : null,
      ...this.form, code: this.form.code.trim(), name: this.form.name.trim()
    };
    const req = this.dialogMode === 'edit'
      ? this.http.put(`${API_URL}/api/coa/${(this.dialogNode!.data as CoaNodeDto).id}`, body)
      : this.http.post(`${API_URL}/api/coa`, body);
    req.subscribe({
      next: () => { this.saving.set(false); this.dialogVisible = false; this.load(); this.toast.add({ severity: 'success', summary: 'Saved' }); },
      error: (e) => { this.saving.set(false); this.toast.add({ severity: 'error', summary: 'Error', detail: e.error?.message ?? 'Save failed.' }); }
    });
  }

  remove(node: TreeNode) {
    const d = node.data as CoaNodeDto;
    if (!confirm(`Delete head "${d.code} - ${d.name}"?`)) return;
    this.http.delete(`${API_URL}/api/coa/${d.id}`).subscribe({
      next: () => { this.load(); this.toast.add({ severity: 'success', summary: 'Deleted' }); },
      error: (e) => this.toast.add({ severity: 'error', summary: 'Error', detail: e.error?.message ?? 'Delete failed.' })
    });
  }
}
