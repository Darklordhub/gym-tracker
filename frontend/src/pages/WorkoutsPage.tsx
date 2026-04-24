import { useDeferredValue, useEffect, useMemo, useState } from 'react'
import { searchExerciseCatalog } from '../api/exerciseCatalog'
import { fetchCycleGuidance } from '../api/cycle'
import { fetchLatestCalorieLog } from '../api/calories'
import { fetchGoals } from '../api/goals'
import { fetchProgressiveOverloadRecommendation } from '../api/progressiveOverload'
import { fetchLatestReadinessLog } from '../api/readiness'
import {
  completeActiveWorkoutSession,
  createWorkout,
  createWorkoutTemplate,
  deleteWorkoutTemplate,
  deleteWorkout,
  fetchActiveWorkoutSession,
  fetchWorkoutTemplates,
  fetchWorkouts,
  startActiveWorkoutSession,
  updateActiveWorkoutSession,
} from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { getWorkoutAssistantInsight, getSuggestedNextWeight } from '../lib/exerciseSuggestions'
import { buildDailyCalorieBalance } from '../lib/calorieBalance'
import { formatDate, getTodayDateValue } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import { buildDailyTrainingScore } from '../lib/trainingScore'
import type { CycleGuidance } from '../types/cycle'
import type { CalorieLog } from '../types/calories'
import type { GoalSettings } from '../types/goals'
import type { ExerciseCatalogItem } from '../types/exerciseCatalog'
import type { ProgressiveOverloadRecommendation } from '../types/progressiveOverload'
import type { ReadinessLog } from '../types/readiness'
import type {
  ActiveWorkoutSession,
  CardioActivityType,
  CardioIntensity,
  ExerciseEntryPayload,
  Workout,
  WorkoutTemplate,
} from '../types/workout'

type ExerciseSetFormState = {
  reps: string
  weightKg: string
}

type ExerciseFormState = {
  exerciseName: string
  catalogItem: ExerciseCatalogItem | null
  sets: ExerciseSetFormState[]
}

type WorkoutFormState = {
  date: string
  notes: string
  exerciseEntries: ExerciseFormState[]
}

type ExerciseSetFieldErrors = Partial<Record<keyof ExerciseSetFormState, string>>

type ExerciseFieldErrors = {
  exerciseName?: string
  sets?: string
  setErrors: ExerciseSetFieldErrors[]
}

type WorkoutFormErrors = {
  date?: string
  notes?: string
  exerciseEntries?: string
  exercises: ExerciseFieldErrors[]
}

type TemplateFormErrors = {
  name?: string
}

type OverloadRecommendationState = {
  status: 'loading' | 'loaded' | 'error'
  recommendation: ProgressiveOverloadRecommendation | null
  errorMessage?: string
}

type CardioFormState = {
  date: string
  cardioActivityType: CardioActivityType
  cardioDurationMinutes: string
  cardioDistanceKm: string
  cardioIntensity: CardioIntensity
  notes: string
}

type CardioFormErrors = {
  date?: string
  cardioActivityType?: string
  cardioDurationMinutes?: string
  cardioDistanceKm?: string
  cardioIntensity?: string
  notes?: string
}

const createSetForm = (): ExerciseSetFormState => ({
  reps: '',
  weightKg: '',
})

const createExerciseForm = (): ExerciseFormState => ({
  exerciseName: '',
  catalogItem: null,
  sets: [createSetForm()],
})

const initialQuickLogFormState = (): WorkoutFormState => ({
  date: getTodayDateValue(),
  notes: '',
  exerciseEntries: [createExerciseForm()],
})

const initialActiveFormState = (): WorkoutFormState => ({
  date: getTodayDateValue(),
  notes: '',
  exerciseEntries: [],
})

const initialCardioFormState = (): CardioFormState => ({
  date: getTodayDateValue(),
  cardioActivityType: 'walking',
  cardioDurationMinutes: '',
  cardioDistanceKm: '',
  cardioIntensity: 'low',
  notes: '',
})

function validateWorkoutForm(
  form: WorkoutFormState,
  options?: { requireDate?: boolean; requireExercises?: boolean },
): WorkoutFormErrors {
  const requireDate = options?.requireDate ?? true
  const requireExercises = options?.requireExercises ?? true

  const exercises = form.exerciseEntries.map<ExerciseFieldErrors>((exercise) => {
    const currentErrors: ExerciseFieldErrors = { setErrors: [] }

    if (!exercise.exerciseName.trim()) {
      currentErrors.exerciseName = 'Exercise name is required.'
    }

    if (exercise.sets.length === 0) {
      currentErrors.sets = 'Add at least one set.'
    }

    currentErrors.setErrors = exercise.sets.map<ExerciseSetFieldErrors>((set) => {
      const setErrors: ExerciseSetFieldErrors = {}

      const reps = Number(set.reps)
      if (!set.reps.trim()) {
        setErrors.reps = 'Reps are required.'
      } else if (Number.isNaN(reps) || reps < 1 || reps > 100) {
        setErrors.reps = 'Reps must be between 1 and 100.'
      }

      const weightKg = Number(set.weightKg)
      if (!set.weightKg.trim()) {
        setErrors.weightKg = 'Weight is required.'
      } else if (Number.isNaN(weightKg) || weightKg < 0 || weightKg > 500) {
        setErrors.weightKg = 'Weight must be between 0 and 500 kg.'
      }

      return setErrors
    })

    return currentErrors
  })

  const errors: WorkoutFormErrors = { exercises }

  if (requireDate) {
    if (!form.date) {
      errors.date = 'Date is required.'
    } else if (form.date > getTodayDateValue()) {
      errors.date = 'Date cannot be in the future.'
    }
  }

  if (form.notes.length > 500) {
    errors.notes = 'Notes must be 500 characters or less.'
  }

  if (requireExercises && form.exerciseEntries.length === 0) {
    errors.exerciseEntries = 'Add at least one exercise.'
  }

  return errors
}

function hasWorkoutErrors(errors: WorkoutFormErrors) {
  return Boolean(
    errors.date ||
      errors.notes ||
      errors.exerciseEntries ||
      errors.exercises.some(
        (exercise) =>
          Boolean(exercise.exerciseName || exercise.sets) ||
          exercise.setErrors.some((setErrors) => Object.keys(setErrors).length > 0),
      ),
  )
}

function toExercisePayload(exerciseEntries: ExerciseFormState[]): ExerciseEntryPayload[] {
  return exerciseEntries.map((exercise) => ({
    exerciseName: exercise.exerciseName.trim(),
    sets: exercise.sets.map((set) => ({
      reps: Number(set.reps),
      weightKg: Number(set.weightKg),
    })),
  }))
}

function normalizeExerciseName(exerciseName: string) {
  return exerciseName.trim().toUpperCase()
}

function mapSessionToForm(session: ActiveWorkoutSession): WorkoutFormState {
  return {
    date: session.startedAtUtc.slice(0, 10),
    notes: session.notes,
    exerciseEntries: session.exerciseEntries.map((exercise) => ({
      exerciseName: exercise.exerciseName,
      catalogItem: null,
      sets: exercise.sets
        .slice()
        .sort((left, right) => left.order - right.order)
        .map((set) => ({
          reps: set.reps.toString(),
          weightKg: set.weightKg.toString(),
        })),
    })),
  }
}

function mapTemplateToForm(template: WorkoutTemplate): WorkoutFormState {
  return {
    date: getTodayDateValue(),
    notes: template.notes,
    exerciseEntries: template.exerciseEntries.map((exercise) => ({
      exerciseName: exercise.exerciseName,
      catalogItem: null,
      sets: exercise.sets
        .slice()
        .sort((left, right) => left.order - right.order)
        .map((set) => ({
          reps: set.reps.toString(),
          weightKg: set.weightKg.toString(),
        })),
    })),
  }
}

export function WorkoutsPage() {
  const [workouts, setWorkouts] = useState<Workout[]>([])
  const [templates, setTemplates] = useState<WorkoutTemplate[]>([])
  const [goals, setGoals] = useState<GoalSettings | null>(null)
  const [cycleGuidance, setCycleGuidance] = useState<CycleGuidance | null>(null)
  const [readinessLog, setReadinessLog] = useState<ReadinessLog | null>(null)
  const [calorieLog, setCalorieLog] = useState<CalorieLog | null>(null)
  const [activeSession, setActiveSession] = useState<ActiveWorkoutSession | null>(null)
  const [activeForm, setActiveForm] = useState<WorkoutFormState>(initialActiveFormState)
  const [activeErrors, setActiveErrors] = useState<WorkoutFormErrors>({ exercises: [] })
  const [quickLogForm, setQuickLogForm] = useState<WorkoutFormState>(initialQuickLogFormState)
  const [quickLogErrors, setQuickLogErrors] = useState<WorkoutFormErrors>({ exercises: [] })
  const [cardioForm, setCardioForm] = useState<CardioFormState>(initialCardioFormState)
  const [cardioErrors, setCardioErrors] = useState<CardioFormErrors>({})
  const [workoutSearch, setWorkoutSearch] = useState('')
  const [workoutDateFrom, setWorkoutDateFrom] = useState('')
  const [workoutDateTo, setWorkoutDateTo] = useState('')
  const [templateName, setTemplateName] = useState('')
  const [templateErrors, setTemplateErrors] = useState<TemplateFormErrors>({})
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [isSavingCardio, setIsSavingCardio] = useState(false)
  const [isSavingTemplate, setIsSavingTemplate] = useState(false)
  const [isStartingSession, setIsStartingSession] = useState(false)
  const [isSavingSession, setIsSavingSession] = useState(false)
  const [isCompletingSession, setIsCompletingSession] = useState(false)
  const [deletingWorkoutId, setDeletingWorkoutId] = useState<number | null>(null)
  const [deletingTemplateId, setDeletingTemplateId] = useState<number | null>(null)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [overloadRecommendations, setOverloadRecommendations] = useState<
    Record<string, OverloadRecommendationState>
  >({})

  useEffect(() => {
    void loadData()
  }, [])

  const stats = useMemo(() => {
    const latestWorkout = workouts[0]
    const totalExercises = workouts.reduce(
      (count, workout) => count + workout.exerciseEntries.length,
      0,
    )
    const totalSets = workouts.reduce(
      (count, workout) =>
        count +
        workout.exerciseEntries.reduce((setCount, exercise) => setCount + exercise.sets.length, 0),
      0,
    )

    return {
      latestWorkout,
      totalWorkouts: workouts.length,
      cardioSessions: workouts.filter((workout) => workout.workoutType === 'cardio').length,
      totalExercises,
      totalSets,
    }
  }, [workouts])

  const filteredWorkouts = useMemo(() => {
    const normalizedSearch = workoutSearch.trim().toUpperCase()

    return workouts.filter((workout) => {
      const workoutDate = workout.date.slice(0, 10)
      const matchesDateFrom = !workoutDateFrom || workoutDate >= workoutDateFrom
      const matchesDateTo = !workoutDateTo || workoutDate <= workoutDateTo
      const matchesSearch =
        !normalizedSearch ||
        workout.notes.toUpperCase().includes(normalizedSearch) ||
        (workout.cardioActivityType?.toUpperCase().includes(normalizedSearch) ?? false) ||
        workout.exerciseEntries.some((exercise) =>
          exercise.exerciseName.trim().toUpperCase().includes(normalizedSearch),
        )

      return matchesDateFrom && matchesDateTo && matchesSearch
    })
  }, [workoutDateFrom, workoutDateTo, workoutSearch, workouts])

  const activeSessionStats = useMemo(() => {
    const exerciseCount = activeForm.exerciseEntries.length
    const setCount = activeForm.exerciseEntries.reduce((count, exercise) => count + exercise.sets.length, 0)

    return {
      exerciseCount,
      setCount,
    }
  }, [activeForm.exerciseEntries])

  const calorieBalance = useMemo(
    () => buildDailyCalorieBalance(workouts, goals, calorieLog, getTodayDateValue()),
    [calorieLog, goals, workouts],
  )
  const trainingScore = useMemo(
    () =>
      buildDailyTrainingScore({
        workouts,
        goals,
        calorieLog,
        readinessLog,
        cycleGuidance,
        date: getTodayDateValue(),
      }).score,
    [calorieLog, cycleGuidance, goals, readinessLog, workouts],
  )

  const assistantInsight = useMemo(
    () => getWorkoutAssistantInsight(workouts, goals, cycleGuidance, readinessLog, calorieBalance, trainingScore),
    [calorieBalance, cycleGuidance, goals, readinessLog, trainingScore, workouts],
  )

  const selectedExerciseRequests = useMemo(() => {
    const requests = new Map<string, string>()

    for (const exercise of [...quickLogForm.exerciseEntries, ...activeForm.exerciseEntries]) {
      const exerciseName = exercise.exerciseName.trim()
      const key = normalizeExerciseName(exerciseName)

      if (key.length >= 3 && !requests.has(key)) {
        requests.set(key, exerciseName)
      }
    }

    return Array.from(requests, ([key, exerciseName]) => ({ key, exerciseName }))
  }, [activeForm.exerciseEntries, quickLogForm.exerciseEntries])

  useEffect(() => {
    let isCancelled = false
    const pendingRequests = selectedExerciseRequests.filter(
      ({ key }) => overloadRecommendations[key] === undefined,
    )

    if (pendingRequests.length === 0) {
      return
    }

    const timeoutId = window.setTimeout(() => {
      for (const { key, exerciseName } of pendingRequests) {
        setOverloadRecommendations((current) => {
          if (current[key]) {
            return current
          }

          return {
            ...current,
            [key]: {
              status: 'loading',
              recommendation: null,
            },
          }
        })

        void fetchProgressiveOverloadRecommendation(exerciseName)
          .then((recommendation) => {
            if (isCancelled) {
              return
            }

            setOverloadRecommendations((current) => ({
              ...current,
              [key]: {
                status: 'loaded',
                recommendation,
              },
            }))
          })
          .catch((error) => {
            if (isCancelled) {
              return
            }

            setOverloadRecommendations((current) => ({
              ...current,
              [key]: {
                status: 'error',
                recommendation: null,
                errorMessage: getRequestErrorMessage(error, 'Progressive overload guidance is unavailable.'),
              },
            }))
          })
      }
    }, 350)

    return () => {
      isCancelled = true
      window.clearTimeout(timeoutId)
    }
  }, [overloadRecommendations, selectedExerciseRequests])

  function getOverloadRecommendationState(exerciseName: string) {
    const key = normalizeExerciseName(exerciseName)
    return key ? overloadRecommendations[key] : undefined
  }

  async function loadData() {
    try {
      setIsLoading(true)
      setErrorMessage(null)

      const [workoutData, templateData, currentActiveSession, goalData, cycleGuidanceData, latestReadinessLog, latestCalorieLog] = await Promise.all([
        fetchWorkouts(),
        fetchWorkoutTemplates(),
        fetchActiveWorkoutSession(),
        fetchGoals().catch(() => null),
        fetchCycleGuidance().catch(() => null),
        fetchLatestReadinessLog().catch(() => null),
        fetchLatestCalorieLog().catch(() => null),
      ])

      setWorkouts(workoutData)
      setTemplates(templateData)
      setGoals(goalData)
      setCycleGuidance(cycleGuidanceData)
      setReadinessLog(latestReadinessLog)
      setCalorieLog(latestCalorieLog)
      setActiveSession(currentActiveSession)
      setActiveForm(currentActiveSession ? mapSessionToForm(currentActiveSession) : initialActiveFormState())
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to load workouts, templates, and active session.'))
    } finally {
      setIsLoading(false)
    }
  }

  function resetQuickLogForm() {
    setQuickLogForm(initialQuickLogFormState())
    setQuickLogErrors({ exercises: [] })
  }

  function resetCardioForm() {
    setCardioForm(initialCardioFormState())
    setCardioErrors({})
  }

  function resetActiveForm(session: ActiveWorkoutSession | null) {
    setActiveForm(session ? mapSessionToForm(session) : initialActiveFormState())
    setActiveErrors({ exercises: [] })
  }

  function applyTemplateToQuickLog(template: WorkoutTemplate) {
    setQuickLogForm(mapTemplateToForm(template))
    setQuickLogErrors({ exercises: [] })
    setFeedback(`Loaded "${template.name}" into quick log.`)
    setErrorMessage(null)
  }

  async function handleStartActiveSession(template?: WorkoutTemplate) {
    if (activeSession) {
      return
    }

    try {
      setIsStartingSession(true)
      setFeedback(null)
      setErrorMessage(null)

      const session = await startActiveWorkoutSession({
        notes: template?.notes ?? '',
        exerciseEntries: template ? toExercisePayload(mapTemplateToForm(template).exerciseEntries) : [],
      })

      setActiveSession(session)
      resetActiveForm(session)
      setFeedback(
        template
          ? `Started active workout from "${template.name}".`
          : 'Started a new active workout session.',
      )
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to start an active workout.'))
    } finally {
      setIsStartingSession(false)
    }
  }

  function updateExercise(
    kind: 'quick' | 'active',
    exerciseIndex: number,
    field: keyof ExerciseFormState,
    value: string | ExerciseCatalogItem | null,
  ) {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === exerciseIndex
          ? {
              ...exercise,
              [field]: value,
              ...(field === 'exerciseName' ? { catalogItem: null } : null),
            }
          : exercise,
      ),
    }))
  }

  function selectCatalogExercise(
    kind: 'quick' | 'active',
    exerciseIndex: number,
    catalogItem: ExerciseCatalogItem,
  ) {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === exerciseIndex
          ? {
              ...exercise,
              exerciseName: catalogItem.name,
              catalogItem,
            }
          : exercise,
      ),
    }))
  }

  function updateSet(
    kind: 'quick' | 'active',
    exerciseIndex: number,
    setIndex: number,
    field: keyof ExerciseSetFormState,
    value: string,
  ) {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentExerciseIndex) =>
        currentExerciseIndex === exerciseIndex
          ? {
              ...exercise,
              sets: exercise.sets.map((set, currentSetIndex) =>
                currentSetIndex === setIndex ? { ...set, [field]: value } : set,
              ),
            }
          : exercise,
      ),
    }))
  }

  function addExercise(kind: 'quick' | 'active') {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: [...current.exerciseEntries, createExerciseForm()],
    }))
  }

  function removeExercise(kind: 'quick' | 'active', exerciseIndex: number) {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.filter((_, currentIndex) => currentIndex !== exerciseIndex),
    }))
  }

  function addSet(kind: 'quick' | 'active', exerciseIndex: number) {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === exerciseIndex
          ? { ...exercise, sets: [...exercise.sets, createSetForm()] }
          : exercise,
      ),
    }))
  }

  function removeSet(kind: 'quick' | 'active', exerciseIndex: number, setIndex: number) {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === exerciseIndex
          ? { ...exercise, sets: exercise.sets.filter((_, currentSetIndex) => currentSetIndex !== setIndex) }
          : exercise,
      ),
    }))
  }

  async function handleQuickLogSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const nextErrors = validateWorkoutForm(quickLogForm)
    setQuickLogErrors(nextErrors)
    setFeedback(null)
    setErrorMessage(null)

    if (hasWorkoutErrors(nextErrors)) {
      return
    }

    try {
      setIsSaving(true)
      const createdWorkout = await createWorkout({
        date: quickLogForm.date,
        notes: quickLogForm.notes.trim(),
        exerciseEntries: toExercisePayload(quickLogForm.exerciseEntries),
      })

      setWorkouts((current) => [createdWorkout, ...current].sort(compareWorkouts))
      setFeedback('Workout saved.')
      resetQuickLogForm()
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to save this workout.'))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleCardioSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const nextErrors = validateCardioForm(cardioForm)
    setCardioErrors(nextErrors)
    setFeedback(null)
    setErrorMessage(null)

    if (hasCardioErrors(nextErrors)) {
      return
    }

    try {
      setIsSavingCardio(true)
      const createdWorkout = await createWorkout({
        date: cardioForm.date,
        workoutType: 'cardio',
        notes: cardioForm.notes.trim(),
        exerciseEntries: [],
        cardioActivityType: cardioForm.cardioActivityType,
        cardioDurationMinutes: Number(cardioForm.cardioDurationMinutes),
        cardioDistanceKm:
          cardioForm.cardioDistanceKm.trim() === '' ? null : Number(cardioForm.cardioDistanceKm),
        cardioIntensity: cardioForm.cardioIntensity,
      })

      setWorkouts((current) => [createdWorkout, ...current].sort(compareWorkouts))
      setFeedback('Cardio session saved.')
      resetCardioForm()
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to save this cardio session.'))
    } finally {
      setIsSavingCardio(false)
    }
  }

  async function handleDeleteWorkout(workout: Workout) {
    const confirmed = window.confirm('Are you sure you want to delete this workout? This cannot be undone.')
    if (!confirmed) {
      return
    }

    try {
      setDeletingWorkoutId(workout.id)
      setFeedback(null)
      setErrorMessage(null)
      await deleteWorkout(workout.id)
      setWorkouts((current) => current.filter((currentWorkout) => currentWorkout.id !== workout.id))
      setFeedback('Workout deleted.')
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to delete this workout.'))
    } finally {
      setDeletingWorkoutId(null)
    }
  }

  async function handleSaveTemplate() {
    const name = templateName.trim()

    if (!name) {
      setTemplateErrors({ name: 'Template name is required.' })
      return
    }

    const nextErrors = validateWorkoutForm(quickLogForm)
    setQuickLogErrors(nextErrors)
    setTemplateErrors({})
    setFeedback(null)
    setErrorMessage(null)

    if (hasWorkoutErrors(nextErrors)) {
      return
    }

    try {
      setIsSavingTemplate(true)
      const createdTemplate = await createWorkoutTemplate({
        name,
        notes: quickLogForm.notes.trim(),
        exerciseEntries: toExercisePayload(quickLogForm.exerciseEntries),
      })

      setTemplates((current) =>
        [...current, createdTemplate].sort(
          (left, right) => left.name.localeCompare(right.name) || left.id - right.id,
        ),
      )
      setTemplateName('')
      setFeedback(`Template "${createdTemplate.name}" saved.`)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to save this template.'))
    } finally {
      setIsSavingTemplate(false)
    }
  }

  async function handleDeleteTemplate(template: WorkoutTemplate) {
    const confirmed = window.confirm('Are you sure you want to delete this template? This cannot be undone.')
    if (!confirmed) {
      return
    }

    try {
      setDeletingTemplateId(template.id)
      setFeedback(null)
      setErrorMessage(null)
      await deleteWorkoutTemplate(template.id)
      setTemplates((current) => current.filter((currentTemplate) => currentTemplate.id !== template.id))
      setFeedback(`Template "${template.name}" deleted.`)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to delete this template.'))
    } finally {
      setDeletingTemplateId(null)
    }
  }

  async function handleSaveActiveSession() {
    if (!activeSession) {
      return
    }

    const nextErrors = validateWorkoutForm(activeForm, {
      requireDate: false,
      requireExercises: false,
    })
    setActiveErrors(nextErrors)
    setFeedback(null)
    setErrorMessage(null)

    if (hasWorkoutErrors(nextErrors)) {
      return
    }

    try {
      setIsSavingSession(true)
      const updatedSession = await updateActiveWorkoutSession({
        notes: activeForm.notes.trim(),
        exerciseEntries: toExercisePayload(activeForm.exerciseEntries),
      })

      setActiveSession(updatedSession)
      resetActiveForm(updatedSession)
      setFeedback('Active workout progress saved.')
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to save active workout progress.'))
    } finally {
      setIsSavingSession(false)
    }
  }

  async function handleCompleteActiveSession() {
    if (!activeSession) {
      return
    }

    const nextErrors = validateWorkoutForm(activeForm, {
      requireDate: false,
      requireExercises: true,
    })
    setActiveErrors(nextErrors)
    setFeedback(null)
    setErrorMessage(null)

    if (hasWorkoutErrors(nextErrors)) {
      return
    }

    try {
      setIsCompletingSession(true)
      await updateActiveWorkoutSession({
        notes: activeForm.notes.trim(),
        exerciseEntries: toExercisePayload(activeForm.exerciseEntries),
      })

      const completedWorkout = await completeActiveWorkoutSession()

      setWorkouts((current) => [completedWorkout, ...current].sort(compareWorkouts))
      setActiveSession(null)
      resetActiveForm(null)
      setFeedback('Active workout completed and moved into workout history.')
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to complete the active workout.'))
    } finally {
      setIsCompletingSession(false)
    }
  }

  return (
    <main className="page-shell">
      <section className="workouts-hero-forge">
        <div className="workouts-hero-main">
          <span className="eyebrow">FORGE / Workload</span>
          <h1>Workouts</h1>
          <p className="hero-text">
            Log strength and cardio cleanly, keep templates close, and surface coaching signals without crowding the core workflow.
          </p>
        </div>
        <div className="workouts-hero-side">
          <article className="forge-focus-card">
            <span className="stat-label">Current operating state</span>
            <strong>{activeSession ? 'Active workout running' : 'Ready to log'}</strong>
            <p>
              {activeSession
                ? `${activeSessionStats.exerciseCount} exercises and ${activeSessionStats.setCount} sets in progress.`
                : assistantInsight.todaySuggestion.message}
            </p>
            <div className="forge-focus-pills">
              <span className="info-pill">{stats.totalWorkouts} total workouts</span>
              <span className="info-pill info-pill-cardio">{stats.cardioSessions} cardio sessions</span>
            </div>
          </article>
        </div>
      </section>

      <section className="forge-stat-strip forge-stat-strip-workouts">
        <WorkoutSignalCard
          tone="lime"
          label="Latest"
          value={stats.latestWorkout ? formatDate(stats.latestWorkout.date) : 'No data'}
          description={
            stats.latestWorkout
              ? stats.latestWorkout.workoutType === 'cardio'
                ? `${formatCardioActivityType(stats.latestWorkout.cardioActivityType)} for ${stats.latestWorkout.cardioDurationMinutes} min`
                : `${stats.latestWorkout.exerciseEntries.length} exercises logged`
              : 'Create your first workout'
          }
        />
        <WorkoutSignalCard tone="blue" label="Workouts" value={stats.totalWorkouts.toString()} description="Sessions recorded" />
        <WorkoutSignalCard tone="teal" label="Exercises" value={stats.totalExercises.toString()} description="Exercise entries across all workouts" />
        <WorkoutSignalCard tone="amber" label="Sets" value={stats.totalSets.toString()} description="Logged working sets" />
        <WorkoutSignalCard tone="violet" label="Cardio" value={stats.cardioSessions.toString()} description="Cardio sessions logged" />
        <WorkoutSignalCard
          tone="rose"
          label="Active session"
          value={activeSession ? 'In progress' : 'Idle'}
          description={activeSession ? `Started ${formatDate(activeSession.startedAtUtc)}` : 'Start from scratch or a template'}
        />
      </section>

      {feedback || errorMessage ? (
        <section className="content-grid workout-grid">
          <div className="panel panel-span-2 page-feedback-panel">
            <div className="feedback-stack page-feedback-stack">
              {feedback ? <p className="feedback success">{feedback}</p> : null}
              {errorMessage ? <p className="feedback error">{errorMessage}</p> : null}
            </div>
          </div>
        </section>
      ) : null}

      <section className="workouts-main-grid">
        <div className="panel panel-span-2 workout-assistant-panel workouts-panel-full">
          <div className="panel-header">
            <div>
              <h2>Workout assistant</h2>
              <p>Keep guidance prominent, but isolate it from the logging actions so the page stays readable under load.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading guidance" description="Reviewing your recent training patterns." loading />
          ) : errorMessage ? (
            <StateCard title="Guidance unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="assistant-grid workout-assistant-grid workouts-assistant-grid">
              <article className="assistant-card assistant-card-highlight">
                <span className="stat-label">Today&apos;s suggestion</span>
                <strong>{assistantInsight.todaySuggestion.title}</strong>
                <p>{assistantInsight.todaySuggestion.message}</p>
                <span className="record-hint">
                  Recommended focus: {assistantInsight.todaySuggestion.trainingType}
                </span>
              </article>

              <article className="assistant-card">
                <span className="stat-label">Weekly consistency</span>
                <strong>Stay on track</strong>
                <p>{assistantInsight.weeklyNudge}</p>
                <span className="record-hint">Based on your current weekly workout target.</span>
              </article>

              <article className="assistant-card">
                <span className="stat-label">PR opportunity</span>
                <strong>
                  {assistantInsight.prOpportunity
                    ? `${assistantInsight.prOpportunity.exerciseName} PR window`
                    : 'No clear PR push right now'}
                </strong>
                <p>
                  {assistantInsight.prOpportunity
                    ? `${assistantInsight.prOpportunity.message}${assistantInsight.prOpportunity.targetWeightKg ? ` Target ${assistantInsight.prOpportunity.targetWeightKg} kg.` : ''}`
                    : 'Build a little more recent strength data before pushing for a heavier top set.'}
                </p>
                <span className="record-hint">Simple heuristics from your recent logs.</span>
              </article>

              <article className="assistant-card">
                <span className="stat-label">
                  {cycleGuidance?.isEnabled ? 'Cycle-aware guidance' : 'Revisit next'}
                </span>
                <strong>
                  {cycleGuidance?.isEnabled
                    ? cycleGuidance.guidanceHeadline
                    : assistantInsight.revisitSuggestions[0]?.exerciseName ?? 'Exercise rotation looks current'}
                </strong>
                <p>
                  {cycleGuidance?.isEnabled
                    ? cycleGuidance.guidanceMessage
                    : assistantInsight.revisitSuggestions[0]?.message ??
                      'Nothing stands out as overdue from your recent history.'}
                </p>
                <span className="record-hint">
                  {cycleGuidance?.isEnabled
                    ? cycleGuidance.estimatedCurrentPhase
                      ? `Estimated phase: ${cycleGuidance.estimatedCurrentPhase}.`
                      : 'Add more cycle history for a better estimate.'
                    : 'Use this as a simple prompt, not fixed programming.'}
                </span>
              </article>
            </div>
          )}
        </div>

        <div className="panel panel-span-2 workouts-panel-full">
          <div className="panel-header">
            <div>
              <h2>{activeSession ? 'Active workout bay' : 'Start workout'}</h2>
              <p>
                {activeSession
                  ? 'This session stays in progress until you complete it.'
                  : 'Start from scratch here, or start from a template on the right.'}
              </p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading active workout" description="Checking whether you already have a session in progress." loading />
          ) : activeSession ? (
            <>
              <div className="session-banner session-banner-prominent workouts-active-banner">
                <div className="session-banner-copy">
                  <span className="pr-badge">In Progress</span>
                  <span className="record-hint">Started on {formatDate(activeSession.startedAtUtc)}</span>
                </div>
                <div className="session-banner-metrics" aria-label="Active session summary">
                  <span className="info-pill">
                    {activeSessionStats.exerciseCount} exercise{activeSessionStats.exerciseCount === 1 ? '' : 's'}
                  </span>
                  <span className="info-pill">
                    {activeSessionStats.setCount} set{activeSessionStats.setCount === 1 ? '' : 's'}
                  </span>
                </div>
              </div>

              <div className="weight-form workout-flow-stack workouts-active-shell">
                <label className="field">
                  <span>Notes</span>
                  <textarea
                    className="text-area"
                    rows={4}
                    maxLength={500}
                    placeholder="Optional notes for this active session"
                    value={activeForm.notes}
                    onChange={(event) =>
                      setActiveForm((current) => ({ ...current, notes: event.target.value }))
                    }
                    aria-invalid={Boolean(activeErrors.notes)}
                  />
                  <small>Use notes for cues like tempo, intensity, or anything you want to remember before saving.</small>
                  {activeErrors.notes ? <small className="field-error">{activeErrors.notes}</small> : null}
                </label>

                <div className="exercise-builder exercise-builder-emphasis">
                  <div className="section-title-row">
                    <div>
                      <h3>Session exercises</h3>
                      <p>Add movements as you work through the session, then save progress between sets if needed.</p>
                    </div>
                    <button type="button" className="ghost-button" onClick={() => addExercise('active')}>
                      Add exercise
                    </button>
                  </div>

                  {activeErrors.exerciseEntries ? (
                    <small className="field-error">{activeErrors.exerciseEntries}</small>
                  ) : null}

                  {activeForm.exerciseEntries.length === 0 ? (
                    <StateCard
                      title="No exercises yet"
                      description="Add your first exercise, then log each set as you work through the session."
                    />
                  ) : (
                    <div className="exercise-list">
                      {activeForm.exerciseEntries.map((exercise, exerciseIndex) => (
                        <ExerciseEditorCard
                          key={`active-${exerciseIndex}`}
                          title={`Exercise ${exerciseIndex + 1}`}
                          sectionLabel="Active session"
                          exercise={exercise}
                          errors={activeErrors.exercises[exerciseIndex]}
                          workouts={workouts}
                          overloadState={getOverloadRecommendationState(exercise.exerciseName)}
                          onExerciseChange={(field, value) =>
                            updateExercise('active', exerciseIndex, field, value)
                          }
                          onCatalogSelect={(catalogItem) =>
                            selectCatalogExercise('active', exerciseIndex, catalogItem)
                          }
                          onSetChange={(setIndex, field, value) =>
                            updateSet('active', exerciseIndex, setIndex, field, value)
                          }
                          onAddSet={() => addSet('active', exerciseIndex)}
                          onRemoveSet={(setIndex) => removeSet('active', exerciseIndex, setIndex)}
                          onRemoveExercise={() => removeExercise('active', exerciseIndex)}
                        />
                      ))}
                    </div>
                  )}
                </div>

                <div className="action-row action-row-prominent">
                  <button
                    type="button"
                    className="ghost-button"
                    onClick={() => void handleSaveActiveSession()}
                    disabled={isSavingSession || isCompletingSession}
                  >
                    {isSavingSession ? 'Saving...' : 'Save progress'}
                  </button>
                  <button
                    type="button"
                    className="primary-button"
                    onClick={() => void handleCompleteActiveSession()}
                    disabled={isSavingSession || isCompletingSession}
                  >
                    {isCompletingSession ? 'Completing...' : 'Complete workout'}
                  </button>
                </div>
              </div>
            </>
          ) : (
            <div className="start-workout-stack workout-empty-state workouts-empty-launch">
              <StateCard
                title="No active workout"
                description="Start from scratch here, or start from one of your saved templates."
              />
              <button
                type="button"
                className="primary-button"
                onClick={() => void handleStartActiveSession()}
                disabled={isStartingSession}
              >
                {isStartingSession ? 'Starting...' : 'Start from scratch'}
              </button>
            </div>
          )}
        </div>

        <div className="panel workouts-panel-strength">
          <div className="panel-header">
            <div>
              <h2>Quick strength log</h2>
              <p>Save a completed strength workout directly without using active mode.</p>
            </div>
          </div>

          <div className="template-toolbar template-toolbar-workout workouts-template-toolbar">
            <label className="field template-name-field">
              <span>Save current structure as template</span>
              <input
                type="text"
                placeholder="Upper Body A"
                value={templateName}
                onChange={(event) => setTemplateName(event.target.value)}
                aria-invalid={Boolean(templateErrors.name)}
              />
              {templateErrors.name ? <small className="field-error">{templateErrors.name}</small> : null}
            </label>

            <div className="toolbar-actions">
              <button
                type="button"
                className="ghost-button"
                onClick={() => void handleSaveTemplate()}
                disabled={isSavingTemplate}
              >
                {isSavingTemplate ? 'Saving...' : 'Save as template'}
              </button>

              <button type="button" className="ghost-button" onClick={resetQuickLogForm} disabled={isSaving}>
                Clear form
              </button>
            </div>
          </div>

          <form className="weight-form workout-flow-stack workouts-strength-form" onSubmit={handleQuickLogSubmit} noValidate>
            <label className="field">
              <span>Date</span>
              <input
                type="date"
                value={quickLogForm.date}
                onChange={(event) =>
                  setQuickLogForm((current) => ({ ...current, date: event.target.value }))
                }
                aria-invalid={Boolean(quickLogErrors.date)}
              />
              {quickLogErrors.date ? <small className="field-error">{quickLogErrors.date}</small> : null}
            </label>

            <label className="field">
              <span>Notes</span>
              <textarea
                className="text-area"
                rows={4}
                maxLength={500}
                placeholder="Optional notes for this session"
                value={quickLogForm.notes}
                onChange={(event) =>
                  setQuickLogForm((current) => ({ ...current, notes: event.target.value }))
                }
                aria-invalid={Boolean(quickLogErrors.notes)}
              />
              <small>Optional notes help add context before this workout goes into history.</small>
              {quickLogErrors.notes ? <small className="field-error">{quickLogErrors.notes}</small> : null}
            </label>

            <div className="exercise-builder workouts-exercise-builder">
              <div className="section-title-row">
                <div>
                  <h3>Exercises</h3>
                  <p>Add each movement in the order you performed it. Consistent naming keeps history and PR suggestions cleaner.</p>
                </div>
                <button type="button" className="ghost-button" onClick={() => addExercise('quick')}>
                  Add exercise
                </button>
              </div>

              {quickLogErrors.exerciseEntries ? (
                <small className="field-error">{quickLogErrors.exerciseEntries}</small>
              ) : null}

              {quickLogForm.exerciseEntries.length === 0 ? (
                <StateCard
                  title="No exercises yet"
                  description="Add your first exercise when you are ready to build this strength workout."
                />
              ) : (
                <div className="exercise-list">
                  {quickLogForm.exerciseEntries.map((exercise, exerciseIndex) => (
                    <ExerciseEditorCard
                      key={`quick-${exerciseIndex}`}
                      title={`Exercise ${exerciseIndex + 1}`}
                      sectionLabel="Quick log"
                      exercise={exercise}
                      errors={quickLogErrors.exercises[exerciseIndex]}
                      workouts={workouts}
                      overloadState={getOverloadRecommendationState(exercise.exerciseName)}
                      onExerciseChange={(field, value) =>
                        updateExercise('quick', exerciseIndex, field, value)
                      }
                      onCatalogSelect={(catalogItem) =>
                        selectCatalogExercise('quick', exerciseIndex, catalogItem)
                      }
                      onSetChange={(setIndex, field, value) =>
                        updateSet('quick', exerciseIndex, setIndex, field, value)
                      }
                      onAddSet={() => addSet('quick', exerciseIndex)}
                      onRemoveSet={(setIndex) => removeSet('quick', exerciseIndex, setIndex)}
                      onRemoveExercise={() => removeExercise('quick', exerciseIndex)}
                    />
                  ))}
                </div>
              )}
            </div>

            <div className="action-row action-row-prominent">
              <button type="submit" className="primary-button" disabled={isSaving}>
                {isSaving ? 'Saving...' : 'Save workout'}
              </button>
            </div>
          </form>
        </div>

        <div className="panel workouts-panel-templates">
          <div className="panel-header">
            <div>
              <h2>Templates</h2>
              <p>Reusable workout structures for quick logging or active sessions.</p>
            </div>
          </div>

          <p className="section-note">
            Keep a short list of repeatable structures here, then use quick log or active mode when you are ready to train.
          </p>

          {isLoading ? (
            <StateCard title="Loading templates" description="Fetching your saved workout structures." loading />
          ) : templates.length === 0 ? (
            <StateCard
              title="No templates yet"
              description="Save your quick-log workout structure to reuse it later."
            />
          ) : (
            <div className="template-list" role="list">
              {templates.map((template) => (
                <article key={template.id} className="template-card" role="listitem">
                  <div className="workout-card-header">
                    <div>
                      <p className="entry-date">{template.name}</p>
                      <strong className="entry-weight">{template.exerciseEntries.length} exercises</strong>
                    </div>
                  </div>

                  {template.notes ? <p className="workout-notes">{template.notes}</p> : null}

                  <div className="exercise-summary-list">
                    {template.exerciseEntries.map((exercise) => (
                      <div key={exercise.id} className="exercise-summary-item">
                        <div className="exercise-summary-copy">
                          <strong>{exercise.exerciseName}</strong>
                          <span>{describeSets(exercise.sets)}</span>
                        </div>
                      </div>
                    ))}
                  </div>

                  <div className="action-row action-row-inline">
                    <button
                      type="button"
                      className="ghost-button"
                      onClick={() => applyTemplateToQuickLog(template)}
                    >
                      Use for quick log
                    </button>
                    <button
                      type="button"
                      className="primary-button"
                      onClick={() => void handleStartActiveSession(template)}
                      disabled={Boolean(activeSession) || isStartingSession}
                    >
                      Start active
                    </button>
                  </div>

                  <div className="template-card-footer">
                    <button
                      type="button"
                      className="ghost-button subtle-danger-button compact-button"
                      onClick={() => void handleDeleteTemplate(template)}
                      disabled={deletingTemplateId === template.id}
                    >
                      {deletingTemplateId === template.id ? 'Deleting...' : 'Delete template'}
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>

        <div className="panel workouts-panel-cardio">
          <div className="panel-header">
            <div>
              <h2>Quick log cardio</h2>
              <p>Save a simple walk, run, ride, or recovery session without using strength fields.</p>
            </div>
          </div>

          <form className="weight-form profile-form-panel" onSubmit={handleCardioSubmit} noValidate>
            <div className="form-grid">
              <label className="field">
                <span>Date</span>
                <input
                  type="date"
                  value={cardioForm.date}
                  onChange={(event) =>
                    setCardioForm((current) => ({ ...current, date: event.target.value }))
                  }
                  aria-invalid={Boolean(cardioErrors.date)}
                />
                {cardioErrors.date ? <small className="field-error">{cardioErrors.date}</small> : null}
              </label>

              <label className="field">
                <span>Cardio type</span>
                <select
                  className="select-input"
                  value={cardioForm.cardioActivityType}
                  onChange={(event) =>
                    setCardioForm((current) => ({
                      ...current,
                      cardioActivityType: event.target.value as CardioActivityType,
                    }))
                  }
                >
                  <option value="walking">Walking</option>
                  <option value="running">Running</option>
                  <option value="cycling">Cycling</option>
                  <option value="other">Other</option>
                </select>
              </label>

              <label className="field">
                <span>Duration (minutes)</span>
                <input
                  type="number"
                  min="1"
                  max="600"
                  step="1"
                  value={cardioForm.cardioDurationMinutes}
                  onChange={(event) =>
                    setCardioForm((current) => ({
                      ...current,
                      cardioDurationMinutes: event.target.value,
                    }))
                  }
                  aria-invalid={Boolean(cardioErrors.cardioDurationMinutes)}
                />
                {cardioErrors.cardioDurationMinutes ? (
                  <small className="field-error">{cardioErrors.cardioDurationMinutes}</small>
                ) : null}
              </label>

              <label className="field">
                <span>Intensity</span>
                <select
                  className="select-input"
                  value={cardioForm.cardioIntensity}
                  onChange={(event) =>
                    setCardioForm((current) => ({
                      ...current,
                      cardioIntensity: event.target.value as CardioIntensity,
                    }))
                  }
                >
                  <option value="low">Low</option>
                  <option value="moderate">Moderate</option>
                  <option value="high">High</option>
                </select>
              </label>

              <label className="field">
                <span>Distance (km)</span>
                <input
                  type="number"
                  min="0"
                  max="500"
                  step="0.1"
                  value={cardioForm.cardioDistanceKm}
                  onChange={(event) =>
                    setCardioForm((current) => ({ ...current, cardioDistanceKm: event.target.value }))
                  }
                  aria-invalid={Boolean(cardioErrors.cardioDistanceKm)}
                  placeholder="Optional"
                />
                <small>Optional if you only want to track time and effort.</small>
                {cardioErrors.cardioDistanceKm ? (
                  <small className="field-error">{cardioErrors.cardioDistanceKm}</small>
                ) : null}
              </label>

              <label className="field field-span-2">
                <span>Notes</span>
                <textarea
                  className="text-area"
                  rows={3}
                  maxLength={500}
                  value={cardioForm.notes}
                  onChange={(event) =>
                    setCardioForm((current) => ({ ...current, notes: event.target.value }))
                  }
                  aria-invalid={Boolean(cardioErrors.notes)}
                  placeholder="Optional context like recovery walk, incline treadmill, or easy spin."
                />
                {cardioErrors.notes ? <small className="field-error">{cardioErrors.notes}</small> : null}
              </label>
            </div>

            <div className="action-row">
              <button type="button" className="ghost-button" onClick={resetCardioForm} disabled={isSavingCardio}>
                Clear cardio form
              </button>
              <button type="submit" className="primary-button" disabled={isSavingCardio}>
                {isSavingCardio ? 'Saving cardio...' : 'Save cardio'}
              </button>
            </div>
          </form>
        </div>

        <div className="panel workouts-panel-history">
          <div className="panel-header">
            <div>
              <h2>Recent workouts</h2>
              <p>Newest sessions first.</p>
            </div>
          </div>

          <div className="filter-toolbar filter-toolbar-workouts">
            <label className="field filter-field search-field">
              <span>Search exercises or notes</span>
              <input
                type="text"
                placeholder="Bench, squat, deload..."
                value={workoutSearch}
                onChange={(event) => setWorkoutSearch(event.target.value)}
              />
            </label>
            <label className="field filter-field">
              <span>From</span>
              <input
                type="date"
                value={workoutDateFrom}
                onChange={(event) => setWorkoutDateFrom(event.target.value)}
              />
            </label>
            <label className="field filter-field">
              <span>To</span>
              <input
                type="date"
                value={workoutDateTo}
                onChange={(event) => setWorkoutDateTo(event.target.value)}
              />
            </label>
          </div>

          <p className="section-note">
            Showing {filteredWorkouts.length} of {workouts.length} workout{workouts.length === 1 ? '' : 's'}.
          </p>

          {isLoading ? (
            <StateCard title="Loading workouts" description="Pulling recent sessions and exercise details." loading />
          ) : workouts.length === 0 ? (
            <StateCard
              title="No workouts yet"
              description="Save a workout with at least one exercise to start building history."
            />
          ) : filteredWorkouts.length === 0 ? (
            <StateCard
              title="No matches found"
              description="Try widening the date range or changing the search term."
            />
          ) : (
            <div className="workout-list" role="list">
              {filteredWorkouts.map((workout) => (
                <article key={workout.id} className="workout-card" role="listitem">
                  <div className="workout-card-header">
                    <div>
                      <p className="entry-date">{formatDate(workout.date)}</p>
                      <strong className="entry-weight">
                        {workout.workoutType === 'cardio'
                          ? `${formatCardioActivityType(workout.cardioActivityType)} cardio`
                          : `${workout.exerciseEntries.length} exercises`}
                      </strong>
                    </div>
                    <span
                      className={
                        workout.workoutType === 'cardio'
                          ? 'info-pill info-pill-cardio'
                          : 'info-pill info-pill-strength'
                      }
                    >
                      {workout.workoutType === 'cardio' ? 'Cardio' : 'Strength'}
                    </span>
                  </div>

                  <div className="workout-card-actions">
                    <button
                      type="button"
                      className="ghost-button subtle-danger-button compact-button"
                      onClick={() => void handleDeleteWorkout(workout)}
                      disabled={deletingWorkoutId === workout.id}
                    >
                      {deletingWorkoutId === workout.id ? 'Deleting...' : 'Delete workout'}
                    </button>
                  </div>

                  {workout.notes ? <p className="workout-notes">{workout.notes}</p> : null}

                  {workout.workoutType === 'cardio' ? (
                    <div className="exercise-summary-list">
                      <div className="exercise-summary-item cardio-summary-item">
                        <div className="exercise-summary-copy">
                          <strong>{formatCardioActivityType(workout.cardioActivityType)}</strong>
                          <span>
                            {workout.cardioDurationMinutes} min
                            {workout.cardioDistanceKm ? ` • ${workout.cardioDistanceKm} km` : ''}
                          </span>
                        </div>
                        <div className="exercise-summary-meta">
                          <span className="info-pill">
                            {formatCardioIntensity(workout.cardioIntensity)}
                          </span>
                          <span className="record-hint">
                            {workout.cardioDistanceKm ? 'Duration, distance, intensity' : 'Duration and intensity'}
                          </span>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="exercise-summary-list">
                      {workout.exerciseEntries.map((exercise) => (
                        <div key={exercise.id} className="exercise-summary-item">
                          <div className="exercise-summary-copy">
                            <strong>{exercise.exerciseName}</strong>
                            <span>{describeSets(exercise.sets)}</span>
                          </div>
                          <div className="exercise-summary-meta">
                            {exercise.isPersonalRecord ? <span className="pr-badge">PR</span> : null}
                            <span className="record-hint">Best: {exercise.personalRecordWeightKg} kg</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </article>
              ))}
            </div>
          )}
        </div>
      </section>
    </main>
  )
}

function WorkoutSignalCard({
  tone,
  label,
  value,
  description,
}: {
  tone: 'lime' | 'blue' | 'teal' | 'amber' | 'violet' | 'rose'
  label: string
  value: string
  description: string
}) {
  return (
    <article className={`forge-stat-card forge-stat-card-${tone}`}>
      <div className="forge-stat-glow" aria-hidden="true" />
      <span className="stat-label">{label}</span>
      <strong>{value}</strong>
      <p>{description}</p>
    </article>
  )
}

function validateCardioForm(form: CardioFormState): CardioFormErrors {
  const errors: CardioFormErrors = {}

  if (!form.date) {
    errors.date = 'Date is required.'
  } else if (form.date > getTodayDateValue()) {
    errors.date = 'Date cannot be in the future.'
  }

  if (!form.cardioDurationMinutes.trim()) {
    errors.cardioDurationMinutes = 'Duration is required.'
  } else {
    const duration = Number(form.cardioDurationMinutes)
    if (Number.isNaN(duration) || duration < 1 || duration > 600) {
      errors.cardioDurationMinutes = 'Duration must be between 1 and 600 minutes.'
    }
  }

  if (form.cardioDistanceKm.trim()) {
    const distance = Number(form.cardioDistanceKm)
    if (Number.isNaN(distance) || distance <= 0 || distance > 500) {
      errors.cardioDistanceKm = 'Distance must be between 0.1 and 500 km.'
    }
  }

  if (form.notes.length > 500) {
    errors.notes = 'Notes must be 500 characters or less.'
  }

  return errors
}

function hasCardioErrors(errors: CardioFormErrors) {
  return Boolean(
    errors.date ||
      errors.cardioActivityType ||
      errors.cardioDurationMinutes ||
      errors.cardioDistanceKm ||
      errors.cardioIntensity ||
      errors.notes,
  )
}

function compareWorkouts(left: Workout, right: Workout) {
  return new Date(right.date).getTime() - new Date(left.date).getTime() || right.id - left.id
}

function describeSets(
  sets: Array<{
    order: number
    reps: number
    weightKg: number
  }>,
) {
  return sets
    .slice()
    .sort((left, right) => left.order - right.order)
    .map((set) => `Set ${set.order}: ${set.reps} reps at ${set.weightKg} kg`)
    .join(' • ')
}

function formatCardioActivityType(activityType: Workout['cardioActivityType']) {
  switch (activityType) {
    case 'walking':
      return 'Walking'
    case 'running':
      return 'Running'
    case 'cycling':
      return 'Cycling'
    case 'other':
      return 'Other'
    default:
      return 'Cardio'
  }
}

function formatCardioIntensity(intensity: Workout['cardioIntensity']) {
  switch (intensity) {
    case 'low':
      return 'Low intensity'
    case 'moderate':
      return 'Moderate intensity'
    case 'high':
      return 'High intensity'
    default:
      return 'Cardio'
  }
}

function ExerciseEditorCard({
  title,
  sectionLabel,
  exercise,
  errors,
  workouts,
  overloadState,
  onExerciseChange,
  onCatalogSelect,
  onSetChange,
  onAddSet,
  onRemoveSet,
  onRemoveExercise,
}: {
  title: string
  sectionLabel: string
  exercise: ExerciseFormState
  errors?: ExerciseFieldErrors
  workouts: Workout[]
  overloadState?: OverloadRecommendationState
  onExerciseChange: (field: keyof ExerciseFormState, value: string | ExerciseCatalogItem | null) => void
  onCatalogSelect: (catalogItem: ExerciseCatalogItem) => void
  onSetChange: (setIndex: number, field: keyof ExerciseSetFormState, value: string) => void
  onAddSet: () => void
  onRemoveSet: (setIndex: number) => void
  onRemoveExercise?: () => void
}) {
  return (
    <div className="exercise-card">
      <div className="exercise-card-header">
        <div className="exercise-card-heading">
          <span className="stat-label">{sectionLabel}</span>
          <h3>{title}</h3>
        </div>
        {onRemoveExercise ? (
          <button type="button" className="ghost-button subtle-danger-button compact-button" onClick={onRemoveExercise}>
            Remove exercise
          </button>
        ) : null}
      </div>

      <ExerciseCatalogPickerField
        exercise={exercise}
        errorMessage={errors?.exerciseName}
        onExerciseNameChange={(value) => onExerciseChange('exerciseName', value)}
        onCatalogSelect={onCatalogSelect}
      />

      {exercise.catalogItem ? <ExerciseHelpCard item={exercise.catalogItem} /> : null}

      {normalizeExerciseName(exercise.exerciseName).length >= 3 ? (
        <ExerciseSuggestionNotice
          overloadState={overloadState}
          fallbackSuggestion={getSuggestedNextWeight(workouts, exercise.exerciseName, null, null)}
        />
      ) : null}

      <div className="section-title-row compact-row">
        <div>
          <h3>Sets</h3>
          <p>Track reps and load, then keep moving with quick add.</p>
        </div>
        <button type="button" className="ghost-button compact-button" onClick={onAddSet}>
          Add set
        </button>
      </div>

      {errors?.sets ? <small className="field-error">{errors.sets}</small> : null}

      <div className="set-list">
        {exercise.sets.map((set, setIndex) => (
          <div key={setIndex} className="set-card">
            <div className="set-card-header">
              <div className="set-card-title">
                <span className="set-index-badge">{setIndex + 1}</span>
                <strong>Set {setIndex + 1}</strong>
              </div>
              {exercise.sets.length > 1 ? (
                <button
                  type="button"
                  className="ghost-button subtle-danger-button compact-button"
                  onClick={() => onRemoveSet(setIndex)}
                >
                  Remove
                </button>
              ) : null}
            </div>

            <div className="exercise-fields">
              <label className="field">
                <span>Reps</span>
                <input
                  type="number"
                  min="1"
                  max="100"
                  value={set.reps}
                  onChange={(event) => onSetChange(setIndex, 'reps', event.target.value)}
                  aria-invalid={Boolean(errors?.setErrors[setIndex]?.reps)}
                />
                {errors?.setErrors[setIndex]?.reps ? (
                  <small className="field-error">{errors.setErrors[setIndex]?.reps}</small>
                ) : null}
              </label>

              <label className="field">
                <span>Weight (kg)</span>
                <input
                  type="number"
                  min="0"
                  max="500"
                  step="0.1"
                  value={set.weightKg}
                  onChange={(event) => onSetChange(setIndex, 'weightKg', event.target.value)}
                  aria-invalid={Boolean(errors?.setErrors[setIndex]?.weightKg)}
                />
                {errors?.setErrors[setIndex]?.weightKg ? (
                  <small className="field-error">{errors.setErrors[setIndex]?.weightKg}</small>
                ) : null}
              </label>
            </div>
          </div>
        ))}
      </div>

      <div className="exercise-card-footer">
        <button type="button" className="ghost-button compact-button add-set-footer-button" onClick={onAddSet}>
          Add another set
        </button>
      </div>
    </div>
  )
}

function ExerciseHelpCard({ item }: { item: ExerciseCatalogItem }) {
  const [isExpanded, setIsExpanded] = useState(false)
  const muscleTags = [item.primaryMuscle, ...item.secondaryMuscles].filter(
    (muscle): muscle is string => Boolean(muscle),
  )
  const guidanceText = item.instructions ?? item.description
  const guidancePreview =
    guidanceText && guidanceText.length > 220 ? `${guidanceText.slice(0, 220).trimEnd()}...` : guidanceText
  const shouldClamp = Boolean(guidanceText && guidanceText.length > 220)

  return (
    <section className="exercise-help-card">
      <div className="exercise-help-header">
        <div className="exercise-help-heading">
          <span className="stat-label">Exercise guidance</span>
          <h4>{item.name}</h4>
        </div>
        {shouldClamp ? (
          <button
            type="button"
            className="ghost-button compact-button"
            onClick={() => setIsExpanded((current) => !current)}
          >
            {isExpanded ? 'Show less' : 'Show more'}
          </button>
        ) : null}
      </div>

      <div className="exercise-help-body">
        {item.thumbnailUrl ? (
          <div className="exercise-help-media">
            <img src={item.thumbnailUrl} alt={item.name} />
          </div>
        ) : null}

        <div className="exercise-help-copy">
          <div className="exercise-help-pills">
            {muscleTags.slice(0, 4).map((muscle) => (
              <span key={muscle} className="info-pill">
                {formatLabel(muscle)}
              </span>
            ))}
            {item.equipment ? <span className="info-pill">{formatLabel(item.equipment)}</span> : null}
          </div>

          {guidanceText ? (
            <p className="exercise-help-text">{isExpanded ? guidanceText : guidancePreview}</p>
          ) : (
            <p className="exercise-help-text">Catalog guidance is limited for this exercise, but the name and tags are still linked.</p>
          )}

          <div className="exercise-help-footer">
            {item.videoUrl ? (
              <a className="ghost-button compact-button" href={item.videoUrl} target="_blank" rel="noreferrer">
                Watch demo
              </a>
            ) : null}
            <span className="record-hint">Catalog-backed guidance only. Your sets and save flow stay unchanged.</span>
          </div>
        </div>
      </div>
    </section>
  )
}

const exerciseCatalogSearchCache = new Map<string, ExerciseCatalogItem[]>()

function ExerciseCatalogPickerField({
  exercise,
  errorMessage,
  onExerciseNameChange,
  onCatalogSelect,
}: {
  exercise: ExerciseFormState
  errorMessage?: string
  onExerciseNameChange: (value: string) => void
  onCatalogSelect: (catalogItem: ExerciseCatalogItem) => void
}) {
  const [results, setResults] = useState<ExerciseCatalogItem[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [isOpen, setIsOpen] = useState(false)
  const [searchErrorMessage, setSearchErrorMessage] = useState<string | null>(null)
  const deferredExerciseName = useDeferredValue(exercise.exerciseName)
  const normalizedQuery = deferredExerciseName.trim().toLowerCase()

  useEffect(() => {
    let isCancelled = false

    if (!isOpen || normalizedQuery.length < 2) {
      setResults([])
      setIsLoading(false)
      setSearchErrorMessage(null)
      return
    }

    const cachedResults = exerciseCatalogSearchCache.get(normalizedQuery)
    if (cachedResults) {
      setResults(cachedResults)
      setIsLoading(false)
      setSearchErrorMessage(null)
      return
    }

    setIsLoading(true)
    setSearchErrorMessage(null)

    const timeoutId = window.setTimeout(() => {
      void searchExerciseCatalog(deferredExerciseName.trim())
        .then((nextResults) => {
          if (isCancelled) {
            return
          }

          exerciseCatalogSearchCache.set(normalizedQuery, nextResults)
          setResults(nextResults)
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
  }, [deferredExerciseName, isOpen, normalizedQuery])

  return (
    <div className="catalog-picker-field">
      <label className="field">
        <span>Exercise name</span>
        <input
          type="text"
          placeholder="Bench Press"
          value={exercise.exerciseName}
          onChange={(event) => {
            onExerciseNameChange(event.target.value)
            setIsOpen(true)
          }}
          onFocus={() => setIsOpen(true)}
          onBlur={() => window.setTimeout(() => setIsOpen(false), 120)}
          aria-invalid={Boolean(errorMessage)}
          aria-expanded={isOpen && normalizedQuery.length >= 2}
          aria-autocomplete="list"
        />
        <small>Pick from the catalog or keep typing manually to preserve your existing naming flow.</small>
        {errorMessage ? <small className="field-error">{errorMessage}</small> : null}
      </label>

      {exercise.catalogItem ? (
        <button
          type="button"
          className="catalog-selected-card"
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => setIsOpen((current) => !current)}
        >
          {exercise.catalogItem.thumbnailUrl ? (
            <img src={exercise.catalogItem.thumbnailUrl} alt="" className="catalog-selected-thumb" />
          ) : (
            <span className="catalog-selected-thumb catalog-selected-thumb-placeholder" aria-hidden="true">
              {exercise.catalogItem.name.slice(0, 1)}
            </span>
          )}
          <span className="catalog-selected-copy">
            <strong>{exercise.catalogItem.name}</strong>
            <span>
              {exercise.catalogItem.primaryMuscle ?? 'Catalog exercise'}
              {exercise.catalogItem.equipment ? ` · ${exercise.catalogItem.equipment}` : ''}
            </span>
          </span>
        </button>
      ) : null}

      {isOpen && normalizedQuery.length >= 2 ? (
        <div className="catalog-suggestions-panel" role="listbox" aria-label="Exercise catalog suggestions">
          {isLoading ? <p className="catalog-suggestion-status">Searching catalog...</p> : null}
          {!isLoading && searchErrorMessage ? <p className="catalog-suggestion-status field-error">{searchErrorMessage}</p> : null}
          {!isLoading && !searchErrorMessage && results.length === 0 ? (
            <p className="catalog-suggestion-status">No catalog matches. You can still keep the current manual name.</p>
          ) : null}
          {!isLoading && !searchErrorMessage && results.length > 0 ? (
            <div className="catalog-suggestion-list">
              {results.slice(0, 6).map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className="catalog-suggestion-item"
                  onMouseDown={(event) => event.preventDefault()}
                  onClick={() => {
                    onCatalogSelect(item)
                    setIsOpen(false)
                  }}
                >
                  {item.thumbnailUrl ? (
                    <img src={item.thumbnailUrl} alt="" className="catalog-suggestion-thumb" />
                  ) : (
                    <span className="catalog-suggestion-thumb catalog-selected-thumb-placeholder" aria-hidden="true">
                      {item.name.slice(0, 1)}
                    </span>
                  )}
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

function ExerciseSuggestionNotice({
  overloadState,
  fallbackSuggestion,
}: {
  overloadState?: OverloadRecommendationState
  fallbackSuggestion: ReturnType<typeof getSuggestedNextWeight>
}) {
  if (!overloadState || overloadState.status === 'loading') {
    return (
      <div className="suggestion-card">
        <span className="stat-label">Progressive overload guidance</span>
        <strong>Loading target...</strong>
        <span className="stat-subtext">Checking your recent saved sessions for this exercise.</span>
      </div>
    )
  }

  if (overloadState?.status === 'loaded' && overloadState.recommendation) {
    const recommendation = overloadState.recommendation

    return (
      <div className="suggestion-card">
        <span className="stat-label">Progressive overload guidance</span>
        <strong>
          {recommendation.recommendedWeightKg !== null
            ? `${recommendation.recommendedWeightKg} kg`
            : 'No target yet'}
        </strong>
        <span className="stat-subtext">{recommendation.shortReason}</span>
        <span className="record-hint">
          {formatProgressionStatus(recommendation.progressionStatus)} • {recommendation.recommendedRepTarget}
        </span>
      </div>
    )
  }

  if (overloadState?.status === 'error') {
    return (
      <div className="suggestion-card">
        <span className="stat-label">Progressive overload guidance</span>
        <strong>Guidance unavailable</strong>
        <span className="stat-subtext">
          {fallbackSuggestion
            ? fallbackSuggestion.reason
            : overloadState.errorMessage ?? 'Workout logging is still available.'}
        </span>
        {fallbackSuggestion?.confidenceLabel ? (
          <span className="record-hint">Fallback: {fallbackSuggestion.confidenceLabel}</span>
        ) : null}
      </div>
    )
  }

  if (!fallbackSuggestion) {
    return (
      <div className="suggestion-card">
        <span className="stat-label">Progressive overload guidance</span>
        <strong>No history yet</strong>
        <span className="stat-subtext">Save this exercise once to get a recommendation.</span>
      </div>
    )
  }

  return (
    <div className="suggestion-card">
      <span className="stat-label">Progressive overload guidance</span>
      <strong>{fallbackSuggestion.suggestedWeightKg} kg</strong>
      <span className="stat-subtext">{fallbackSuggestion.reason}</span>
      {fallbackSuggestion.confidenceLabel ? (
        <span className="record-hint">Fallback: {fallbackSuggestion.confidenceLabel}</span>
      ) : null}
      {fallbackSuggestion.prOpportunity ? (
        <div className="suggestion-callout">
          <span className="pr-badge">PR window</span>
          <span className="stat-subtext">
            {fallbackSuggestion.prOpportunity.message}
            {fallbackSuggestion.prOpportunity.targetWeightKg
              ? ` Target ${fallbackSuggestion.prOpportunity.targetWeightKg} kg.`
              : ''}
          </span>
        </div>
      ) : null}
    </div>
  )
}

function formatLabel(value: string) {
  return value
    .split(/[\s,_-]+/)
    .filter(Boolean)
    .map((part) => part[0]?.toUpperCase() + part.slice(1))
    .join(' ')
}

function formatProgressionStatus(status: ProgressiveOverloadRecommendation['progressionStatus']) {
  switch (status) {
    case 'increase':
      return 'Increase'
    case 'deload':
      return 'Deload'
    case 'hold':
      return 'Hold'
  }
}
