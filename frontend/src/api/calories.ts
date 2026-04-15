import { apiClient, isNotFoundError } from '../lib/http'
import type { CalorieLog, CalorieLogPayload } from '../types/calories'

export async function fetchLatestCalorieLog() {
  try {
    const response = await apiClient.get<CalorieLog>('/Calories/latest')
    return response.data
  } catch (error) {
    if (isNotFoundError(error)) {
      return null
    }

    throw error
  }
}

export async function upsertCalorieLog(payload: CalorieLogPayload) {
  const response = await apiClient.post<CalorieLog>('/Calories', payload)
  return response.data
}
