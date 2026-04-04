import axios from 'axios'
import type { GoalSettings, GoalSettingsPayload } from '../types/goals'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5224/api',
})

export async function fetchGoals() {
  const response = await api.get<GoalSettings>('/Goals')
  return response.data
}

export async function updateGoals(payload: GoalSettingsPayload) {
  const response = await api.put<GoalSettings>('/Goals', payload)
  return response.data
}
