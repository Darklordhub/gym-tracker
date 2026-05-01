import { useEffect, useRef, useState, type FormEvent } from 'react'
import { PencilLine, Plus, Save, Search, Trash2, UtensilsCrossed, X } from 'lucide-react'
import {
  addMealItem,
  createMeal,
  deleteMeal,
  deleteMealItem,
  getDailyMeals,
  getFoodDetail,
  searchFoods,
  updateMeal,
  updateMealItem,
} from '../api/nutritionApi'
import { StateCard } from '../components/StateCard'
import { formatDate, getTodayDateValue } from '../lib/format'
import { getRequestErrorMessage, isNotFoundError } from '../lib/http'
import type {
  DailyMeals,
  NutritionFoodDetail,
  NutritionFoodSearchResult,
  UpdateMealItemRequest,
  UpdateMealRequest,
  UserMeal,
  UserMealItem,
} from '../types/nutrition'

const SEARCH_PAGE_SIZE = 12
const SEARCH_MIN_CHARACTERS = 2
const SEARCH_DEBOUNCE_MS = 350
const GRAM_UNIT = 'g'
const MEAL_TYPE_OPTIONS = [
  { value: 'breakfast', label: 'Breakfast' },
  { value: 'lunch', label: 'Lunch' },
  { value: 'dinner', label: 'Dinner' },
  { value: 'snack', label: 'Snack' },
] as const

type MealTypeValue = (typeof MEAL_TYPE_OPTIONS)[number]['value']
type PageFeedback = { tone: 'success' | 'error'; message: string } | null
type FoodResolutionState = 'usable' | 'unusable' | 'error'
type MealGroup = {
  key: string
  label: string
  description: string
  meals: UserMeal[]
}
type CreateMealFormState = {
  mealType: MealTypeValue
  title: string
  notes: string
}
type FoodDetailStatus = {
  canAdd: boolean
  hasCalories: boolean
  message: string | null
}

const initialCreateMealFormState = (): CreateMealFormState => ({
  mealType: 'breakfast',
  title: '',
  notes: '',
})

export function NutritionPage() {
  const [selectedDate, setSelectedDate] = useState(getTodayDateValue())
  const [dailyMeals, setDailyMeals] = useState<DailyMeals | null>(null)
  const [isLoadingMeals, setIsLoadingMeals] = useState(true)
  const [isRefreshingMeals, setIsRefreshingMeals] = useState(false)
  const [mealsErrorMessage, setMealsErrorMessage] = useState<string | null>(null)
  const [pageFeedback, setPageFeedback] = useState<PageFeedback>(null)
  const [targetMealId, setTargetMealId] = useState<number | null>(null)

  const [createMealForm, setCreateMealForm] = useState<CreateMealFormState>(initialCreateMealFormState)
  const [isCreatingMeal, setIsCreatingMeal] = useState(false)
  const [savingMealId, setSavingMealId] = useState<number | null>(null)
  const [deletingMealId, setDeletingMealId] = useState<number | null>(null)
  const [updatingItemId, setUpdatingItemId] = useState<number | null>(null)
  const [deletingItemId, setDeletingItemId] = useState<number | null>(null)

  const [searchQuery, setSearchQuery] = useState('')
  const [debouncedSearchQuery, setDebouncedSearchQuery] = useState('')
  const [searchResults, setSearchResults] = useState<NutritionFoodSearchResult[]>([])
  const [isSearching, setIsSearching] = useState(false)
  const [searchErrorMessage, setSearchErrorMessage] = useState<string | null>(null)

  const [selectedFoodSummary, setSelectedFoodSummary] = useState<NutritionFoodSearchResult | null>(null)
  const [selectedFoodDetail, setSelectedFoodDetail] = useState<NutritionFoodDetail | null>(null)
  const [selectedFoodLoading, setSelectedFoodLoading] = useState(false)
  const [selectedFoodError, setSelectedFoodError] = useState<string | null>(null)
  const [foodResolutionByKey, setFoodResolutionByKey] = useState<Record<string, FoodResolutionState>>({})
  const [quantityInput, setQuantityInput] = useState('100')
  const [unit, setUnit] = useState(GRAM_UNIT)
  const [isAddingItem, setIsAddingItem] = useState(false)

  const latestMealsRequestRef = useRef(0)
  const latestSearchRequestRef = useRef(0)
  const latestFoodDetailRequestRef = useRef(0)

  useEffect(() => {
    void loadDailyMeals(selectedDate)
  }, [selectedDate])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedSearchQuery(searchQuery.trim())
    }, SEARCH_DEBOUNCE_MS)

    return () => window.clearTimeout(timeoutId)
  }, [searchQuery])

  useEffect(() => {
    if (debouncedSearchQuery.length === 0) {
      latestSearchRequestRef.current += 1
      setSearchResults([])
      setSearchErrorMessage(null)
      setIsSearching(false)
      return
    }

    if (debouncedSearchQuery.length < SEARCH_MIN_CHARACTERS) {
      latestSearchRequestRef.current += 1
      setSearchResults([])
      setSearchErrorMessage(null)
      setIsSearching(false)
      return
    }

    const requestId = ++latestSearchRequestRef.current

    async function runSearch() {
      try {
        setIsSearching(true)
        setSearchErrorMessage(null)
        const results = await searchFoods(debouncedSearchQuery, 1, SEARCH_PAGE_SIZE)

        if (requestId !== latestSearchRequestRef.current) {
          return
        }

        setSearchResults(results)
      } catch (error) {
        if (requestId !== latestSearchRequestRef.current) {
          return
        }

        setSearchResults([])
        setSearchErrorMessage(getRequestErrorMessage(error, 'Unable to search USDA foods right now.'))
      } finally {
        if (requestId === latestSearchRequestRef.current) {
          setIsSearching(false)
        }
      }
    }

    void runSearch()
  }, [debouncedSearchQuery])

  const activeDailyMeals = dailyMeals?.date === selectedDate ? dailyMeals : null
  const mealCount = activeDailyMeals?.meals.length ?? 0
  const groupedMeals = buildMealGroups(activeDailyMeals?.meals ?? [])
  const targetMeal = activeDailyMeals?.meals.find((meal) => meal.id === targetMealId) ?? null
  const quantityValue = parsePositiveNumber(quantityInput)
  const quantityError =
    quantityInput.trim().length === 0
      ? 'Quantity is required.'
      : quantityValue === null
        ? 'Enter a quantity greater than zero.'
        : null
  const foodDetailStatus = getFoodDetailStatus(selectedFoodDetail)
  const addFoodValidationMessage = getAddFoodValidationMessage({
    selectedFoodSummary,
    selectedFoodDetail,
    selectedFoodError,
    selectedFoodLoading,
    targetMealId,
    quantityError,
    unit,
    foodDetailStatus,
  })
  const canAddFoodToMeal =
    Boolean(selectedFoodDetail)
    && foodDetailStatus.canAdd
    && Boolean(targetMealId)
    && !quantityError
    && unit === GRAM_UNIT
    && !selectedFoodLoading
    && !selectedFoodError
    && !isAddingItem
  const selectedFoodPreview = buildFoodPreview(selectedFoodDetail, quantityValue)

  async function loadDailyMeals(dateValue: string, preferredMealId?: number | null) {
    const requestId = ++latestMealsRequestRef.current
    const isHardLoad = !dailyMeals || dailyMeals.date !== dateValue

    try {
      if (isHardLoad) {
        setIsLoadingMeals(true)
      } else {
        setIsRefreshingMeals(true)
      }

      setMealsErrorMessage(null)
      const nextDailyMeals = await getDailyMeals(dateValue)

      if (requestId !== latestMealsRequestRef.current) {
        return
      }

      setDailyMeals(nextDailyMeals)
      setTargetMealId((currentTargetMealId) =>
        resolveTargetMealId(nextDailyMeals.meals, preferredMealId ?? null, currentTargetMealId),
      )
    } catch (error) {
      if (requestId !== latestMealsRequestRef.current) {
        return
      }

      setMealsErrorMessage(getRequestErrorMessage(error, 'Unable to load meals for this date.'))
    } finally {
      if (requestId === latestMealsRequestRef.current) {
        setIsLoadingMeals(false)
        setIsRefreshingMeals(false)
      }
    }
  }

  async function handleSelectFood(food: NutritionFoodSearchResult) {
    const requestId = ++latestFoodDetailRequestRef.current
    const foodKey = buildFoodKey(food.source, food.externalId)

    setSelectedFoodSummary(food)
    setSelectedFoodDetail(null)
    setSelectedFoodError(null)
    setSelectedFoodLoading(true)
    setPageFeedback(null)

    try {
      const detail = await getFoodDetail(food.source, food.externalId)

      if (requestId !== latestFoodDetailRequestRef.current) {
        return
      }

      const detailStatus = getFoodDetailStatus(detail)
      setSelectedFoodDetail(detail)
      setFoodResolutionByKey((current) => ({
        ...current,
        [foodKey]: detailStatus.canAdd ? 'usable' : 'unusable',
      }))
    } catch (error) {
      if (requestId !== latestFoodDetailRequestRef.current) {
        return
      }

      setSelectedFoodDetail(null)
      setSelectedFoodError(buildFoodDetailErrorMessage(error))
      setFoodResolutionByKey((current) => ({
        ...current,
        [foodKey]: 'error',
      }))
    } finally {
      if (requestId === latestFoodDetailRequestRef.current) {
        setSelectedFoodLoading(false)
      }
    }
  }

  async function handleCreateMeal(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    try {
      setIsCreatingMeal(true)
      setPageFeedback(null)

      const meal = await createMeal(selectedDate, {
        mealType: createMealForm.mealType,
        title: normalizeOptionalText(createMealForm.title),
        notes: normalizeOptionalText(createMealForm.notes),
      })

      setCreateMealForm((current) => ({
        ...current,
        title: '',
        notes: '',
      }))
      await loadDailyMeals(selectedDate, meal.id)
      setPageFeedback({ tone: 'success', message: `Meal created for ${formatMealType(meal.mealType)}.` })
    } catch (error) {
      setPageFeedback({
        tone: 'error',
        message: getRequestErrorMessage(error, 'Unable to create a meal right now.'),
      })
    } finally {
      setIsCreatingMeal(false)
    }
  }

  async function handleSaveMeal(mealId: number, payload: UpdateMealRequest) {
    try {
      setSavingMealId(mealId)
      setPageFeedback(null)
      await updateMeal(mealId, payload)
      await loadDailyMeals(selectedDate, mealId)
      setPageFeedback({ tone: 'success', message: 'Meal updated.' })
      return true
    } catch (error) {
      setPageFeedback({
        tone: 'error',
        message: getRequestErrorMessage(error, 'Unable to update this meal.'),
      })
      return false
    } finally {
      setSavingMealId(null)
    }
  }

  async function handleDeleteMeal(meal: UserMeal) {
    const confirmed = window.confirm(`Delete "${getMealDisplayName(meal)}" and all of its items?`)
    if (!confirmed) {
      return
    }

    try {
      setDeletingMealId(meal.id)
      setPageFeedback(null)
      await deleteMeal(meal.id)
      await loadDailyMeals(selectedDate, meal.id === targetMealId ? null : targetMealId)
      setPageFeedback({ tone: 'success', message: 'Meal deleted.' })
    } catch (error) {
      setPageFeedback({
        tone: 'error',
        message: getRequestErrorMessage(error, 'Unable to delete this meal.'),
      })
    } finally {
      setDeletingMealId(null)
    }
  }

  async function handleAddMealItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!selectedFoodDetail || addFoodValidationMessage || !quantityValue || !targetMealId) {
      setPageFeedback({
        tone: 'error',
        message: addFoodValidationMessage ?? 'Select a usable USDA food before adding it to a meal.',
      })
      return
    }

    try {
      const targetMealName = targetMeal ? getMealDisplayName(targetMeal) : 'the selected meal'
      setIsAddingItem(true)
      setPageFeedback(null)
      await addMealItem(targetMealId, {
        sourceProvider: selectedFoodDetail.source,
        externalFoodId: selectedFoodDetail.externalId,
        quantity: quantityValue,
        unit,
      })
      await loadDailyMeals(selectedDate, targetMealId)
      setPageFeedback({ tone: 'success', message: `${selectedFoodDetail.name} added to ${targetMealName}.` })
    } catch (error) {
      setPageFeedback({
        tone: 'error',
        message: getRequestErrorMessage(error, 'Unable to add this food to the selected meal.'),
      })
    } finally {
      setIsAddingItem(false)
    }
  }

  async function handleUpdateMealItem(item: UserMealItem, payload: UpdateMealItemRequest) {
    try {
      setUpdatingItemId(item.id)
      setPageFeedback(null)
      await updateMealItem(item.id, payload)
      await loadDailyMeals(selectedDate, targetMealId)
      setPageFeedback({ tone: 'success', message: 'Meal item updated.' })
      return true
    } catch (error) {
      setPageFeedback({
        tone: 'error',
        message: getRequestErrorMessage(error, 'Unable to update this meal item.'),
      })
      return false
    } finally {
      setUpdatingItemId(null)
    }
  }

  async function handleDeleteMealItem(item: UserMealItem) {
    const confirmed = window.confirm(`Delete "${item.foodNameSnapshot}" from this meal?`)
    if (!confirmed) {
      return
    }

    try {
      setDeletingItemId(item.id)
      setPageFeedback(null)
      await deleteMealItem(item.id)
      await loadDailyMeals(selectedDate, targetMealId)
      setPageFeedback({ tone: 'success', message: 'Meal item deleted.' })
    } catch (error) {
      setPageFeedback({
        tone: 'error',
        message: getRequestErrorMessage(error, 'Unable to delete this meal item.'),
      })
    } finally {
      setDeletingItemId(null)
    }
  }

  return (
    <main className="page-shell nutrition-shell">
      <section className="panel nutrition-toolbar-panel">
        <div className="nutrition-toolbar-copy">
          <span className="eyebrow">FORGE / Nutrition</span>
          <h1>Nutrition</h1>
          <p>
            Search USDA foods, load reliable detail before adding anything, and manage daily meals without changing the existing calorie log.
          </p>
        </div>

        <div className="nutrition-toolbar-controls">
          <label className="field nutrition-toolbar-date-field">
            <span>Date</span>
            <input
              type="date"
              value={selectedDate}
              onChange={(event) => setSelectedDate(event.target.value)}
            />
          </label>

          <div className="nutrition-toolbar-pills">
            <span className="info-pill">{mealCount} {mealCount === 1 ? 'meal' : 'meals'}</span>
            <span className="info-pill">{activeDailyMeals ? formatDate(activeDailyMeals.date) : formatDate(selectedDate)}</span>
            <span className="info-pill info-pill-strength">{targetMeal ? `Target: ${getMealDisplayName(targetMeal)}` : 'Select a target meal'}</span>
          </div>
        </div>
      </section>

      {pageFeedback ? (
        <div className={pageFeedback.tone === 'error' ? 'feedback error' : 'feedback success'}>
          {pageFeedback.message}
        </div>
      ) : null}

      <section className="nutrition-workspace">
        <div className="nutrition-main-column">
          <section className="panel nutrition-summary-panel">
            <div className="panel-header">
              <div>
                <h2>Daily summary</h2>
                <p>These totals come from saved meals only. Existing calorie tracking remains untouched until a later phase.</p>
              </div>
              {isRefreshingMeals ? <span className="record-hint">Refreshing day…</span> : null}
            </div>

            <div className="nutrition-summary-grid">
              <NutritionStatCard label="Calories" value={activeDailyMeals?.totalCalories ?? 0} unit="kcal" />
              <NutritionStatCard label="Protein" value={activeDailyMeals?.totalProtein ?? 0} unit="g" />
              <NutritionStatCard label="Carbs" value={activeDailyMeals?.totalCarbs ?? 0} unit="g" />
              <NutritionStatCard label="Fat" value={activeDailyMeals?.totalFat ?? 0} unit="g" />
              <NutritionStatCard label="Fiber" value={activeDailyMeals?.totalFiber ?? 0} unit="g" />
              <NutritionStatCard label="Sugar" value={activeDailyMeals?.totalSugar ?? 0} unit="g" />
            </div>
          </section>

          <section className="panel nutrition-create-meal-panel">
            <div className="panel-header">
              <div>
                <h2>Create meal</h2>
                <p>Add a meal for the selected date, then target it from the nutrition sidebar.</p>
              </div>
            </div>

            <form className="nutrition-create-meal-form" onSubmit={handleCreateMeal}>
              <label className="field">
                <span>Meal type</span>
                <select
                  value={createMealForm.mealType}
                  onChange={(event) =>
                    setCreateMealForm((current) => ({
                      ...current,
                      mealType: event.target.value as MealTypeValue,
                    }))
                  }
                >
                  {MEAL_TYPE_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="field">
                <span>Title</span>
                <input
                  type="text"
                  value={createMealForm.title}
                  maxLength={160}
                  onChange={(event) =>
                    setCreateMealForm((current) => ({
                      ...current,
                      title: event.target.value,
                    }))
                  }
                  placeholder="Optional title"
                />
              </label>

              <label className="field nutrition-create-meal-notes">
                <span>Notes</span>
                <textarea
                  value={createMealForm.notes}
                  maxLength={500}
                  onChange={(event) =>
                    setCreateMealForm((current) => ({
                      ...current,
                      notes: event.target.value,
                    }))
                  }
                  rows={2}
                  placeholder="Optional notes"
                />
              </label>

              <div className="action-row nutrition-create-meal-actions">
                <button type="submit" className="primary-button compact-button" disabled={isCreatingMeal}>
                  <Plus aria-hidden="true" focusable="false" strokeWidth={1.9} />
                  {isCreatingMeal ? 'Creating meal...' : 'Add meal'}
                </button>
              </div>
            </form>
          </section>

          {mealsErrorMessage ? (
            <StateCard title="Daily meals unavailable" description={mealsErrorMessage} tone="error" />
          ) : isLoadingMeals && !activeDailyMeals ? (
            <StateCard title="Loading meals" description="Pulling your saved meals for the selected day." loading />
          ) : mealCount === 0 ? (
            <section className="panel nutrition-empty-day-panel">
              <StateCard
                title="No meals for this day yet"
                description="Create your first meal for the selected date, then use the USDA search panel to add foods."
              />
            </section>
          ) : (
            <div className="nutrition-meal-groups">
              {groupedMeals.map((group) => (
                <section key={group.key} className="panel nutrition-meal-group">
                  <div className="panel-header">
                    <div>
                      <h2>{group.label}</h2>
                      <p>{group.description}</p>
                    </div>
                    <span className="info-pill">{group.meals.length}</span>
                  </div>

                  {group.meals.length === 0 ? (
                    <StateCard
                      title={`No ${group.label.toLowerCase()} meals`}
                      description="Create a meal in this group when you need a separate slot for food items."
                    />
                  ) : (
                    <div className="nutrition-meal-card-list">
                      {group.meals.map((meal) => (
                        <MealCard
                          key={meal.id}
                          meal={meal}
                          isTargetMeal={meal.id === targetMealId}
                          isSaving={savingMealId === meal.id}
                          isDeleting={deletingMealId === meal.id}
                          updatingItemId={updatingItemId}
                          deletingItemId={deletingItemId}
                          onSelectTarget={() => setTargetMealId(meal.id)}
                          onSaveMeal={handleSaveMeal}
                          onDeleteMeal={handleDeleteMeal}
                          onUpdateMealItem={handleUpdateMealItem}
                          onDeleteMealItem={handleDeleteMealItem}
                        />
                      ))}
                    </div>
                  )}
                </section>
              ))}
            </div>
          )}
        </div>

        <aside className="nutrition-side-column">
          <section className="panel nutrition-search-panel">
            <div className="panel-header">
              <div>
                <h2>Food search</h2>
                <p>Search USDA foods, then select a result to load usable gram-based nutrition details.</p>
              </div>
            </div>

            <div className="nutrition-search-controls">
              <label className="field nutrition-search-field">
                <span>Search foods</span>
                <div className="nutrition-search-input-shell">
                  <Search aria-hidden="true" focusable="false" strokeWidth={1.9} />
                  <input
                    type="search"
                    value={searchQuery}
                    onChange={(event) => setSearchQuery(event.target.value)}
                    placeholder="Chicken breast, oats, banana..."
                  />
                </div>
              </label>

              <label className="field nutrition-target-meal-field">
                <span>Target meal</span>
                <select
                  value={targetMealId ?? ''}
                  onChange={(event) => setTargetMealId(event.target.value ? Number(event.target.value) : null)}
                >
                  <option value="">Select a meal</option>
                  {(activeDailyMeals?.meals ?? []).map((meal) => (
                    <option key={meal.id} value={meal.id}>
                      {formatMealType(meal.mealType)} · {getMealDisplayName(meal)}
                    </option>
                  ))}
                </select>
                {selectedFoodSummary && !targetMealId ? (
                  <span className="field-error">Select a meal before adding a food item.</span>
                ) : null}
              </label>
            </div>

            <div className="nutrition-search-results">
              {searchErrorMessage ? (
                <StateCard title="Search unavailable" description={searchErrorMessage} tone="error" />
              ) : debouncedSearchQuery.length === 0 ? (
                <StateCard
                  title="Search USDA foods"
                  description="Type at least two characters to start searching and then load detail nutrition for a result."
                />
              ) : debouncedSearchQuery.length < SEARCH_MIN_CHARACTERS ? (
                <StateCard
                  title="Keep typing"
                  description={`Use at least ${SEARCH_MIN_CHARACTERS} characters before search starts.`}
                />
              ) : isSearching ? (
                <StateCard title="Searching foods" description="Looking up USDA foods for the current query." loading />
              ) : searchResults.length === 0 ? (
                <StateCard title="No foods found" description="Try a broader search term or another USDA phrasing." />
              ) : (
                <div className="nutrition-search-result-list">
                  {searchResults.map((food) => (
                    <SearchResultCard
                      key={buildFoodKey(food.source, food.externalId)}
                      food={food}
                      isSelected={buildFoodKey(food.source, food.externalId) === buildFoodKey(selectedFoodSummary?.source, selectedFoodSummary?.externalId)}
                      isLoading={selectedFoodLoading && buildFoodKey(food.source, food.externalId) === buildFoodKey(selectedFoodSummary?.source, selectedFoodSummary?.externalId)}
                      resolutionState={foodResolutionByKey[buildFoodKey(food.source, food.externalId)]}
                      onSelect={() => void handleSelectFood(food)}
                    />
                  ))}
                </div>
              )}
            </div>
          </section>

          <section className="panel nutrition-preview-panel">
            <div className="panel-header">
              <div>
                <h2>Selected food</h2>
                <p>Only detail nutrition from the backend can unlock the meal builder.</p>
              </div>
            </div>

            {selectedFoodError ? (
              <StateCard title="Food detail unavailable" description={selectedFoodError} tone="error" />
            ) : selectedFoodLoading ? (
              <StateCard title="Loading food detail" description="Fetching the normalized USDA detail for the selected result." loading />
            ) : !selectedFoodSummary ? (
              <StateCard
                title="No food selected"
                description="Choose a USDA result from the search panel to load calories, macros, and gram-based support."
              />
            ) : !selectedFoodDetail ? (
              <StateCard
                title="Food detail unavailable"
                description="This USDA result could not be prepared for preview. Try another result."
                tone="error"
              />
            ) : (
              <form className="nutrition-add-item-form" onSubmit={handleAddMealItem}>
                <div className={foodDetailStatus.canAdd ? 'nutrition-preview-card nutrition-preview-card-usable' : 'nutrition-preview-card nutrition-preview-card-unusable'}>
                  <div className="nutrition-preview-heading">
                    <div>
                      <span className="stat-label">USDA detail</span>
                      <strong>{selectedFoodDetail.name}</strong>
                      <p>{buildSelectedFoodDescription(selectedFoodDetail)}</p>
                    </div>
                    <span className="info-pill">{formatSourceLabel(selectedFoodDetail.source)}</span>
                  </div>

                  <div className="nutrition-preview-pills">
                    {selectedFoodDetail.foodCategory ? <span className="info-pill">{selectedFoodDetail.foodCategory}</span> : null}
                    {selectedFoodDetail.foodType ? <span className="info-pill">{selectedFoodDetail.foodType}</span> : null}
                    {hasSupportedGramUnit(selectedFoodDetail) ? <span className="info-pill info-pill-strength">Gram-ready</span> : <span className="info-pill">No gram support</span>}
                  </div>

                  <div className="nutrition-preview-grid">
                    <NutritionPreviewMetric label="Calories / 100g" value={selectedFoodDetail.caloriesPer100Grams} unit="kcal" />
                    <NutritionPreviewMetric label="Protein / 100g" value={selectedFoodDetail.proteinGramsPer100Grams} unit="g" />
                    <NutritionPreviewMetric label="Carbs / 100g" value={selectedFoodDetail.carbsGramsPer100Grams} unit="g" />
                    <NutritionPreviewMetric label="Fat / 100g" value={selectedFoodDetail.fatGramsPer100Grams} unit="g" />
                    <NutritionPreviewMetric label="Fiber / 100g" value={selectedFoodDetail.fiberGramsPer100Grams} unit="g" />
                    <NutritionPreviewMetric label="Sugar / 100g" value={selectedFoodDetail.sugarGramsPer100Grams} unit="g" />
                  </div>
                </div>

                <div className="nutrition-add-item-controls">
                  <label className="field">
                    <span>Quantity</span>
                    <input
                      type="number"
                      min="0.01"
                      step="0.01"
                      inputMode="decimal"
                      value={quantityInput}
                      onChange={(event) => setQuantityInput(event.target.value)}
                    />
                    {quantityError ? <span className="field-error">{quantityError}</span> : null}
                  </label>

                  <label className="field">
                    <span>Unit</span>
                    <select value={unit} onChange={(event) => setUnit(event.target.value)}>
                      <option value="g">Grams</option>
                    </select>
                  </label>
                </div>

                <div className="nutrition-calculation-preview">
                  <span className="stat-label">Preview for {quantityValue ? `${formatNutritionValue(quantityValue)} g` : 'selected amount'}</span>
                  <div className="nutrition-calculation-preview-grid">
                    <NutritionPreviewMetric label="Calories" value={selectedFoodPreview.calories} unit="kcal" />
                    <NutritionPreviewMetric label="Protein" value={selectedFoodPreview.protein} unit="g" />
                    <NutritionPreviewMetric label="Carbs" value={selectedFoodPreview.carbs} unit="g" />
                    <NutritionPreviewMetric label="Fat" value={selectedFoodPreview.fat} unit="g" />
                  </div>
                </div>

                {foodDetailStatus.message ? (
                  <div className="feedback error">{foodDetailStatus.message}</div>
                ) : null}

                {!foodDetailStatus.message && selectedFoodDetail.caloriesPer100Grams !== null && !hasPrimaryMacroData(selectedFoodDetail) ? (
                  <div className="nutrition-preview-note">
                    Only calorie data is available for this USDA item. Macro fields may stay unavailable.
                  </div>
                ) : null}

                {addFoodValidationMessage && addFoodValidationMessage !== foodDetailStatus.message ? (
                  <div className="feedback error">{addFoodValidationMessage}</div>
                ) : null}

                <div className="action-row action-row-inline">
                  <button type="submit" className="primary-button" disabled={!canAddFoodToMeal}>
                    <UtensilsCrossed aria-hidden="true" focusable="false" strokeWidth={1.9} />
                    {isAddingItem ? 'Adding food...' : `Add food to ${targetMeal ? getMealDisplayName(targetMeal) : 'selected meal'}`}
                  </button>
                </div>
              </form>
            )}
          </section>
        </aside>
      </section>
    </main>
  )
}

function SearchResultCard({
  food,
  isSelected,
  isLoading,
  resolutionState,
  onSelect,
}: {
  food: NutritionFoodSearchResult
  isSelected: boolean
  isLoading: boolean
  resolutionState?: FoodResolutionState
  onSelect: () => void
}) {
  const statusMessage = isLoading
    ? 'Loading nutrition details...'
    : resolutionState === 'usable'
      ? 'Nutrition details ready'
      : resolutionState === 'unusable'
        ? 'No usable gram-based nutrition'
        : resolutionState === 'error'
          ? 'Detail unavailable. Try another result.'
          : 'Select to load nutrition details'

  return (
    <button
      type="button"
      className={isSelected ? 'nutrition-search-result nutrition-search-result-active' : 'nutrition-search-result'}
      onClick={onSelect}
    >
      <div className="nutrition-search-result-header">
        <div className="nutrition-search-result-copy">
          <strong>{food.name}</strong>
          <p>{buildSearchResultDescription(food)}</p>
        </div>
        <span className="info-pill">{formatSourceLabel(food.source)}</span>
      </div>

      <div className="nutrition-search-result-footer">
        <span className={resolutionState === 'error' || resolutionState === 'unusable' ? 'nutrition-search-result-hint nutrition-search-result-hint-warning' : 'nutrition-search-result-hint'}>
          {statusMessage}
        </span>
        {food.caloriesPer100Grams !== null ? (
          <span className="info-pill">{formatNutritionValue(food.caloriesPer100Grams)} kcal / 100g</span>
        ) : null}
      </div>
    </button>
  )
}

function MealCard({
  meal,
  isTargetMeal,
  isSaving,
  isDeleting,
  updatingItemId,
  deletingItemId,
  onSelectTarget,
  onSaveMeal,
  onDeleteMeal,
  onUpdateMealItem,
  onDeleteMealItem,
}: {
  meal: UserMeal
  isTargetMeal: boolean
  isSaving: boolean
  isDeleting: boolean
  updatingItemId: number | null
  deletingItemId: number | null
  onSelectTarget: () => void
  onSaveMeal: (mealId: number, payload: UpdateMealRequest) => Promise<boolean>
  onDeleteMeal: (meal: UserMeal) => Promise<void>
  onUpdateMealItem: (item: UserMealItem, payload: UpdateMealItemRequest) => Promise<boolean>
  onDeleteMealItem: (item: UserMealItem) => Promise<void>
}) {
  const [isEditing, setIsEditing] = useState(false)
  const [titleInput, setTitleInput] = useState(meal.title ?? '')
  const [notesInput, setNotesInput] = useState(meal.notes ?? '')
  const [dateInput, setDateInput] = useState(meal.date)
  const [mealTypeInput, setMealTypeInput] = useState(normalizeMealTypeForInput(meal.mealType))
  const [quantityDrafts, setQuantityDrafts] = useState<Record<number, string>>(() => buildQuantityDrafts(meal.items))

  useEffect(() => {
    setTitleInput(meal.title ?? '')
    setNotesInput(meal.notes ?? '')
    setDateInput(meal.date)
    setMealTypeInput(normalizeMealTypeForInput(meal.mealType))
    setQuantityDrafts(buildQuantityDrafts(meal.items))
  }, [meal])

  async function handleMealSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const didSave = await onSaveMeal(meal.id, {
      date: dateInput,
      mealType: mealTypeInput,
      title: normalizeOptionalText(titleInput),
      notes: normalizeOptionalText(notesInput),
    })

    if (didSave) {
      setIsEditing(false)
    }
  }

  async function handleItemSave(item: UserMealItem) {
    const quantityValue = parsePositiveNumber(quantityDrafts[item.id] ?? item.quantity.toString())
    if (!quantityValue) {
      return
    }

    await onUpdateMealItem(item, {
      quantity: quantityValue,
      unit: GRAM_UNIT,
    })
  }

  return (
    <article className={isTargetMeal ? 'nutrition-meal-card nutrition-meal-card-target' : 'nutrition-meal-card'}>
      <div className="nutrition-meal-card-header">
        <div className="nutrition-meal-card-heading">
          <span className="stat-label">{formatMealType(meal.mealType)}</span>
          <h3>{getMealDisplayName(meal)}</h3>
          <p>{meal.notes ?? 'No notes added for this meal.'}</p>
        </div>

        <div className="nutrition-meal-card-actions">
          <button
            type="button"
            className={isTargetMeal ? 'primary-button compact-button' : 'ghost-button compact-button'}
            onClick={onSelectTarget}
            disabled={isDeleting}
          >
            {isTargetMeal ? 'Target meal' : 'Use as target'}
          </button>
          <button
            type="button"
            className="ghost-button compact-button"
            onClick={() => setIsEditing((current) => !current)}
            disabled={isDeleting}
          >
            {isEditing ? <X aria-hidden="true" focusable="false" strokeWidth={1.9} /> : <PencilLine aria-hidden="true" focusable="false" strokeWidth={1.9} />}
            {isEditing ? 'Close' : 'Edit'}
          </button>
          <button
            type="button"
            className="ghost-button compact-button subtle-danger-button"
            onClick={() => void onDeleteMeal(meal)}
            disabled={isDeleting}
          >
            <Trash2 aria-hidden="true" focusable="false" strokeWidth={1.9} />
            {isDeleting ? 'Deleting...' : 'Delete'}
          </button>
        </div>
      </div>

      <div className="nutrition-meal-card-meta">
        <span className="info-pill">{formatNutritionValue(meal.totalCalories)} kcal</span>
        <span className="info-pill">P {formatNutritionValue(meal.totalProtein)} g</span>
        <span className="info-pill">C {formatNutritionValue(meal.totalCarbs)} g</span>
        <span className="info-pill">F {formatNutritionValue(meal.totalFat)} g</span>
      </div>

      {isEditing ? (
        <form className="nutrition-meal-edit-form" onSubmit={handleMealSave}>
          <label className="field">
            <span>Meal date</span>
            <input type="date" value={dateInput} onChange={(event) => setDateInput(event.target.value)} />
          </label>

          <label className="field">
            <span>Meal type</span>
            <select value={mealTypeInput} onChange={(event) => setMealTypeInput(event.target.value)}>
              {MEAL_TYPE_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            <span>Title</span>
            <input
              type="text"
              maxLength={160}
              value={titleInput}
              onChange={(event) => setTitleInput(event.target.value)}
              placeholder="Optional title"
            />
          </label>

          <label className="field nutrition-meal-edit-notes">
            <span>Notes</span>
            <textarea
              rows={2}
              maxLength={500}
              value={notesInput}
              onChange={(event) => setNotesInput(event.target.value)}
              placeholder="Optional notes"
            />
          </label>

          <div className="action-row nutrition-meal-edit-actions">
            <button
              type="submit"
              className="primary-button compact-button"
              disabled={isSaving || !dateInput || !mealTypeInput.trim()}
            >
              <Save aria-hidden="true" focusable="false" strokeWidth={1.9} />
              {isSaving ? 'Saving...' : 'Save meal'}
            </button>
          </div>
        </form>
      ) : null}

      {meal.items.length === 0 ? (
        <div className="nutrition-empty-items">
          <strong>No foods added yet.</strong>
          <p>Use the USDA search panel to add gram-based items into this meal.</p>
        </div>
      ) : (
        <div className="nutrition-item-list">
          {meal.items.map((item) => {
            const quantityDraft = quantityDrafts[item.id] ?? item.quantity.toString()
            const itemQuantityValue = parsePositiveNumber(quantityDraft)
            const itemQuantityError =
              quantityDraft.trim().length > 0 && itemQuantityValue === null
                ? 'Enter a valid gram amount.'
                : null

            return (
              <div key={item.id} className="nutrition-item-card">
                <div className="nutrition-item-copy">
                  <div>
                    <strong>{item.foodNameSnapshot}</strong>
                    <p>{buildMealItemDescription(item)}</p>
                  </div>
                  <div className="nutrition-item-pills">
                    <span className="info-pill">{formatSourceLabel(item.sourceProvider)}</span>
                    <span className="info-pill">{formatNutritionValue(item.calories)} kcal</span>
                    <span className="info-pill">P {formatNutritionValue(item.protein)} g</span>
                    <span className="info-pill">C {formatNutritionValue(item.carbs)} g</span>
                    <span className="info-pill">F {formatNutritionValue(item.fat)} g</span>
                  </div>
                </div>

                <div className="nutrition-item-actions">
                  <label className="field nutrition-item-quantity-field">
                    <span>Quantity (g)</span>
                    <input
                      type="number"
                      min="0.01"
                      step="0.01"
                      inputMode="decimal"
                      value={quantityDraft}
                      onChange={(event) =>
                        setQuantityDrafts((current) => ({
                          ...current,
                          [item.id]: event.target.value,
                        }))
                      }
                    />
                    {itemQuantityError ? <span className="field-error">{itemQuantityError}</span> : null}
                  </label>

                  <div className="action-row nutrition-item-action-row">
                    <button
                      type="button"
                      className="ghost-button compact-button"
                      onClick={() => void handleItemSave(item)}
                      disabled={updatingItemId === item.id || Boolean(itemQuantityError) || !itemQuantityValue}
                    >
                      {updatingItemId === item.id ? 'Updating...' : 'Update'}
                    </button>
                    <button
                      type="button"
                      className="ghost-button compact-button subtle-danger-button"
                      onClick={() => void onDeleteMealItem(item)}
                      disabled={deletingItemId === item.id}
                    >
                      {deletingItemId === item.id ? 'Deleting...' : 'Delete'}
                    </button>
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      )}
    </article>
  )
}

function NutritionStatCard({
  label,
  value,
  unit,
}: {
  label: string
  value: number
  unit: string
}) {
  return (
    <article className="nutrition-stat-card">
      <span className="stat-label">{label}</span>
      <strong>{formatNutritionValue(value)}</strong>
      <p>{unit}</p>
    </article>
  )
}

function NutritionPreviewMetric({
  label,
  value,
  unit,
}: {
  label: string
  value: number | null
  unit: string
}) {
  return (
    <div className="nutrition-preview-metric">
      <span>{label}</span>
      <strong>{value === null ? 'Unavailable' : `${formatNutritionValue(value)} ${unit}`}</strong>
    </div>
  )
}

function buildMealGroups(meals: UserMeal[]): MealGroup[] {
  const grouped: MealGroup[] = MEAL_TYPE_OPTIONS.map((option) => ({
    key: option.value,
    label: option.label,
    description: `Meals tagged as ${option.label.toLowerCase()} for the selected day.`,
    meals: meals.filter((meal) => meal.mealType === option.value),
  }))

  const knownTypes = new Set(MEAL_TYPE_OPTIONS.map((option) => option.value))
  const uncategorizedMeals = meals.filter((meal) => !knownTypes.has(meal.mealType as MealTypeValue))

  if (uncategorizedMeals.length > 0) {
    grouped.push({
      key: 'other',
      label: 'Other',
      description: 'Meals with a custom type outside the standard day groups.',
      meals: uncategorizedMeals,
    })
  }

  return grouped
}

function buildFoodKey(source?: string | null, externalId?: string | null) {
  return `${source ?? ''}::${externalId ?? ''}`
}

function resolveTargetMealId(meals: UserMeal[], preferredMealId: number | null, currentMealId: number | null) {
  const availableMealIds = new Set(meals.map((meal) => meal.id))

  if (preferredMealId !== null && availableMealIds.has(preferredMealId)) {
    return preferredMealId
  }

  if (currentMealId !== null && availableMealIds.has(currentMealId)) {
    return currentMealId
  }

  return meals[0]?.id ?? null
}

function buildFoodPreview(food: NutritionFoodDetail | null, quantity: number | null) {
  return {
    calories: scaleFromHundredGrams(food?.caloriesPer100Grams, quantity),
    protein: scaleFromHundredGrams(food?.proteinGramsPer100Grams, quantity),
    carbs: scaleFromHundredGrams(food?.carbsGramsPer100Grams, quantity),
    fat: scaleFromHundredGrams(food?.fatGramsPer100Grams, quantity),
  }
}

function getFoodDetailStatus(food: NutritionFoodDetail | null): FoodDetailStatus {
  if (!food) {
    return {
      canAdd: false,
      hasCalories: false,
      message: null,
    }
  }

  if (!hasSupportedGramUnit(food)) {
    return {
      canAdd: false,
      hasCalories: false,
      message: 'This USDA item does not include usable gram-based nutrition. Please choose another result.',
    }
  }

  if (food.caloriesPer100Grams === null) {
    return {
      canAdd: false,
      hasCalories: false,
      message: 'This USDA item does not include usable calorie data. Please choose another result.',
    }
  }

  return {
    canAdd: true,
    hasCalories: true,
    message: null,
  }
}

function getAddFoodValidationMessage({
  selectedFoodSummary,
  selectedFoodDetail,
  selectedFoodError,
  selectedFoodLoading,
  targetMealId,
  quantityError,
  unit,
  foodDetailStatus,
}: {
  selectedFoodSummary: NutritionFoodSearchResult | null
  selectedFoodDetail: NutritionFoodDetail | null
  selectedFoodError: string | null
  selectedFoodLoading: boolean
  targetMealId: number | null
  quantityError: string | null
  unit: string
  foodDetailStatus: FoodDetailStatus
}) {
  if (!selectedFoodSummary) {
    return null
  }

  if (selectedFoodLoading) {
    return 'Nutrition detail is still loading for the selected USDA result.'
  }

  if (selectedFoodError) {
    return selectedFoodError
  }

  if (!selectedFoodDetail) {
    return 'Select a USDA result to load usable nutrition details.'
  }

  if (foodDetailStatus.message) {
    return foodDetailStatus.message
  }

  if (!targetMealId) {
    return 'Select a target meal before adding this food item.'
  }

  if (unit !== GRAM_UNIT) {
    return 'Only gram-based quantities are supported right now.'
  }

  if (quantityError) {
    return quantityError
  }

  return null
}

function buildFoodDetailErrorMessage(error: unknown) {
  if (isNotFoundError(error)) {
    return 'This USDA result could not be loaded. Try another result.'
  }

  return getRequestErrorMessage(error, 'Unable to load nutrition details for this USDA result.')
}

function buildSearchResultDescription(food: NutritionFoodSearchResult) {
  return food.brandName ?? food.foodCategory ?? food.dataType ?? 'Select to load nutrition details'
}

function buildSelectedFoodDescription(food: NutritionFoodDetail) {
  return food.brandName ?? food.foodCategory ?? food.dataType ?? 'USDA nutrition detail'
}

function buildMealItemDescription(item: UserMealItem) {
  return item.brandNameSnapshot
    ? `${item.brandNameSnapshot} · ${formatNutritionValue(item.quantity)} g`
    : `${formatNutritionValue(item.quantity)} g`
}

function hasSupportedGramUnit(food: NutritionFoodDetail) {
  return food.supportedUnits.some((unitValue) => ['g', 'gram', 'grams'].includes(unitValue.trim().toLowerCase()))
}

function hasPrimaryMacroData(food: NutritionFoodDetail) {
  return (
    food.proteinGramsPer100Grams !== null
    || food.carbsGramsPer100Grams !== null
    || food.fatGramsPer100Grams !== null
  )
}

function scaleFromHundredGrams(valuePer100Grams: number | null | undefined, quantity: number | null) {
  if (quantity === null || valuePer100Grams === null || valuePer100Grams === undefined) {
    return null
  }

  return roundToTwo((valuePer100Grams * quantity) / 100)
}

function buildQuantityDrafts(items: UserMealItem[]) {
  return Object.fromEntries(items.map((item) => [item.id, item.quantity.toString()]))
}

function parsePositiveNumber(value: string) {
  const normalizedValue = value.trim()
  if (!normalizedValue) {
    return null
  }

  const parsedValue = Number(normalizedValue)
  if (!Number.isFinite(parsedValue) || parsedValue <= 0) {
    return null
  }

  return parsedValue
}

function normalizeOptionalText(value: string) {
  const trimmedValue = value.trim()
  return trimmedValue.length > 0 ? trimmedValue : null
}

function normalizeMealTypeForInput(value: string) {
  const normalizedValue = value.trim().toLowerCase()
  return MEAL_TYPE_OPTIONS.some((option) => option.value === normalizedValue)
    ? normalizedValue
    : 'snack'
}

function getMealDisplayName(meal: UserMeal) {
  return meal.title?.trim() || formatMealType(meal.mealType)
}

function formatMealType(value: string) {
  const matchedOption = MEAL_TYPE_OPTIONS.find((option) => option.value === value)
  if (matchedOption) {
    return matchedOption.label
  }

  return value
    .split(/[\s_-]+/)
    .filter(Boolean)
    .map((part) => part[0]?.toUpperCase() + part.slice(1))
    .join(' ')
}

function formatSourceLabel(value: string) {
  return value.trim().toUpperCase()
}

function formatNutritionValue(value: number | null | undefined) {
  const normalizedValue = value ?? 0
  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 2,
  }).format(normalizedValue)
}

function roundToTwo(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100
}
