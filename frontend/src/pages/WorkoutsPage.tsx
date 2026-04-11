import { useEffect, useMemo, useState } from 'react'
import { fetchCycleGuidance } from '../api/cycle'
import { fetchGoals } from '../api/goals'
import {
  completeActiveWorkoutSession,
  createWorkout,
  createWorkoutTemplate,
  deleteWorkout,
  fetchActiveWorkoutSession,
  fetchWorkoutTemplates,
  fetchWorkouts,
  startActiveWorkoutSession,
  updateActiveWorkoutSession,
} from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { getWorkoutAssistantInsight, getSuggestedNextWeight } from '../lib/exerciseSuggestions'
import { formatDate, getTodayDateValue } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { CycleGuidance } from '../types/cycle'
import type { GoalSettings } from '../types/goals'
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

function mapSessionToForm(session: ActiveWorkoutSession): WorkoutFormState {
  return {
    date: session.startedAtUtc.slice(0, 10),
    notes: session.notes,
    exerciseEntries: session.exerciseEntries.map((exercise) => ({
      exerciseName: exercise.exerciseName,
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
  const [feedback, setFeedback] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

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

  const assistantInsight = useMemo(
    () => getWorkoutAssistantInsight(workouts, goals, cycleGuidance),
    [cycleGuidance, goals, workouts],
  )

  async function loadData() {
    try {
      setIsLoading(true)
      setErrorMessage(null)

      const [workoutData, templateData, currentActiveSession, goalData, cycleGuidanceData] = await Promise.all([
        fetchWorkouts(),
        fetchWorkoutTemplates(),
        fetchActiveWorkoutSession(),
        fetchGoals().catch(() => null),
        fetchCycleGuidance().catch(() => null),
      ])

      setWorkouts(workoutData)
      setTemplates(templateData)
      setGoals(goalData)
      setCycleGuidance(cycleGuidanceData)
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
    value: string,
  ) {
    const setter = kind === 'quick' ? setQuickLogForm : setActiveForm

    setter((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === exerciseIndex ? { ...exercise, [field]: value } : exercise,
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
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Gym Tracker</span>
          <h1>Workouts</h1>
          <p className="hero-text">
            Run an active workout session, quick-log completed sessions, reuse templates, and track every set inside each exercise.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">Latest</span>
            <strong>{stats.latestWorkout ? formatDate(stats.latestWorkout.date) : 'No data'}</strong>
            <span className="stat-subtext">
              {stats.latestWorkout
                ? stats.latestWorkout.workoutType === 'cardio'
                  ? `${formatCardioActivityType(stats.latestWorkout.cardioActivityType)} for ${stats.latestWorkout.cardioDurationMinutes} min`
                  : `${stats.latestWorkout.exerciseEntries.length} exercises logged`
                : 'Create your first workout'}
            </span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Workouts</span>
            <strong>{stats.totalWorkouts}</strong>
            <span className="stat-subtext">Sessions recorded</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Exercises</span>
            <strong>{stats.totalExercises}</strong>
            <span className="stat-subtext">Exercise entries across all workouts</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Cardio</span>
            <strong>{stats.cardioSessions}</strong>
            <span className="stat-subtext">Cardio sessions logged</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Sets</span>
            <strong>{stats.totalSets}</strong>
            <span className="stat-subtext">Logged working sets</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Active Session</span>
            <strong>{activeSession ? 'In progress' : 'Idle'}</strong>
            <span className="stat-subtext">
              {activeSession
                ? `Started ${formatDate(activeSession.startedAtUtc)}`
                : 'Start from scratch or a template'}
            </span>
          </article>
        </div>
      </section>

      <section className="content-grid workout-grid">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>{activeSession ? 'Active workout' : 'Start workout'}</h2>
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
              <div className="session-banner session-banner-prominent">
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

              <div className="weight-form workout-flow-stack">
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
                          onExerciseChange={(field, value) =>
                            updateExercise('active', exerciseIndex, field, value)
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
            <div className="start-workout-stack workout-empty-state">
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

          {feedback ? <p className="feedback success">{feedback}</p> : null}
          {errorMessage ? <p className="feedback error">{errorMessage}</p> : null}
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Templates</h2>
              <p>Reusable workout structures for quick logging or active sessions.</p>
            </div>
          </div>

          {!isLoading && !errorMessage ? (
            <div className="assistant-card workout-assistant-card">
              <div className="assistant-card-header">
                <div>
                  <span className="stat-label">Workout assistant</span>
                  <strong>What to focus on next</strong>
                </div>
                <span className="record-hint">Heuristic guidance</span>
              </div>

              <div className="assistant-list">
                <div className="assistant-list-item">
                  <strong>Weekly consistency</strong>
                  <span>{assistantInsight.weeklyNudge}</span>
                </div>

                {cycleGuidance?.isEnabled ? (
                  <div className="assistant-list-item">
                    <strong>{cycleGuidance.guidanceHeadline}</strong>
                    <span>
                      {cycleGuidance.guidanceMessage}
                      {cycleGuidance.estimatedCurrentPhase
                        ? ` Estimated phase: ${cycleGuidance.estimatedCurrentPhase}.`
                        : ''}
                    </span>
                  </div>
                ) : null}

                <div className="assistant-list-item">
                  <strong>{assistantInsight.todaySuggestion.title}</strong>
                  <span>{assistantInsight.todaySuggestion.message}</span>
                </div>

                <div className="assistant-list-item">
                  <strong>
                    {assistantInsight.prOpportunity
                      ? `${assistantInsight.prOpportunity.exerciseName} PR window`
                      : 'No clear PR push right now'}
                  </strong>
                  <span>
                    {assistantInsight.prOpportunity
                      ? `${assistantInsight.prOpportunity.message}${assistantInsight.prOpportunity.targetWeightKg ? ` Target ${assistantInsight.prOpportunity.targetWeightKg} kg.` : ''}`
                      : 'Build a little more recent strength data before pushing for a heavier top set.'}
                  </span>
                </div>

                <div className="assistant-list-item">
                  <strong>
                    {assistantInsight.revisitSuggestions[0]?.exerciseName ?? 'Exercise rotation looks current'}
                  </strong>
                  <span>
                    {assistantInsight.revisitSuggestions[0]?.message ??
                      'Nothing stands out as overdue from your recent history.'}
                  </span>
                </div>
              </div>
            </div>
          ) : null}

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
                </article>
              ))}
            </div>
          )}
        </div>
      </section>

      <section className="content-grid workout-grid">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Quick log workout</h2>
              <p>Save a completed workout directly without using active mode.</p>
            </div>
          </div>

          <div className="template-toolbar template-toolbar-workout">
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

          <form className="weight-form workout-flow-stack" onSubmit={handleQuickLogSubmit} noValidate>
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

            <div className="exercise-builder">
              <div className="section-title-row">
                <div>
                  <h3>Exercises</h3>
                  <p>Add each movement in the order you performed it. Keep exercise names consistent for cleaner history and PR tracking.</p>
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
                      onExerciseChange={(field, value) =>
                        updateExercise('quick', exerciseIndex, field, value)
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

        <div className="panel">
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
      </section>

      <section className="content-grid workout-grid">
        <div className="panel panel-span-2">
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
  onExerciseChange,
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
  onExerciseChange: (field: keyof ExerciseFormState, value: string) => void
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

      <label className="field">
        <span>Exercise name</span>
        <input
          type="text"
          placeholder="Bench Press"
          value={exercise.exerciseName}
          onChange={(event) => onExerciseChange('exerciseName', event.target.value)}
          aria-invalid={Boolean(errors?.exerciseName)}
        />
        <small>Use the same naming each time to improve progress suggestions and records.</small>
        {errors?.exerciseName ? <small className="field-error">{errors.exerciseName}</small> : null}
      </label>

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

            {exercise.exerciseName.trim() ? (
              <ExerciseSuggestionNotice
                suggestion={getSuggestedNextWeight(
                  workouts,
                  exercise.exerciseName,
                  setIndex + 1,
                  !set.reps.trim() || Number.isNaN(Number(set.reps)) ? null : Number(set.reps),
                )}
              />
            ) : null}

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

function ExerciseSuggestionNotice({
  suggestion,
}: {
  suggestion: ReturnType<typeof getSuggestedNextWeight>
}) {
  if (!suggestion) {
    return (
      <div className="suggestion-card">
        <span className="stat-label">Next Weight Suggestion</span>
        <strong>No history yet</strong>
        <span className="stat-subtext">Save this exercise once to get a recommendation.</span>
      </div>
    )
  }

  return (
    <div className="suggestion-card">
      <span className="stat-label">Next Weight Suggestion</span>
      <strong>{suggestion.suggestedWeightKg} kg</strong>
      <span className="stat-subtext">{suggestion.reason}</span>
      {suggestion.confidenceLabel ? (
        <span className="record-hint">Confidence: {suggestion.confidenceLabel}</span>
      ) : null}
      {suggestion.prOpportunity ? (
        <div className="suggestion-callout">
          <span className="pr-badge">PR window</span>
          <span className="stat-subtext">
            {suggestion.prOpportunity.message}
            {suggestion.prOpportunity.targetWeightKg
              ? ` Target ${suggestion.prOpportunity.targetWeightKg} kg.`
              : ''}
          </span>
        </div>
      ) : null}
    </div>
  )
}
