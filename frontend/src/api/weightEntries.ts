import { apiClient } from '../lib/http'
import { normalizeDateOnlyValue } from '../lib/dateOnly'
import type { WeightEntry, WeightEntryPayload } from '../types/weight'

export async function fetchWeightEntries() {
  const response = await apiClient.get<WeightEntry[]>('/WeightEntries')
  return response.data.map(normalizeWeightEntry)
}

export async function createWeightEntry(payload: WeightEntryPayload) {
  const response = await apiClient.post<WeightEntry>('/WeightEntries', payload)
  return normalizeWeightEntry(response.data)
}

export async function updateWeightEntry(id: number, payload: WeightEntryPayload) {
  const response = await apiClient.put<WeightEntry>(`/WeightEntries/${id}`, payload)
  return normalizeWeightEntry(response.data)
}

export async function deleteWeightEntry(id: number) {
  await apiClient.delete(`/WeightEntries/${id}`)
}

function normalizeWeightEntry(entry: WeightEntry): WeightEntry {
  return {
    ...entry,
    date: normalizeDateOnlyValue(entry.date),
  }
}
