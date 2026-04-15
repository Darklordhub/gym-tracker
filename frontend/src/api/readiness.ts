import { apiClient, isNotFoundError } from '../lib/http'
import type { ReadinessLog, ReadinessLogPayload } from '../types/readiness'

export async function fetchLatestReadinessLog() {
  try {
    const response = await apiClient.get<ReadinessLog>('/Readiness/latest')
    return response.data
  } catch (error) {
    if (isNotFoundError(error)) {
      return null
    }

    throw error
  }
}

export async function fetchRecentReadinessLogs(days = 7) {
  const response = await apiClient.get<ReadinessLog[]>('/Readiness/recent', {
    params: { days },
  })
  return response.data
}

export async function upsertReadinessLog(payload: ReadinessLogPayload) {
  const response = await apiClient.post<ReadinessLog>('/Readiness', payload)
  return response.data
}
