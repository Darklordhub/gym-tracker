import { apiClient } from '../lib/http'
import type { AiWorkoutGeneratePayload, AiWorkoutPlan } from '../types/aiWorkout'

export async function generateAiWorkout(payload: AiWorkoutGeneratePayload) {
  const response = await apiClient.post<AiWorkoutPlan>('/ai-workout/generate', payload)
  return response.data
}
