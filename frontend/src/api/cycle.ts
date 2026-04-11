import { apiClient } from '../lib/http'
import type {
  CycleEntry,
  CycleEntryPayload,
  CycleGuidance,
  CycleSettings,
  CycleSettingsPayload,
  CycleSymptomLog,
  CycleSymptomLogPayload,
} from '../types/cycle'

export async function fetchCycleSettings() {
  const response = await apiClient.get<CycleSettings>('/cycle/settings')
  return response.data
}

export async function updateCycleSettings(payload: CycleSettingsPayload) {
  const response = await apiClient.put<CycleSettings>('/cycle/settings', payload)
  return response.data
}

export async function fetchCycleHistory() {
  const response = await apiClient.get<CycleEntry[]>('/cycle/history')
  return response.data
}

export async function createCycleEntry(payload: CycleEntryPayload) {
  const response = await apiClient.post<CycleEntry>('/cycle/history', payload)
  return response.data
}

export async function updateCycleEntry(entryId: number, payload: CycleEntryPayload) {
  const response = await apiClient.put<CycleEntry>(`/cycle/history/${entryId}`, payload)
  return response.data
}

export async function deleteCycleEntry(entryId: number) {
  await apiClient.delete(`/cycle/history/${entryId}`)
}

export async function fetchCycleSymptomLogs() {
  const response = await apiClient.get<CycleSymptomLog[]>('/cycle/symptoms')
  return response.data
}

export async function createCycleSymptomLog(payload: CycleSymptomLogPayload) {
  const response = await apiClient.post<CycleSymptomLog>('/cycle/symptoms', payload)
  return response.data
}

export async function updateCycleSymptomLog(logId: number, payload: CycleSymptomLogPayload) {
  const response = await apiClient.put<CycleSymptomLog>(`/cycle/symptoms/${logId}`, payload)
  return response.data
}

export async function deleteCycleSymptomLog(logId: number) {
  await apiClient.delete(`/cycle/symptoms/${logId}`)
}

export async function fetchCycleGuidance() {
  const response = await apiClient.get<CycleGuidance>('/cycle/guidance')
  return response.data
}
