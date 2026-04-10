import { apiClient } from '../lib/http'
import type { ChangePasswordPayload, UpdateProfilePayload, UserProfile } from '../types/profile'

export async function fetchProfile() {
  const response = await apiClient.get<UserProfile>('/profile')
  return response.data
}

export async function updateProfile(payload: UpdateProfilePayload) {
  const response = await apiClient.put<UserProfile>('/profile', payload)
  return response.data
}

export async function changePassword(payload: ChangePasswordPayload) {
  await apiClient.put('/profile/password', payload)
}
