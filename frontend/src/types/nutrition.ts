export type NutritionFoodPortion = {
  unitName: string | null
  amount: number | null
  gramWeight: number | null
  providerPortionId: string | null
  isDefault: boolean
}

export type NutritionFoodSearchResult = {
  source: string
  externalId: string
  name: string
  brandName: string | null
  foodCategory: string | null
  dataType: string | null
  caloriesPer100Grams: number | null
  proteinGramsPer100Grams: number | null
  carbsGramsPer100Grams: number | null
  fatGramsPer100Grams: number | null
}

export type NutritionFoodDetail = {
  source: string
  externalId: string
  name: string
  brandName: string | null
  foodType: string | null
  foodCategory: string | null
  dataType: string | null
  barcode: string | null
  caloriesPer100Grams: number | null
  proteinGramsPer100Grams: number | null
  carbsGramsPer100Grams: number | null
  fatGramsPer100Grams: number | null
  fiberGramsPer100Grams: number | null
  sugarGramsPer100Grams: number | null
  supportedUnits: string[]
  portions?: NutritionFoodPortion[] | null
}

export type UserMealItem = {
  id: number
  nutritionCatalogItemId: number | null
  foodNameSnapshot: string
  brandNameSnapshot: string | null
  sourceProvider: string
  externalFoodId: string
  quantity: number
  unit: string
  consumedGrams: number
  calories: number
  protein: number
  carbs: number
  fat: number
  fiber: number | null
  sugar: number | null
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export type UserMeal = {
  id: number
  date: string
  mealType: string
  title: string | null
  notes: string | null
  totalCalories: number
  totalProtein: number
  totalCarbs: number
  totalFat: number
  totalFiber: number | null
  totalSugar: number | null
  createdAt: string
  updatedAt: string
  items: UserMealItem[]
}

export type DailyMeals = {
  date: string
  meals: UserMeal[]
  totalCalories: number
  totalProtein: number
  totalCarbs: number
  totalFat: number
  totalFiber: number | null
  totalSugar: number | null
  caloriesLinkedToDailyLog: boolean
  sourceMode: string | null
  conflictMessage: string | null
  dailyLogCalories: number | null
}

export type CreateMealRequest = {
  mealType: string
  title: string | null
  notes: string | null
}

export type UpdateMealRequest = {
  date: string
  mealType: string
  title: string | null
  notes: string | null
}

export type AddMealItemRequest = {
  sourceProvider: string
  externalFoodId: string
  quantity: number
  unit: string
  sortOrder?: number
}

export type UpdateMealItemRequest = {
  quantity: number
  unit: string
  sortOrder?: number
}
