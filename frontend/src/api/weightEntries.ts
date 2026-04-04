import { apiClient } from '../lib/http'
import type { WeightEntry, WeightEntryPayload } from '../types/weight'

export async function fetchWeightEntries() {
  const response = await apiClient.get<WeightEntry[]>('/WeightEntries')
  return response.data
}

export async function createWeightEntry(payload: WeightEntryPayload) {
  const response = await apiClient.post<WeightEntry>('/WeightEntries', payload)
  return response.data
}

export async function updateWeightEntry(id: number, payload: WeightEntryPayload) {
  const response = await apiClient.put<WeightEntry>(`/WeightEntries/${id}`, payload)
  return response.data
}

export async function deleteWeightEntry(id: number) {
  await apiClient.delete(`/WeightEntries/${id}`)
}
