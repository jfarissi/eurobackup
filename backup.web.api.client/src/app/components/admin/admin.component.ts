import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MaterialModule } from '../../material.module';
import { PermissionCategory, PermissionSection, PermissionCategories, buildPermissionCategories, allCatalogPermissions, permissionActionLabel } from '../../constants/permissions';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';
import { FormHelpComponent } from '../shared/form-help/form-help.component';
import { HelpApiService, HelpAnalyticsSummary, HelpContentDto } from '../../services/help-api.service';
import { HelpContentService } from '../../services/help-content.service';
import { PermissionService } from '../../services/permission.service';
import { Permissions } from '../../constants/permissions';

interface Tenant { id: string; name: string; isActive: boolean; createdAt: string; companyCount: number; }
interface CompanyAdmin { id: string; tenantId: string; tenantName?: string; name: string; isActive: boolean; defaultLanguageCode: string; defaultCurrencyCode: string; createdAt: string; }
interface RoleAdmin { id: string; name: string; normalizedName?: string; permissions: string[]; }
interface UserAdmin {
  id: string; username: string; email?: string;
  firstName?: string; lastName?: string;
  companyId?: string; isAdmin?: boolean; roles?: string[];
  createdAt?: string;
  companies?: { companyId: string; name: string }[];
}

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, TPipe, FormHelpComponent],
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css']
})
export class AdminComponent implements OnInit {
  selectedTab = 0;

  // Tenants
  tenants: Tenant[] = [];
  showTenantModal = false;
  editingTenantId: string | null = null;
  newTenant: Partial<Tenant> = { name: '', isActive: true };

  // Companies
  companies: CompanyAdmin[] = [];
  showCompanyModal = false;
  editingCompanyId: string | null = null;
  newCompany: Partial<CompanyAdmin> & { tenantId: string } = { name: '', tenantId: '', isActive: true, defaultLanguageCode: 'fr-BE', defaultCurrencyCode: 'EUR' };

  // Roles
  roles: RoleAdmin[] = [];
  allPermissions: string[] = [];
  permissionCategories: PermissionCategory[] = [];
  expandedRoleId: string | null = null; // '__new__' = création
  editingRoleId: string | null = null;
  newRole: { name: string; permissions: string[] } = { name: '', permissions: [] };

  // Users
  users: UserAdmin[] = [];
  expandedUserId: string | null = null;

  // User CRUD
  showUserModal = false;
  editingUserId: string | null = null;
  newUser: { username: string; email: string; firstName: string; lastName: string; password: string; companyId: string; isAdmin: boolean; roleName: string } = this.emptyUser();
  showPasswordResetModal = false;
  resetPasswordUserId: string | null = null;
  resetPasswordUsername = '';
  newPassword = '';

  // Assign user to company
  showAssignModal = false;
  assigningUser: UserAdmin | null = null;
  assignCompanyId = '';

  // Help CMS
  readonly HelpP = Permissions;
  helpArticles: HelpContentDto[] = [];
  helpAnalytics: HelpAnalyticsSummary | null = null;
  showHelpModal = false;
  editingHelp: HelpContentDto = this.emptyHelp();
  helpFilterLang = 'fr';

  loading = false;
  saving = false;
  actionMessage = '';
  actionError = '';

  constructor(
    private http: HttpClient,
    private i18n: AppI18nService,
    private helpApi: HelpApiService,
    private helpContent: HelpContentService,
    public perm: PermissionService
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  emptyHelp(): HelpContentDto {
    return {
      helpKey: '',
      lang: 'fr',
      title: '',
      n1: '',
      body: '',
      rules: '',
      example: '',
      guide: '',
      version: 'v1.0.0',
      status: 'Draft'
    };
  }

  loadHelp(): void {
    if (!this.perm.has(Permissions.HelpManage)) return;
    this.helpApi.listAdmin(this.helpFilterLang || undefined).subscribe({
      next: a => this.helpArticles = a,
      error: () => this.helpArticles = []
    });
    this.helpApi.analyticsSummary(30).subscribe({
      next: s => this.helpAnalytics = s,
      error: () => this.helpAnalytics = null
    });
  }

  openHelpModal(item?: HelpContentDto): void {
    this.editingHelp = item ? { ...item } : this.emptyHelp();
    this.showHelpModal = true;
    this.actionError = '';
  }

  saveHelp(): void {
    if (!this.editingHelp.helpKey || !this.editingHelp.title) {
      this.actionError = 'HelpKey + Title required';
      return;
    }
    this.saving = true;
    const req = this.editingHelp.id
      ? this.helpApi.update(this.editingHelp.id, this.editingHelp)
      : this.helpApi.create(this.editingHelp);
    req.subscribe({
      next: () => {
        this.saving = false;
        this.showHelpModal = false;
        this.actionMessage = this.i18n.t('admin.help.save');
        this.loadHelp();
        this.helpContent.reloadPublished();
      },
      error: (e) => {
        this.saving = false;
        this.actionError = e?.error?.error || 'Error';
      }
    });
  }

  publishHelp(item: HelpContentDto): void {
    if (!item.id) return;
    this.helpApi.transition(item.id, 'Published').subscribe({
      next: () => { this.loadHelp(); this.helpContent.reloadPublished(); },
      error: (e) => this.actionError = e?.error?.error || 'Error'
    });
  }

  archiveHelp(item: HelpContentDto): void {
    if (!item.id) return;
    this.helpApi.archive(item.id).subscribe({
      next: () => { this.loadHelp(); this.helpContent.reloadPublished(); },
      error: (e) => this.actionError = e?.error?.error || 'Error'
    });
  }

  loadAll(): void {
    this.loading = true;
    this.http.get<Tenant[]>('/api/admin/tenants').subscribe(t => this.tenants = t);
    this.http.get<CompanyAdmin[]>('/api/admin/companies').subscribe(c => { this.companies = c; this.loading = false; });
    this.http.get<UserAdmin[]>('/api/admin/users').subscribe(u => this.users = u);
    this.http.get<RoleAdmin[]>('/api/roles').subscribe(r => this.roles = r);
    this.http.get<string[]>('/api/roles/permissions').subscribe({
      next: p => this.applyPermissionCatalog(p),
      error: () => this.applyPermissionCatalog([])
    });
    this.loadHelp();
  }

  /** Fusionne API + constantes locales puis regroupe par catégorie métier. */
  private applyPermissionCatalog(fromApi: string[]): void {
    const local = allCatalogPermissions();
    this.allPermissions = Array.from(new Set([...(fromApi ?? []), ...local])).sort((a, b) => a.localeCompare(b));
    this.permissionCategories = buildPermissionCategories(this.allPermissions);
  }

  /** Permissions d'une catégorie (toutes sections). */
  categoryPermissions(cat: PermissionCategory): string[] {
    return cat.sections.flatMap(s => s.permissions);
  }

  toggleCategoryPermissions(cat: PermissionCategory): void {
    const perms = this.categoryPermissions(cat);
    const allSelected = perms.every(p => this.isPermissionSelected(p));
    if (allSelected) {
      this.newRole.permissions = this.newRole.permissions.filter(p => !perms.includes(p));
    } else {
      for (const p of perms) {
        if (!this.newRole.permissions.includes(p)) this.newRole.permissions.push(p);
      }
    }
  }

  isCategoryAllSelected(cat: PermissionCategory): boolean {
    return this.categoryPermissions(cat).every(p => this.isPermissionSelected(p));
  }

  isCategoryPartialSelected(cat: PermissionCategory): boolean {
    const perms = this.categoryPermissions(cat);
    const sel = perms.filter(p => this.isPermissionSelected(p));
    return sel.length > 0 && sel.length < perms.length;
  }

  toggleSectionPermissions(section: PermissionSection): void {
    const codes = section.permissions as readonly string[];
    const allSelected = codes.every(p => this.isPermissionSelected(p));
    if (allSelected) {
      this.newRole.permissions = this.newRole.permissions.filter(p => !codes.includes(p));
    } else {
      for (const p of codes) {
        if (!this.newRole.permissions.includes(p)) this.newRole.permissions.push(p);
      }
    }
  }

  isSectionAllSelected(section: PermissionSection): boolean {
    return section.permissions.every(p => this.isPermissionSelected(p));
  }

  isSectionPartialSelected(section: PermissionSection): boolean {
    const sel = section.permissions.filter(p => this.isPermissionSelected(p));
    return sel.length > 0 && sel.length < section.permissions.length;
  }

  // ── Tenants ──────────────────────────────────────────────────────────────

  openTenantModal(tenant?: Tenant): void {
    this.showTenantModal = true;
    this.actionError = '';
    if (tenant) {
      this.editingTenantId = tenant.id;
      this.newTenant = { name: tenant.name, isActive: tenant.isActive };
    } else {
      this.editingTenantId = null;
      this.newTenant = { name: '', isActive: true };
    }
  }

  saveTenant(): void {
    if (!this.newTenant.name?.trim()) { this.actionError = this.i18n.t('admin.error.nameRequired'); return; }
    this.saving = true;
    const req = this.editingTenantId
      ? this.http.put<Tenant>(`/api/admin/tenants/${this.editingTenantId}`, this.newTenant)
      : this.http.post<Tenant>('/api/admin/tenants', this.newTenant);
    req.subscribe({
      next: (t) => {
        this.saving = false;
        this.showTenantModal = false;
        this.actionMessage = this.i18n.t('admin.tenantSaved', { name: t.name });
        this.loadAll();
      },
      error: (e) => { this.saving = false; this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  // ── Companies ─────────────────────────────────────────────────────────────

  openCompanyModal(company?: CompanyAdmin): void {
    this.showCompanyModal = true;
    this.actionError = '';
    if (company) {
      this.editingCompanyId = company.id;
      this.newCompany = { name: company.name, tenantId: company.tenantId, isActive: company.isActive, defaultLanguageCode: company.defaultLanguageCode, defaultCurrencyCode: company.defaultCurrencyCode };
    } else {
      this.editingCompanyId = null;
      this.newCompany = { name: '', tenantId: this.tenants[0]?.id ?? '', isActive: true, defaultLanguageCode: 'fr-BE', defaultCurrencyCode: 'EUR' };
    }
  }

  saveCompany(): void {
    if (!this.newCompany.name?.trim()) { this.actionError = this.i18n.t('admin.error.nameRequired'); return; }
    if (!this.newCompany.tenantId) { this.actionError = this.i18n.t('admin.error.tenantRequired'); return; }
    this.saving = true;
    const req = this.editingCompanyId
      ? this.http.put<CompanyAdmin>(`/api/admin/companies/${this.editingCompanyId}`, this.newCompany)
      : this.http.post<CompanyAdmin>('/api/admin/companies', this.newCompany);
    req.subscribe({
      next: (c) => {
        this.saving = false;
        this.showCompanyModal = false;
        this.actionMessage = this.i18n.t('admin.companySaved', { name: c.name });
        this.loadAll();
      },
      error: (e) => { this.saving = false; this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  companiesForTenant(tenantId: string): CompanyAdmin[] {
    return this.companies.filter(c => c.tenantId === tenantId);
  }

  // ── Users ─────────────────────────────────────────────────────────────────

  toggleUserCompanies(user: UserAdmin): void {
    if (this.expandedUserId === user.id) {
      this.expandedUserId = null;
      return;
    }
    this.expandedUserId = user.id;
    this.http.get<{ companyId: string; name: string }[]>(`/api/admin/users/${user.id}/companies`).subscribe(c => {
      user.companies = c;
    });
  }

  openAssignModal(user: UserAdmin): void {
    this.assigningUser = user;
    this.assignCompanyId = this.companies[0]?.id ?? '';
    this.showAssignModal = true;
    this.actionError = '';
  }

  assignUserToCompany(): void {
    if (!this.assigningUser || !this.assignCompanyId) return;
    this.saving = true;
    this.http.post(`/api/admin/users/${this.assigningUser.id}/assign-company/${this.assignCompanyId}`, {}).subscribe({
      next: () => {
        this.saving = false;
        this.showAssignModal = false;
        this.actionMessage = this.i18n.t('admin.userAssigned');
        this.toggleUserCompanies(this.assigningUser!);
        this.loadAll();
      },
      error: (e) => { this.saving = false; this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  removeUserFromCompany(user: UserAdmin, companyId: string): void {
    if (!confirm(this.i18n.t('admin.confirm.removeAccess'))) return;
    this.http.delete(`/api/admin/users/${user.id}/companies/${companyId}`).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('admin.accessRemoved');
        user.companies = user.companies?.filter(c => c.companyId !== companyId);
      },
      error: (e) => { this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  // ── User CRUD ─────────────────────────────────────────────────────────────

  private emptyUser() {
    return { username: '', email: '', firstName: '', lastName: '', password: '', companyId: this.companies[0]?.id ?? '', isAdmin: false, roleName: '' };
  }

  openUserModal(user?: UserAdmin): void {
    this.showUserModal = true;
    this.actionError = '';
    if (user) {
      this.editingUserId = user.id;
      this.newUser = { username: user.username, email: user.email ?? '', firstName: user.firstName ?? '', lastName: user.lastName ?? '', password: '', companyId: user.companyId ?? '', isAdmin: user.isAdmin ?? false, roleName: user.roles?.[0] && user.roles[0] !== 'Admin' ? user.roles[0] : '' };
    } else {
      this.editingUserId = null;
      this.newUser = { username: '', email: '', firstName: '', lastName: '', password: '', companyId: this.companies[0]?.id ?? '', isAdmin: false, roleName: '' };
    }
  }

  saveUser(): void {
    if (!this.newUser.username.trim()) { this.actionError = this.i18n.t('admin.error.usernameRequired'); return; }
    if (!this.editingUserId && !this.newUser.password.trim()) { this.actionError = this.i18n.t('admin.error.passwordRequired'); return; }
    this.saving = true;
    const req = this.editingUserId
      ? this.http.put(`/api/admin/users/${this.editingUserId}`, this.newUser)
      : this.http.post('/api/admin/users', this.newUser);
    req.subscribe({
      next: () => {
        this.saving = false;
        this.showUserModal = false;
        this.actionMessage = this.editingUserId ? this.i18n.t('admin.userUpdated') : this.i18n.t('admin.userCreated');
        this.loadAll();
      },
      error: (e) => { this.saving = false; this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  deleteUser(user: UserAdmin): void {
    if (!confirm(this.i18n.t('admin.confirm.deleteUser', { username: user.username }))) return;
    this.http.delete(`/api/admin/users/${user.id}`).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('admin.userDeleted', { username: user.username });
        this.users = this.users.filter(u => u.id !== user.id);
      },
      error: (e) => { this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  openResetPasswordModal(user: UserAdmin): void {
    this.resetPasswordUserId = user.id;
    this.resetPasswordUsername = user.username;
    this.newPassword = '';
    this.showPasswordResetModal = true;
    this.actionError = '';
  }

  submitResetPassword(): void {
    if (!this.newPassword.trim()) { this.actionError = this.i18n.t('admin.error.passwordRequired'); return; }
    this.saving = true;
    this.http.post(`/api/admin/users/${this.resetPasswordUserId}/reset-password`, { newPassword: this.newPassword }).subscribe({
      next: () => {
        this.saving = false;
        this.showPasswordResetModal = false;
        this.actionMessage = this.i18n.t('admin.passwordReset', { username: this.resetPasswordUsername });
      },
      error: (e) => { this.saving = false; this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  // ── Roles CRUD ────────────────────────────────────────────────────────────

  isRoleExpanded(roleId: string): boolean {
    return this.expandedRoleId === roleId;
  }

  openRoleEditor(role?: RoleAdmin): void {
    this.actionError = '';
    if (role) {
      if (this.expandedRoleId === role.id) {
        this.closeRoleEditor();
        return;
      }
      this.expandedRoleId = role.id;
      this.editingRoleId = role.id;
      const perms = this.isAdminRoleName(role.name)
        ? [...allCatalogPermissions()]
        : [...role.permissions];
      this.newRole = { name: role.name, permissions: perms };
    } else {
      if (this.expandedRoleId === '__new__') {
        this.closeRoleEditor();
        return;
      }
      this.expandedRoleId = '__new__';
      this.editingRoleId = null;
      this.newRole = { name: '', permissions: [] };
    }
  }

  get editingAdminRole(): boolean {
    return this.isAdminRoleName(this.newRole.name) && !!this.editingRoleId;
  }

  isAdminRoleName(name?: string | null): boolean {
    return !!name && name.trim().toLowerCase() === 'admin';
  }

  closeRoleEditor(): void {
    this.expandedRoleId = null;
    this.editingRoleId = null;
    this.newRole = { name: '', permissions: [] };
    this.actionError = '';
  }

  isPermissionSelected(code: string): boolean {
    return this.newRole.permissions.includes(code);
  }

  togglePermission(code: string): void {
    const idx = this.newRole.permissions.indexOf(code);
    if (idx >= 0) this.newRole.permissions.splice(idx, 1);
    else this.newRole.permissions.push(code);
  }

  saveRole(): void {
    if (!this.newRole.name.trim()) { this.actionError = this.i18n.t('admin.error.roleNameRequired'); return; }
    this.saving = true;
    const req = this.editingRoleId
      ? this.http.put<RoleAdmin>(`/api/roles/${this.editingRoleId}`, this.newRole)
      : this.http.post<RoleAdmin>('/api/roles', this.newRole);
    req.subscribe({
      next: () => {
        this.saving = false;
        this.actionMessage = this.editingRoleId ? this.i18n.t('admin.roleUpdated') : this.i18n.t('admin.roleCreated');
        this.closeRoleEditor();
        this.http.get<RoleAdmin[]>('/api/roles').subscribe(r => this.roles = r);
      },
      error: (e) => { this.saving = false; this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  deleteRole(role: RoleAdmin): void {
    if (!confirm(this.i18n.t('admin.confirm.deleteRole', { name: role.name }))) return;
    this.http.delete(`/api/roles/${role.id}`).subscribe({
      next: () => {
        this.actionMessage = this.i18n.t('admin.roleDeleted', { name: role.name });
        if (this.expandedRoleId === role.id) this.closeRoleEditor();
        this.roles = this.roles.filter(r => r.id !== role.id);
      },
      error: (e) => { this.actionError = e?.error?.message || this.i18n.t('common.error'); }
    });
  }

  permissionLabel(code: string): string {
    const section = PermissionCategories
      .flatMap(c => c.sections)
      .find(s => (s.permissions as readonly string[]).includes(code));
    const sectionLabel = section ? this.i18n.t(section.label) : code.split('.')[0];
    return `${sectionLabel} — ${this.i18n.t(permissionActionLabel(code))}`;
  }

  permissionShortLabel(code: string): string {
    return this.i18n.t(permissionActionLabel(code));
  }

  companyName(companyId: string): string {
    return this.companies.find(c => c.id === companyId)?.name ?? companyId;
  }

  clearMessages(): void { this.actionMessage = ''; this.actionError = ''; }
}
