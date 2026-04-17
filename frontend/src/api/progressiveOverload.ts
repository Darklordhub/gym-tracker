import { apiClient } from '../lib/http'
import type { ProgressiveOverloadRecommendation } from '../types/progressiveOverload'

export async function fetchProgressiveOverloadRecommendation(exerciseName: string) {
  const response = await apiClient.get<ProgressiveOverloadRecommendation>('/ProgressiveOverload/recommendation', {
    params: { exerciseName },
  })

  return response.data
}
