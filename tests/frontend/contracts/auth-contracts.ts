import type {
  AuthResponse,
  LoginCredentials,
  RegisterCredentials,
  User,
} from '../../../src/frontend/service-desk-web/src/features/auth/types';

// Backend UserInfo.FullName is nullable for login and /auth/me.
const restoredUser: User = {
  userId: '00000000-0000-0000-0000-000000000001',
  email: 'synthetic@example.invalid',
  fullName: null,
  roles: ['Requester'],
};
const registrationUser: User = { ...restoredUser, fullName: 'Synthetic User' };
const response: AuthResponse = { token: 'synthetic-token', user: restoredUser };
const login: LoginCredentials = { email: restoredUser.email, password: 'synthetic-password' };
const register: RegisterCredentials = { ...login, fullName: 'Synthetic User' };

// These inputs must remain rejected by TypeScript even if form validation is bypassed.
// @ts-expect-error Password is required by the backend login request.
const missingLoginPassword: LoginCredentials = { email: restoredUser.email };
// @ts-expect-error Password is required by the backend register request.
const missingRegisterPassword: RegisterCredentials = { email: restoredUser.email, fullName: 'Synthetic' };
// @ts-expect-error Role must use the backend's existing controlled vocabulary.
const inventedRole: User = { ...restoredUser, roles: ['Superuser'] };

void [registrationUser, response, login, register, missingLoginPassword, missingRegisterPassword, inventedRole];
