import { apiClient } from '../lib/http'
import type { ExerciseCatalogItem } from '../types/exerciseCatalog'

export async function fetchExerciseCatalog() {
  const response = await apiClient.get<ExerciseCatalogItem[]>('/exercise-catalog')
  return response.data
}

export async function searchExerciseCatalog(query: string) {
  const response = await apiClient.get<ExerciseCatalogItem[]>('/exercise-catalog/search', {
    params: { q: query },
  })
  return response.data
}

export async function fetchExerciseCatalogItem(id: number) {
  const response = await apiClient.get<ExerciseCatalogItem>(`/exercise-catalog/${id}`)
  return response.data
}
