import { apiClient } from '../lib/http'
import type {
  AddMealItemRequest,
  CreateMealRequest,
  DailyMeals,
  NutritionFoodDetail,
  NutritionFoodPortion,
  NutritionFoodSearchResult,
  UpdateMealItemRequest,
  UpdateMealRequest,
  UserMeal,
  UserMealItem,
} from '../types/nutrition'

type JsonObject = Record<string, unknown>

export async function searchFoods(q: string, page = 1, pageSize = 10) {
  const response = await apiClient.get<unknown>('/nutrition/foods/search', {
    params: { q, page, pageSize },
  })

  return mapSearchResults(response.data)
}

export async function getFoodDetail(source: string, externalId: string) {
  const response = await apiClient.get<unknown>(
    `/nutrition/foods/${encodeURIComponent(source)}/${encodeURIComponent(externalId)}`,
  )

  return mapFoodDetail(response.data, source, externalId)
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

function mapSearchResults(payload: unknown) {
  if (!Array.isArray(payload)) {
    return []
  }

  return payload
    .map((entry) => mapSearchResult(entry))
    .filter((entry): entry is NutritionFoodSearchResult => entry !== null)
}

function mapSearchResult(payload: unknown): NutritionFoodSearchResult | null {
  const record = asRecord(payload)
  if (!record) {
    return null
  }

  const source = readRequiredString(record, ['source', 'Source'])
  const externalId = readRequiredString(record, ['externalId', 'ExternalId'])
  const name = readRequiredString(record, ['name', 'Name'])

  if (!source || !externalId || !name) {
    return null
  }

  return {
    source,
    externalId,
    name,
    brandName: readNullableString(record, ['brandName', 'BrandName']),
    foodCategory: readNullableString(record, ['foodCategory', 'FoodCategory']),
    dataType: readNullableString(record, ['dataType', 'DataType']),
    caloriesPer100Grams: readNullableNumber(record, [
      'caloriesPer100Grams',
      'caloriesPer100g',
      'CaloriesPer100Grams',
      'CaloriesPer100g',
    ]),
    proteinGramsPer100Grams: readNullableNumber(record, [
      'proteinGramsPer100Grams',
      'proteinPer100Grams',
      'proteinPer100g',
      'ProteinGramsPer100Grams',
      'ProteinPer100Grams',
      'ProteinPer100g',
    ]),
    carbsGramsPer100Grams: readNullableNumber(record, [
      'carbsGramsPer100Grams',
      'carbsPer100Grams',
      'carbsPer100g',
      'CarbsGramsPer100Grams',
      'CarbsPer100Grams',
      'CarbsPer100g',
    ]),
    fatGramsPer100Grams: readNullableNumber(record, [
      'fatGramsPer100Grams',
      'fatPer100Grams',
      'fatPer100g',
      'FatGramsPer100Grams',
      'FatPer100Grams',
      'FatPer100g',
    ]),
  }
}

function mapFoodDetail(payload: unknown, fallbackSource: string, fallbackExternalId: string): NutritionFoodDetail {
  const record = asRecord(payload) ?? {}
  const portions = mapFoodPortions(record)
  const supportedUnits = uniqueStrings([
    ...readStringArray(record, ['supportedUnits', 'SupportedUnits']),
    ...portions
      .map((portion) => portion.unitName)
      .filter((portion): portion is string => Boolean(portion)),
  ])

  return {
    source: readRequiredString(record, ['source', 'Source']) ?? fallbackSource,
    externalId: readRequiredString(record, ['externalId', 'ExternalId']) ?? fallbackExternalId,
    name: readRequiredString(record, ['name', 'Name']) ?? 'Unknown USDA item',
    brandName: readNullableString(record, ['brandName', 'BrandName']),
    foodType: readNullableString(record, ['foodType', 'FoodType']),
    foodCategory: readNullableString(record, ['foodCategory', 'FoodCategory']),
    dataType: readNullableString(record, ['dataType', 'DataType']),
    barcode: readNullableString(record, ['barcode', 'Barcode']),
    caloriesPer100Grams: readNullableNumber(record, [
      'caloriesPer100Grams',
      'caloriesPer100g',
      'CaloriesPer100Grams',
      'CaloriesPer100g',
    ]),
    proteinGramsPer100Grams: readNullableNumber(record, [
      'proteinGramsPer100Grams',
      'proteinPer100Grams',
      'proteinPer100g',
      'ProteinGramsPer100Grams',
      'ProteinPer100Grams',
      'ProteinPer100g',
    ]),
    carbsGramsPer100Grams: readNullableNumber(record, [
      'carbsGramsPer100Grams',
      'carbsPer100Grams',
      'carbsPer100g',
      'CarbsGramsPer100Grams',
      'CarbsPer100Grams',
      'CarbsPer100g',
    ]),
    fatGramsPer100Grams: readNullableNumber(record, [
      'fatGramsPer100Grams',
      'fatPer100Grams',
      'fatPer100g',
      'FatGramsPer100Grams',
      'FatPer100Grams',
      'FatPer100g',
    ]),
    fiberGramsPer100Grams: readNullableNumber(record, [
      'fiberGramsPer100Grams',
      'fiberPer100Grams',
      'fiberPer100g',
      'FiberGramsPer100Grams',
      'FiberPer100Grams',
      'FiberPer100g',
    ]),
    sugarGramsPer100Grams: readNullableNumber(record, [
      'sugarGramsPer100Grams',
      'sugarPer100Grams',
      'sugarPer100g',
      'SugarGramsPer100Grams',
      'SugarPer100Grams',
      'SugarPer100g',
    ]),
    supportedUnits,
    portions: portions.length > 0 ? portions : null,
  }
}

function mapFoodPortions(record: JsonObject) {
  const rawPortions = readArrayValue(record, ['portions', 'Portions'])
  if (!rawPortions) {
    return []
  }

  return rawPortions
    .map((portion) => {
      const entry = asRecord(portion)
      if (!entry) {
        return null
      }

      return {
        unitName: readNullableString(entry, ['unitName', 'UnitName']),
        amount: readNullableNumber(entry, ['amount', 'Amount']),
        gramWeight: readNullableNumber(entry, ['gramWeight', 'GramWeight']),
        providerPortionId: readNullableString(entry, ['providerPortionId', 'ProviderPortionId']),
        isDefault: readBoolean(entry, ['isDefault', 'IsDefault']) ?? false,
      } satisfies NutritionFoodPortion
    })
    .filter((portion): portion is NutritionFoodPortion => portion !== null)
}

function asRecord(value: unknown): JsonObject | null {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? (value as JsonObject)
    : null
}

function readRequiredString(record: JsonObject, keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string' && value.trim()) {
      return value.trim()
    }
  }

  return null
}

function readNullableString(record: JsonObject, keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'string') {
      const normalizedValue = value.trim()
      return normalizedValue.length > 0 ? normalizedValue : null
    }
  }

  return null
}

function readNullableNumber(record: JsonObject, keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value
    }

    if (typeof value === 'string' && value.trim()) {
      const parsedValue = Number(value)
      if (Number.isFinite(parsedValue)) {
        return parsedValue
      }
    }
  }

  return null
}

function readArrayValue(record: JsonObject, keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (Array.isArray(value)) {
      return value
    }
  }

  return null
}

function readStringArray(record: JsonObject, keys: string[]) {
  const value = readArrayValue(record, keys)
  if (!value) {
    return []
  }

  return value
    .map((entry) => (typeof entry === 'string' ? entry.trim() : ''))
    .filter(Boolean)
}

function readBoolean(record: JsonObject, keys: string[]) {
  for (const key of keys) {
    const value = record[key]
    if (typeof value === 'boolean') {
      return value
    }
  }

  return null
}

function uniqueStrings(values: string[]) {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))]
}
