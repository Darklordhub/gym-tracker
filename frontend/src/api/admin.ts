import { apiClient } from '../lib/http'
import type {
  AdminExerciseCatalogItem,
  AdminUser,
  ExerciseCatalogSyncResult,
  ResetAdminUserPasswordPayload,
  UpdateExerciseCatalogItemPayload,
  UpdateAdminUserRolePayload,
  UpdateAdminUserStatusPayload,
} from '../types/admin'

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

export async function resetAdminUserPassword(userId: number, payload: ResetAdminUserPasswordPayload) {
  const response = await apiClient.post<{ message: string }>(`/admin/users/${userId}/reset-password`, payload)
  return response.data
}

export async function fetchAdminExerciseCatalog(query = '') {
  const response = await apiClient.get<AdminExerciseCatalogItem[]>('/admin/exercise-catalog', {
    params: query.trim() ? { q: query.trim() } : undefined,
  })
  return response.data
}

export async function updateAdminExerciseCatalogItem(
  itemId: number,
  payload: UpdateExerciseCatalogItemPayload,
) {
  const response = await apiClient.put<AdminExerciseCatalogItem>(`/admin/exercise-catalog/${itemId}`, payload)
  return response.data
}

export async function resetAdminExerciseCatalogItem(itemId: number) {
  const response = await apiClient.post<AdminExerciseCatalogItem>(`/admin/exercise-catalog/${itemId}/reset-provider`)
  return response.data
}

export async function syncAdminExerciseCatalogFromWger() {
  const response = await apiClient.post<ExerciseCatalogSyncResult>('/admin/exercise-catalog/sync-wger')
  return response.data
}
