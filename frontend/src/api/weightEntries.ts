import axios from 'axios'
import type { WeightEntry, WeightEntryPayload } from '../types/weight'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5224/api',
})

export async function fetchWeightEntries() {
  const response = await api.get<WeightEntry[]>('/WeightEntries')
  return response.data
}

export async function createWeightEntry(payload: WeightEntryPayload) {
  const response = await api.post<WeightEntry>('/WeightEntries', payload)
  return response.data
}

export async function updateWeightEntry(id: number, payload: WeightEntryPayload) {
  const response = await api.put<WeightEntry>(`/WeightEntries/${id}`, payload)
  return response.data
}

export async function deleteWeightEntry(id: number) {
  await api.delete(`/WeightEntries/${id}`)
}
