import { useDeferredValue, useEffect, useState, type FormEvent } from 'react'
import axios from 'axios'
import { Dumbbell, PlayCircle } from 'lucide-react'
import { generateAiWorkout } from '../api/aiWorkoutApi'
import { searchExerciseCatalog } from '../api/exerciseCatalog'
import { apiClient, getRequestErrorMessage } from '../lib/http'
import { StateCard } from '../components/StateCard'
import { VideoModal } from '../components/VideoModal'
import type { AiWorkoutExercise, AiWorkoutGeneratePayload, AiWorkoutPlan } from '../types/aiWorkout'
import type { ExerciseCatalogItem } from '../types/exerciseCatalog'

type GoalOption = 'strength' | 'muscle-gain' | 'fat-loss' | 'general-fitness' | 'endurance' | 'custom'
type WorkoutTypeOption = 'auto' | 'full-body' | 'upper' | 'lower' | 'push' | 'pull' | 'core'
type FitnessLevelOption = 'auto' | 'beginner' | 'intermediate' | 'advanced'
type TargetMuscleValue =
  | 'chest'
  | 'back'
  | 'shoulders'
  | 'biceps'
  | 'triceps'
  | 'forearms'
  | 'abs'
  | 'core'
  | 'glutes'
  | 'quadriceps'
  | 'hamstrings'
  | 'calves'

type GeneratorFormState = {
  goal: GoalOption
  customGoal: string
  preferredWorkoutType: WorkoutTypeOption
  durationMinutes: string
  fitnessLevel: FitnessLevelOption
  targetMuscles: TargetMuscleValue[]
  excludedExercises: ExerciseCatalogItem[]
  includeWarmup: boolean
  includeCooldown: boolean
}

type TargetMuscleOption = {
  label: string
  value: TargetMuscleValue
}

type TargetMusclePreset = {
  label: string
  muscles: TargetMuscleValue[]
}

const targetMuscleOptions: TargetMuscleOption[] = [
  { label: 'Chest', value: 'chest' },
  { label: 'Back', value: 'back' },
  { label: 'Shoulders', value: 'shoulders' },
  { label: 'Biceps', value: 'biceps' },
  { label: 'Triceps', value: 'triceps' },
  { label: 'Forearms', value: 'forearms' },
  { label: 'Abs', value: 'abs' },
  { label: 'Core', value: 'core' },
  { label: 'Glutes', value: 'glutes' },
  { label: 'Quadriceps', value: 'quadriceps' },
  { label: 'Hamstrings', value: 'hamstrings' },
  { label: 'Calves', value: 'calves' },
]

const targetMusclePresets: TargetMusclePreset[] = [
  { label: 'Full Body', muscles: ['chest', 'back', 'shoulders', 'biceps', 'triceps', 'glutes', 'quadriceps', 'hamstrings', 'calves', 'core'] },
  { label: 'Push', muscles: ['chest', 'shoulders', 'triceps'] },
  { label: 'Pull', muscles: ['back', 'biceps', 'forearms'] },
  { label: 'Upper Body', muscles: ['chest', 'back', 'shoulders', 'biceps', 'triceps', 'forearms'] },
  { label: 'Lower Body', muscles: ['glutes', 'quadriceps', 'hamstrings', 'calves'] },
]

const targetMuscleLabelMap = new Map(targetMuscleOptions.map((option) => [option.value, option.label]))

const initialFormState: GeneratorFormState = {
  goal: 'general-fitness',
  customGoal: '',
  preferredWorkoutType: 'auto',
  durationMinutes: '45',
  fitnessLevel: 'auto',
  targetMuscles: [],
  excludedExercises: [],
  includeWarmup: true,
  includeCooldown: true,
}

const excludedExerciseSearchCache = new Map<string, ExerciseCatalogItem[]>()

export function AiWorkoutGeneratorPage() {
  const [form, setForm] = useState<GeneratorFormState>(initialFormState)
  const [plan, setPlan] = useState<AiWorkoutPlan | null>(null)
  const [isGenerating, setIsGenerating] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [videoTarget, setVideoTarget] = useState<{ title: string; url: string } | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const payload = buildPayload(form)
    if (!payload.goal.trim()) {
      setErrorMessage('Choose a goal or enter a custom goal before generating a plan.')
      return
    }

    try {
      setIsGenerating(true)
      setErrorMessage(null)
      const nextPlan = await generateAiWorkout(payload)
      setPlan(nextPlan)
    } catch (error) {
      console.error('AI workout generator request failed.', {
        error,
        isAxiosError: axios.isAxiosError(error),
        status: axios.isAxiosError(error) ? error.response?.status : undefined,
        data: axios.isAxiosError(error) ? error.response?.data : undefined,
      })
      setErrorMessage(getAiWorkoutGenerateErrorMessage(error))
    } finally {
      setIsGenerating(false)
    }
  }

  function toggleTargetMuscle(value: TargetMuscleValue) {
    setForm((current) => ({
      ...current,
      targetMuscles: current.targetMuscles.includes(value)
        ? current.targetMuscles.filter((entry) => entry !== value)
        : orderTargetMuscles([...current.targetMuscles, value]),
    }))
  }

  function toggleTargetMusclePreset(preset: TargetMusclePreset) {
    setForm((current) => {
      const isSelected = preset.muscles.every((muscle) => current.targetMuscles.includes(muscle))
      const nextValues = isSelected
        ? current.targetMuscles.filter((muscle) => !preset.muscles.includes(muscle))
        : Array.from(new Set([...current.targetMuscles, ...preset.muscles]))

      return {
        ...current,
        targetMuscles: orderTargetMuscles(nextValues),
      }
    })
  }

  return (
    <main className="page-shell ai-workout-shell">
      <section className="hero-panel ai-workout-hero">
        <div className="ai-workout-hero-copy">
          <span className="eyebrow">FORGE / Generator</span>
          <h1>AI Workout Generator</h1>
          <p className="hero-text">
            Build a structured workout from your local goals, recent training history, and the current exercise catalog.
            This MVP stays deterministic and read-only, so nothing is saved until you choose to log a session yourself.
          </p>
        </div>

        <div className="ai-workout-hero-side">
          <article className="forge-focus-card">
            <span className="stat-label">Generator mode</span>
            <strong>Rules-first MVP</strong>
            <p>
              Uses your catalog coverage, recent workouts, and goal context now, while keeping the service shape ready for a
              future LLM provider.
            </p>
            <div className="forge-focus-pills">
              <span className="info-pill">{plan ? plan.workoutType : 'Read-only plan'}</span>
              <span className="info-pill info-pill-strength">
                {plan ? `${plan.estimatedDurationMinutes} min` : 'No auto-save'}
              </span>
            </div>
          </article>
        </div>
      </section>

      <section className="ai-workout-grid">
        <section className="panel ai-workout-panel">
          <div className="panel-header">
            <div>
              <h2>Generate a plan</h2>
              <p>Choose the goal, split, duration, and any constraints you want the generator to honor.</p>
            </div>
          </div>

          <form className="ai-workout-form" onSubmit={handleSubmit}>
            <div className="ai-workout-form-grid">
              <label className="field">
                <span>Goal</span>
                <select
                  className="select-input"
                  value={form.goal}
                  onChange={(event) => setForm((current) => ({ ...current, goal: event.target.value as GoalOption }))}
                >
                  <option value="strength">Strength</option>
                  <option value="muscle-gain">Muscle gain</option>
                  <option value="fat-loss">Fat loss</option>
                  <option value="general-fitness">General fitness</option>
                  <option value="endurance">Endurance</option>
                  <option value="custom">Custom</option>
                </select>
              </label>

              <label className="field">
                <span>Preferred workout type</span>
                <select
                  className="select-input"
                  value={form.preferredWorkoutType}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, preferredWorkoutType: event.target.value as WorkoutTypeOption }))
                  }
                >
                  <option value="auto">Auto</option>
                  <option value="full-body">Full body</option>
                  <option value="upper">Upper body</option>
                  <option value="lower">Lower body</option>
                  <option value="push">Push</option>
                  <option value="pull">Pull</option>
                  <option value="core">Core</option>
                </select>
              </label>

              {form.goal === 'custom' ? (
                <label className="field field-span-2">
                  <span>Custom goal</span>
                  <input
                    type="text"
                    placeholder="Athletic performance"
                    value={form.customGoal}
                    onChange={(event) => setForm((current) => ({ ...current, customGoal: event.target.value }))}
                  />
                  <small>Use a short phrase. The backend will still normalize this to the closest safe training intent.</small>
                </label>
              ) : null}

              <label className="field">
                <span>Duration minutes</span>
                <input
                  type="number"
                  min="15"
                  max="180"
                  step="5"
                  value={form.durationMinutes}
                  onChange={(event) => setForm((current) => ({ ...current, durationMinutes: event.target.value }))}
                />
              </label>

              <label className="field">
                <span>Fitness level</span>
                <select
                  className="select-input"
                  value={form.fitnessLevel}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, fitnessLevel: event.target.value as FitnessLevelOption }))
                  }
                >
                  <option value="auto">Auto</option>
                  <option value="beginner">Beginner</option>
                  <option value="intermediate">Intermediate</option>
                  <option value="advanced">Advanced</option>
                </select>
              </label>

              <div className="field field-span-2 ai-workout-target-muscles-field">
                <span>Target muscles</span>
                <div className="ai-workout-target-muscle-presets" role="group" aria-label="Target muscle presets">
                  {targetMusclePresets.map((preset) => {
                    const isActive = preset.muscles.every((muscle) => form.targetMuscles.includes(muscle))

                    return (
                      <button
                        key={preset.label}
                        type="button"
                        className={`ai-workout-target-chip ai-workout-target-chip-preset${isActive ? ' ai-workout-target-chip-active' : ''}`}
                        aria-pressed={isActive}
                        onClick={() => toggleTargetMusclePreset(preset)}
                      >
                        {preset.label}
                      </button>
                    )
                  })}
                </div>

                <div className="ai-workout-target-muscle-grid" role="group" aria-label="Target muscles">
                  {targetMuscleOptions.map((option) => {
                    const isActive = form.targetMuscles.includes(option.value)

                    return (
                      <button
                        key={option.value}
                        type="button"
                        className={`ai-workout-target-chip${isActive ? ' ai-workout-target-chip-active' : ''}`}
                        aria-pressed={isActive}
                        onClick={() => toggleTargetMuscle(option.value)}
                      >
                        {option.label}
                      </button>
                    )
                  })}
                </div>

                {form.targetMuscles.length > 0 ? (
                  <div className="ai-workout-selected-targets">
                    <div className="ai-workout-selected-targets-header">
                      <small>Selected muscles</small>
                      <button
                        type="button"
                        className="ghost-button compact-button ai-workout-target-clear-button"
                        onClick={() => setForm((current) => ({ ...current, targetMuscles: [] }))}
                      >
                        Clear
                      </button>
                    </div>
                    <div className="ai-workout-selected-target-chips">
                      {form.targetMuscles.map((value) => (
                        <button
                          key={value}
                          type="button"
                          className="ai-workout-target-chip ai-workout-target-chip-active"
                          onClick={() => toggleTargetMuscle(value)}
                        >
                          {targetMuscleLabelMap.get(value) ?? value}
                        </button>
                      ))}
                    </div>
                  </div>
                ) : (
                  <small>Pick one or more muscles, or leave everything unselected to let the workout type drive the session.</small>
                )}
              </div>

              <ExcludedExercisePickerField
                selectedItems={form.excludedExercises}
                onChange={(selectedItems) => setForm((current) => ({ ...current, excludedExercises: selectedItems }))}
              />
            </div>

            <div className="ai-workout-toggle-grid">
              <label className="field-checkbox">
                <input
                  type="checkbox"
                  checked={form.includeWarmup}
                  onChange={(event) => setForm((current) => ({ ...current, includeWarmup: event.target.checked }))}
                />
                <span>Include warm-up</span>
              </label>

              <label className="field-checkbox">
                <input
                  type="checkbox"
                  checked={form.includeCooldown}
                  onChange={(event) => setForm((current) => ({ ...current, includeCooldown: event.target.checked }))}
                />
                <span>Include cooldown</span>
              </label>
            </div>

            {errorMessage ? <p className="field-error">{errorMessage}</p> : null}

            <div className="ai-workout-form-actions">
              <button type="submit" className="primary-button" disabled={isGenerating}>
                {isGenerating ? 'Generating...' : 'Generate workout'}
              </button>
              <span className="record-hint">Plans are preview-only in this MVP and never auto-create workout records.</span>
            </div>
          </form>
        </section>

        <section className="panel ai-workout-panel">
          <div className="panel-header">
            <div>
              <h2>Generated plan</h2>
              <p>Review the structure, prescription, and catalog media before you take anything into a live session.</p>
            </div>
          </div>

          {isGenerating ? (
            <StateCard title="Generating workout" description="Balancing goals, catalog coverage, and recent training history." loading />
          ) : !plan ? (
            <StateCard
              title="No plan yet"
              description="Choose your constraints on the left, then generate a workout to see the recommended sections and exercises."
            />
          ) : (
            <div className="ai-workout-plan">
              <article className="ai-workout-plan-summary">
                <div>
                  <span className="stat-label">Workout blueprint</span>
                  <h3>{plan.title}</h3>
                  <p>
                    {plan.goal} focus · {plan.workoutType} split · {plan.difficulty} difficulty
                  </p>
                </div>
                <div className="ai-workout-summary-pills">
                  <span className="info-pill">{plan.estimatedDurationMinutes} min</span>
                  <span className="info-pill info-pill-strength">{plan.sections.length} sections</span>
                </div>
              </article>

              <div className="ai-workout-section-list">
                {plan.sections.map((section) => (
                  <article key={section.name} className="ai-workout-section-card">
                    <div className="ai-workout-section-header">
                      <h3>{section.name}</h3>
                      <span className="record-hint">{section.exercises.length} exercises</span>
                    </div>

                    <div className="ai-workout-exercise-list">
                      {section.exercises.map((exercise) => (
                        <article key={`${section.name}-${exercise.name}`} className="ai-workout-exercise-card">
                          <PlanExerciseMedia exercise={exercise} />

                          <div className="ai-workout-exercise-copy">
                            <div className="ai-workout-exercise-header">
                              <strong>{exercise.name}</strong>
                              <div className="ai-workout-exercise-pills">
                                {exercise.category ? <span className="info-pill">{exercise.category}</span> : null}
                                {exercise.targetMuscle ? <span className="info-pill">{exercise.targetMuscle}</span> : null}
                              </div>
                            </div>

                            <div className="ai-workout-prescription-grid">
                              <span><strong>{exercise.sets}</strong> sets</span>
                              <span><strong>{exercise.reps}</strong></span>
                              <span><strong>{exercise.restSeconds}s</strong> rest</span>
                            </div>

                            {exercise.suggestedWeight ? (
                              <p className="ai-workout-prescription-note">{exercise.suggestedWeight}</p>
                            ) : null}

                            <p className="ai-workout-exercise-instructions">{exercise.instructions}</p>

                            {exercise.videoUrl ? (
                              <button
                                type="button"
                                className="ghost-button compact-button ai-workout-video-button"
                                onClick={() => setVideoTarget({ title: exercise.name, url: exercise.videoUrl! })}
                              >
                                <PlayCircle aria-hidden="true" focusable="false" strokeWidth={1.9} />
                                Watch demo
                              </button>
                            ) : null}
                          </div>
                        </article>
                      ))}
                    </div>
                  </article>
                ))}
              </div>

              {plan.notes.length > 0 ? (
                <article className="ai-workout-notes-card">
                  <span className="stat-label">Notes</span>
                  <ul className="ai-workout-note-list">
                    {plan.notes.map((note) => (
                      <li key={note}>{note}</li>
                    ))}
                  </ul>
                </article>
              ) : null}
            </div>
          )}
        </section>
      </section>

      {videoTarget ? (
        <VideoModal title={videoTarget.title} videoUrl={videoTarget.url} onClose={() => setVideoTarget(null)} />
      ) : null}
    </main>
  )
}

function buildPayload(form: GeneratorFormState): AiWorkoutGeneratePayload {
  return {
    goal: form.goal === 'custom' ? form.customGoal.trim() : mapGoalOptionToValue(form.goal),
    preferredWorkoutType: form.preferredWorkoutType === 'auto' ? null : form.preferredWorkoutType,
    durationMinutes: form.durationMinutes.trim() ? Number(form.durationMinutes) : null,
    fitnessLevel: form.fitnessLevel === 'auto' ? null : form.fitnessLevel,
    targetMuscles: form.targetMuscles.length > 0 ? form.targetMuscles : null,
    excludedExercises: form.excludedExercises.length > 0 ? form.excludedExercises.map((item) => item.name.trim()) : null,
    includeWarmup: form.includeWarmup,
    includeCooldown: form.includeCooldown,
  }
}

function mapGoalOptionToValue(goal: Exclude<GoalOption, 'custom'>) {
  switch (goal) {
    case 'strength':
      return 'strength'
    case 'muscle-gain':
      return 'muscle gain'
    case 'fat-loss':
      return 'fat loss'
    case 'general-fitness':
      return 'general fitness'
    case 'endurance':
      return 'endurance'
  }
}

function orderTargetMuscles(values: TargetMuscleValue[]) {
  return targetMuscleOptions
    .map((option) => option.value)
    .filter((value) => values.includes(value))
}

function ExcludedExercisePickerField({
  selectedItems,
  onChange,
}: {
  selectedItems: ExerciseCatalogItem[]
  onChange: (nextItems: ExerciseCatalogItem[]) => void
}) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<ExerciseCatalogItem[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isOpen, setIsOpen] = useState(false)
  const [searchErrorMessage, setSearchErrorMessage] = useState<string | null>(null)
  const deferredQuery = useDeferredValue(query)
  const normalizedQuery = deferredQuery.trim().toLowerCase()

  useEffect(() => {
    let isCancelled = false

    if (!isOpen) {
      setResults([])
      setIsLoading(false)
      setSearchErrorMessage(null)
      return
    }

    if (normalizedQuery.length < 2) {
      setResults([])
      setIsLoading(false)
      setSearchErrorMessage(null)
      return
    }

    const cachedResults = excludedExerciseSearchCache.get(normalizedQuery)
    if (cachedResults) {
      setResults(filterExcludedExerciseResults(cachedResults, selectedItems))
      setIsLoading(false)
      setSearchErrorMessage(null)
      return
    }

    setIsLoading(true)
    setSearchErrorMessage(null)

    const timeoutId = window.setTimeout(() => {
      void searchExerciseCatalog(deferredQuery.trim())
        .then((nextResults) => {
          if (isCancelled) {
            return
          }

          excludedExerciseSearchCache.set(normalizedQuery, nextResults)
          setResults(filterExcludedExerciseResults(nextResults, selectedItems))
        })
        .catch((error) => {
          if (isCancelled) {
            return
          }

          setResults([])
          setSearchErrorMessage(getRequestErrorMessage(error, 'Unable to search the exercise catalog right now.'))
        })
        .finally(() => {
          if (!isCancelled) {
            setIsLoading(false)
          }
        })
    }, 250)

    return () => {
      isCancelled = true
      window.clearTimeout(timeoutId)
    }
  }, [deferredQuery, isOpen, normalizedQuery, selectedItems])

  useEffect(() => {
    if (normalizedQuery.length < 2) {
      return
    }

    setResults((current) => filterExcludedExerciseResults(current, selectedItems))
  }, [normalizedQuery, selectedItems])

  function addItem(item: ExerciseCatalogItem) {
    if (selectedItems.some((selectedItem) => selectedItem.id === item.id)) {
      setQuery('')
      setIsOpen(false)
      return
    }

    onChange([...selectedItems, item])
    setQuery('')
    setResults([])
    setIsOpen(false)
  }

  function removeItem(itemId: number) {
    onChange(selectedItems.filter((item) => item.id !== itemId))
  }

  return (
    <div className="field field-span-2 catalog-picker-field ai-workout-exclusion-picker">
      <span>Excluded exercises</span>
      <input
        type="text"
        placeholder="Search catalog exercises to exclude"
        value={query}
        onChange={(event) => {
          setQuery(event.target.value)
          setIsOpen(true)
        }}
        onFocus={() => setIsOpen(true)}
        onBlur={() => window.setTimeout(() => setIsOpen(false), 120)}
        aria-expanded={isOpen}
        aria-autocomplete="list"
      />
      <small>Search the existing exercise catalog, then add any movements you want the generator to avoid.</small>

      {selectedItems.length > 0 ? (
        <div className="ai-workout-selected-targets">
          <div className="ai-workout-selected-targets-header">
            <small>Excluded exercises</small>
            <button
              type="button"
              className="ghost-button compact-button ai-workout-target-clear-button"
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => onChange([])}
            >
              Clear
            </button>
          </div>
          <div className="ai-workout-exclusion-chip-list">
            {selectedItems.map((item) => (
              <button
                key={item.id}
                type="button"
                className="ai-workout-target-chip ai-workout-target-chip-active ai-workout-exclusion-chip"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => removeItem(item.id)}
              >
                <span>{item.name}</span>
                <span aria-hidden="true">×</span>
              </button>
            ))}
          </div>
        </div>
      ) : null}

      {isOpen ? (
        <div className="catalog-suggestions-panel ai-workout-exclusion-suggestions" role="listbox" aria-label="Excluded exercise suggestions">
          {normalizedQuery.length < 2 ? <p className="catalog-suggestion-status">Type at least 2 characters to search the catalog.</p> : null}
          {normalizedQuery.length >= 2 && isLoading ? <p className="catalog-suggestion-status">Searching catalog...</p> : null}
          {normalizedQuery.length >= 2 && !isLoading && searchErrorMessage ? (
            <p className="catalog-suggestion-status field-error">{searchErrorMessage}</p>
          ) : null}
          {normalizedQuery.length >= 2 && !isLoading && !searchErrorMessage && results.length === 0 ? (
            <p className="catalog-suggestion-status">No matching catalog exercises found for this search.</p>
          ) : null}
          {normalizedQuery.length >= 2 && !isLoading && !searchErrorMessage && results.length > 0 ? (
            <div className="catalog-suggestion-list">
              {results.slice(0, 8).map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className="catalog-suggestion-item"
                  onMouseDown={(event) => event.preventDefault()}
                  onClick={() => addItem(item)}
                >
                  <span className="catalog-suggestion-thumb catalog-selected-thumb-placeholder" aria-hidden="true">
                    {item.name.slice(0, 1)}
                  </span>
                  <span className="catalog-suggestion-copy">
                    <strong>{item.name}</strong>
                    <span>
                      {item.primaryMuscle ?? 'No muscle tag'}
                      {item.equipment ? ` · ${item.equipment}` : ''}
                    </span>
                  </span>
                </button>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

function filterExcludedExerciseResults(
  items: ExerciseCatalogItem[],
  selectedItems: ExerciseCatalogItem[],
) {
  const selectedIds = new Set(selectedItems.map((item) => item.id))
  return items.filter((item) => !selectedIds.has(item.id))
}

function PlanExerciseMedia({ exercise }: { exercise: AiWorkoutExercise }) {
  const mediaUrl = resolvePlanMediaUrl(exercise.thumbnailUrl)
  const [hasError, setHasError] = useState(false)

  useEffect(() => {
    setHasError(false)
  }, [mediaUrl])

  if (!mediaUrl || hasError) {
    return (
      <div className="ai-workout-exercise-media ai-workout-exercise-media-placeholder" aria-hidden="true">
        <span className="exercise-library-item-media-fallback-icon">
          <Dumbbell aria-hidden="true" focusable="false" strokeWidth={1.8} />
        </span>
        <span className="record-hint">No media</span>
      </div>
    )
  }

  return (
    <div className="ai-workout-exercise-media">
      <img
        src={mediaUrl}
        alt={exercise.name}
        loading="lazy"
        decoding="async"
        referrerPolicy="no-referrer"
        onError={(event) => {
          event.currentTarget.style.display = 'none'
          setHasError(true)
        }}
      />
    </div>
  )
}

function resolvePlanMediaUrl(value: string | null | undefined) {
  const normalizedValue = value?.trim()
  if (!normalizedValue) {
    return null
  }

  if (normalizedValue.startsWith('data:image/')) {
    return normalizedValue
  }

  const mediaOrigin = resolvePlanMediaOrigin()

  try {
    const absoluteUrl = normalizedValue.startsWith('//')
      ? new URL(`${window.location.protocol}${normalizedValue}`)
      : new URL(normalizedValue, `${mediaOrigin}/`)

    if (!['http:', 'https:', 'data:', 'blob:'].includes(absoluteUrl.protocol)) {
      return null
    }

    return absoluteUrl.toString()
  } catch {
    return null
  }
}

function resolvePlanMediaOrigin() {
  if (typeof window === 'undefined') {
    return 'http://localhost'
  }

  const configuredApiBaseUrl = typeof apiClient.defaults.baseURL === 'string'
    ? apiClient.defaults.baseURL
    : '/api'

  try {
    return new URL(configuredApiBaseUrl, window.location.origin).origin
  } catch {
    return window.location.origin
  }
}

function getAiWorkoutGenerateErrorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 404) {
      return 'The AI workout generator API is not available on the current backend deployment. Redeploy the backend so POST /api/ai-workout/generate exists.'
    }

    if (error.response?.status === 401) {
      return 'Your session has expired. Sign in again and retry the workout generator.'
    }

    if (error.response?.status === 403) {
      return 'Your account is not allowed to use the AI workout generator.'
    }
  }

  return getRequestErrorMessage(error, 'Unable to generate an AI workout plan right now.')
}
