import { apiClient } from '../lib/http'
import type { GoalSettings, GoalSettingsPayload } from '../types/goals'

export async function fetchGoals() {
  const response = await apiClient.get<GoalSettings>('/Goals')
  return response.data
}

export async function updateGoals(payload: GoalSettingsPayload) {
  const response = await apiClient.put<GoalSettings>('/Goals', payload)
  return response.data
}
