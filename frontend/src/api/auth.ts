import { apiClient } from '../lib/http'
import type { AuthResponse, AuthUser, LoginPayload, RegisterPayload } from '../types/auth'

export async function login(payload: LoginPayload) {
  const response = await apiClient.post<AuthResponse>('/Auth/login', payload)
  return response.data
}

export async function register(payload: RegisterPayload) {
  const response = await apiClient.post<AuthResponse>('/Auth/register', payload)
  return response.data
}

export async function fetchCurrentUser() {
  const response = await apiClient.get<AuthUser>('/Auth/me')
  return response.data
}
