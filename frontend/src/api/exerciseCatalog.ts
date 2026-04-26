import { apiClient } from '../lib/http'
import type { ExerciseCatalogItem, ExerciseCatalogPage } from '../types/exerciseCatalog'

export async function fetchExerciseCatalog() {
  const response = await apiClient.get<ExerciseCatalogItem[]>('/exercise-catalog')
  return response.data
}

export async function searchExerciseCatalog(query: string) {
  const response = await apiClient.get<ExerciseCatalogPage>('/exercise-catalog/search', {
    params: { q: query, page: 1, pageSize: 10 },
  })
  return response.data.items
}

export async function fetchExerciseCatalogPage(page: number, pageSize: number) {
  const response = await apiClient.get<ExerciseCatalogPage>('/exercise-catalog', {
    params: { page, pageSize },
  })
  return response.data
}

export async function searchExerciseCatalogPage(query: string, page: number, pageSize: number) {
  const response = await apiClient.get<ExerciseCatalogPage>('/exercise-catalog/search', {
    params: { q: query, page, pageSize },
  })
  return response.data
}

export async function fetchExerciseCatalogItem(id: number) {
  const response = await apiClient.get<ExerciseCatalogItem>(`/exercise-catalog/${id}`)
  return response.data
}
