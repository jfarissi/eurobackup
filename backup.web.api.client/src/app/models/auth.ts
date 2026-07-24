export interface AuthUser {
  id: string;
  firstName?: string | null;
  lastName?: string | null;
  username: string;
  role: string;
  isAdmin: boolean;
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
}
