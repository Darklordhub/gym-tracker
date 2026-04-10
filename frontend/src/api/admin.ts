import { apiClient } from '../lib/http'
import type { AdminUser, UpdateAdminUserRolePayload, UpdateAdminUserStatusPayload } from '../types/admin'

export async function fetchAdminUsers() {
  const response = await apiClient.get<AdminUser[]>('/admin/users')
  return response.data
}

export async function updateAdminUserRole(userId: number, payload: UpdateAdminUserRolePayload) {
  const response = await apiClient.put<AdminUser>(`/admin/users/${userId}/role`, payload)
  return response.data
}

export async function updateAdminUserStatus(userId: number, payload: UpdateAdminUserStatusPayload) {
  const response = await apiClient.put<AdminUser>(`/admin/users/${userId}/status`, payload)
  return response.data
}
