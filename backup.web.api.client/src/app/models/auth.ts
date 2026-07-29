export interface AuthUser {
  id: string;
  firstName?: string | null;
  lastName?: string | null;
  username: string;
  role: string;
  isAdmin: boolean;
  permissions?: string[];
  companyId?: string | null;
  companyName?: string | null;
  companies?: { id: string; name: string }[];
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  id: string;
  firstName?: string | null;
  lastName?: string | null;
  username: string;
  role: string;
  token: string;
  isAdmin: boolean;
  permissions?: string[];
  companyId?: string | null;
  companyName?: string | null;
  companies?: { id: string; name: string }[];
}
