import { apiClient } from '../lib/http'
import type {
  AddMealItemRequest,
  CreateMealRequest,
  DailyMeals,
  NutritionFoodDetail,
  NutritionFoodSearchResult,
  UpdateMealItemRequest,
  UpdateMealRequest,
  UserMeal,
  UserMealItem,
} from '../types/nutrition'

export async function searchFoods(q: string, page = 1, pageSize = 10) {
  const response = await apiClient.get<NutritionFoodSearchResult[]>('/nutrition/foods/search', {
    params: { q, page, pageSize },
  })

  return response.data
}

export async function getFoodDetail(source: string, externalId: string) {
  const response = await apiClient.get<NutritionFoodDetail>(
    `/nutrition/foods/${encodeURIComponent(source)}/${encodeURIComponent(externalId)}`,
  )

  return response.data
}

export async function getDailyMeals(date: string) {
  const response = await apiClient.get<DailyMeals>(`/nutrition/days/${encodeURIComponent(date)}`)
  return response.data
}

export async function createMeal(date: string, payload: CreateMealRequest) {
  const response = await apiClient.post<UserMeal>(`/nutrition/days/${encodeURIComponent(date)}/meals`, payload)
  return response.data
}

export async function getMeal(mealId: number) {
  const response = await apiClient.get<UserMeal>(`/nutrition/meals/${mealId}`)
  return response.data
}

export async function updateMeal(mealId: number, payload: UpdateMealRequest) {
  const response = await apiClient.put<UserMeal>(`/nutrition/meals/${mealId}`, payload)
  return response.data
}

export async function deleteMeal(mealId: number) {
  await apiClient.delete(`/nutrition/meals/${mealId}`)
}

export async function addMealItem(mealId: number, payload: AddMealItemRequest) {
  const response = await apiClient.post<UserMealItem>(`/nutrition/meals/${mealId}/items`, payload)
  return response.data
}

export async function updateMealItem(mealId: number, payload: UpdateMealItemRequest) {
  const response = await apiClient.put<UserMealItem>(`/nutrition/meal-items/${mealId}`, payload)
  return response.data
}

export async function deleteMealItem(mealId: number) {
  await apiClient.delete(`/nutrition/meal-items/${mealId}`)
}
