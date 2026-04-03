import { useEffect, useMemo, useState } from 'react'
import {
  completeActiveWorkoutSession,
  createWorkout,
  createWorkoutTemplate,
  fetchActiveWorkoutSession,
  fetchWorkoutTemplates,
  fetchWorkouts,
  startActiveWorkoutSession,
  updateActiveWorkoutSession,
} from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getSuggestedNextWeight } from '../lib/exerciseSuggestions'
import { getRequestErrorMessage } from '../lib/http'
import type {
  ActiveWorkoutSession,
  ExerciseEntryPayload,
  Workout,
  WorkoutTemplate,
} from '../types/workout'

type ExerciseFormState = {
  exerciseName: string
  sets: string
  reps: string
  weightKg: string
}

type WorkoutFormState = {
  date: string
  notes: string
  exerciseEntries: ExerciseFormState[]
}

type ExerciseFieldErrors = Partial<Record<keyof ExerciseFormState, string>>

type WorkoutFormErrors = {
  date?: string
  notes?: string
  exerciseEntries?: string
  exercises: ExerciseFieldErrors[]
}

type TemplateFormErrors = {
  name?: string
}

const createExerciseForm = (): ExerciseFormState => ({
  exerciseName: '',
  sets: '',
  reps: '',
  weightKg: '',
})

const initialQuickLogFormState = (): WorkoutFormState => ({
  date: new Date().toISOString().slice(0, 10),
  notes: '',
  exerciseEntries: [createExerciseForm()],
})

const initialActiveFormState = (): WorkoutFormState => ({
  date: new Date().toISOString().slice(0, 10),
  notes: '',
  exerciseEntries: [],
})

function validateWorkoutForm(
  form: WorkoutFormState,
  options?: { requireDate?: boolean; requireExercises?: boolean },
): WorkoutFormErrors {
  const requireDate = options?.requireDate ?? true
  const requireExercises = options?.requireExercises ?? true

  const exerciseErrors = form.exerciseEntries.map<ExerciseFieldErrors>((exercise) => {
    const currentErrors: ExerciseFieldErrors = {}

    if (!exercise.exerciseName.trim()) {
      currentErrors.exerciseName = 'Exercise name is required.'
    }

    const sets = Number(exercise.sets)
    if (!exercise.sets.trim()) {
      currentErrors.sets = 'Sets are required.'
    } else if (Number.isNaN(sets) || sets < 1 || sets > 20) {
      currentErrors.sets = 'Sets must be between 1 and 20.'
    }

    const reps = Number(exercise.reps)
    if (!exercise.reps.trim()) {
      currentErrors.reps = 'Reps are required.'
    } else if (Number.isNaN(reps) || reps < 1 || reps > 100) {
      currentErrors.reps = 'Reps must be between 1 and 100.'
    }

    const weightKg = Number(exercise.weightKg)
    if (!exercise.weightKg.trim()) {
      currentErrors.weightKg = 'Weight is required.'
    } else if (Number.isNaN(weightKg) || weightKg < 0 || weightKg > 500) {
      currentErrors.weightKg = 'Weight must be between 0 and 500 kg.'
    }

    return currentErrors
  })

  const errors: WorkoutFormErrors = { exercises: exerciseErrors }

  if (requireDate) {
    if (!form.date) {
      errors.date = 'Date is required.'
    } else if (form.date > new Date().toISOString().slice(0, 10)) {
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
      errors.exercises.some((exercise) => Object.keys(exercise).length > 0),
  )
}

function toExercisePayload(exerciseEntries: ExerciseFormState[]): ExerciseEntryPayload[] {
  return exerciseEntries.map((exercise) => ({
    exerciseName: exercise.exerciseName.trim(),
    sets: Number(exercise.sets),
    reps: Number(exercise.reps),
    weightKg: Number(exercise.weightKg),
  }))
}

function mapSessionToForm(session: ActiveWorkoutSession): WorkoutFormState {
  return {
    date: session.startedAtUtc.slice(0, 10),
    notes: session.notes,
    exerciseEntries: session.exerciseEntries.map((exercise) => ({
      exerciseName: exercise.exerciseName,
      sets: exercise.sets.toString(),
      reps: exercise.reps.toString(),
      weightKg: exercise.weightKg.toString(),
    })),
  }
}

function mapTemplateToForm(template: WorkoutTemplate): WorkoutFormState {
  return {
    date: new Date().toISOString().slice(0, 10),
    notes: template.notes,
    exerciseEntries: template.exerciseEntries.map((exercise) => ({
      exerciseName: exercise.exerciseName,
      sets: exercise.sets.toString(),
      reps: exercise.reps.toString(),
      weightKg: exercise.weightKg.toString(),
    })),
  }
}

export function WorkoutsPage() {
  const [workouts, setWorkouts] = useState<Workout[]>([])
  const [templates, setTemplates] = useState<WorkoutTemplate[]>([])
  const [activeSession, setActiveSession] = useState<ActiveWorkoutSession | null>(null)
  const [activeForm, setActiveForm] = useState<WorkoutFormState>(initialActiveFormState)
  const [activeErrors, setActiveErrors] = useState<WorkoutFormErrors>({ exercises: [] })
  const [quickLogForm, setQuickLogForm] = useState<WorkoutFormState>(initialQuickLogFormState)
  const [quickLogErrors, setQuickLogErrors] = useState<WorkoutFormErrors>({ exercises: [] })
  const [workoutSearch, setWorkoutSearch] = useState('')
  const [workoutDateFrom, setWorkoutDateFrom] = useState('')
  const [workoutDateTo, setWorkoutDateTo] = useState('')
  const [templateName, setTemplateName] = useState('')
  const [templateErrors, setTemplateErrors] = useState<TemplateFormErrors>({})
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [isSavingTemplate, setIsSavingTemplate] = useState(false)
  const [isStartingSession, setIsStartingSession] = useState(false)
  const [isSavingSession, setIsSavingSession] = useState(false)
  const [isCompletingSession, setIsCompletingSession] = useState(false)
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

    return {
      latestWorkout,
      totalWorkouts: workouts.length,
      totalExercises,
    }
  }, [workouts])

  const personalRecords = useMemo(() => {
    const records = new Map<string, { exerciseName: string; weightKg: number; date: string }>()

    const sortedByDate = [...workouts].sort(
      (left, right) => new Date(right.date).getTime() - new Date(left.date).getTime() || right.id - left.id,
    )

    for (const workout of sortedByDate) {
      for (const exercise of workout.exerciseEntries) {
        const key = exercise.exerciseName.trim().toUpperCase()
        const current = records.get(key)

        if (!current || exercise.weightKg >= current.weightKg) {
          records.set(key, {
            exerciseName: exercise.exerciseName,
            weightKg: exercise.personalRecordWeightKg,
            date: workout.date,
          })
        }
      }
    }

    return [...records.values()].sort(
      (left, right) => right.weightKg - left.weightKg || left.exerciseName.localeCompare(right.exerciseName),
    )
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
        workout.exerciseEntries.some((exercise) =>
          exercise.exerciseName.trim().toUpperCase().includes(normalizedSearch),
        )

      return matchesDateFrom && matchesDateTo && matchesSearch
    })
  }, [workoutDateFrom, workoutDateTo, workoutSearch, workouts])

  async function loadData() {
    try {
      setIsLoading(true)
      setErrorMessage(null)

      const [workoutData, templateData, currentActiveSession] = await Promise.all([
        fetchWorkouts(),
        fetchWorkoutTemplates(),
        fetchActiveWorkoutSession(),
      ])

      setWorkouts(workoutData)
      setTemplates(templateData)
      setActiveSession(currentActiveSession)
      setActiveForm(currentActiveSession ? mapSessionToForm(currentActiveSession) : initialActiveFormState())
    } catch {
      setErrorMessage('Unable to load workouts, templates, and active session. Check that the API is running.')
    } finally {
      setIsLoading(false)
    }
  }

  function resetQuickLogForm() {
    setQuickLogForm(initialQuickLogFormState())
    setQuickLogErrors({ exercises: [] })
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
        exerciseEntries: template
          ? template.exerciseEntries.map((exercise) => ({
              exerciseName: exercise.exerciseName,
              sets: exercise.sets,
              reps: exercise.reps,
              weightKg: exercise.weightKg,
            }))
          : [],
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

  function updateQuickLogExercise(index: number, field: keyof ExerciseFormState, value: string) {
    setQuickLogForm((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === index ? { ...exercise, [field]: value } : exercise,
      ),
    }))
  }

  function updateActiveExercise(index: number, field: keyof ExerciseFormState, value: string) {
    setActiveForm((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === index ? { ...exercise, [field]: value } : exercise,
      ),
    }))
  }

  function addQuickLogExercise() {
    setQuickLogForm((current) => ({
      ...current,
      exerciseEntries: [...current.exerciseEntries, createExerciseForm()],
    }))
  }

  function addActiveExercise() {
    setActiveForm((current) => ({
      ...current,
      exerciseEntries: [...current.exerciseEntries, createExerciseForm()],
    }))
  }

  function removeQuickLogExercise(index: number) {
    setQuickLogForm((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.filter((_, currentIndex) => currentIndex !== index),
    }))
  }

  function removeActiveExercise(index: number) {
    setActiveForm((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.filter((_, currentIndex) => currentIndex !== index),
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
            Run an active workout session, quick-log completed sessions, reuse templates, and keep the full history intact.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">Latest</span>
            <strong>{stats.latestWorkout ? formatDate(stats.latestWorkout.date) : 'No data'}</strong>
            <span className="stat-subtext">
              {stats.latestWorkout
                ? `${stats.latestWorkout.exerciseEntries.length} exercises logged`
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
            <span className="stat-subtext">Entries across all workouts</span>
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
              <div className="session-banner">
                <span className="pr-badge">In Progress</span>
                <span className="record-hint">Started on {formatDate(activeSession.startedAtUtc)}</span>
              </div>

              <div className="weight-form">
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
                  {activeErrors.notes ? <small className="field-error">{activeErrors.notes}</small> : null}
                </label>

                <div className="exercise-builder">
                  <div className="section-title-row">
                    <div>
                      <h3>Session exercises</h3>
                      <p>Add movements as you work through the session.</p>
                    </div>
                    <button type="button" className="ghost-button" onClick={addActiveExercise}>
                      Add exercise
                    </button>
                  </div>

                  {activeErrors.exerciseEntries ? (
                    <small className="field-error">{activeErrors.exerciseEntries}</small>
                  ) : null}

                  {activeForm.exerciseEntries.length === 0 ? (
                    <StateCard
                      title="No exercises yet"
                      description="Add your first exercise to start building this active session."
                    />
                  ) : (
                    <div className="exercise-list">
                      {activeForm.exerciseEntries.map((exercise, index) => (
                        <ExerciseEditorCard
                          key={`active-${index}`}
                          title={`Exercise ${index + 1}`}
                          exercise={exercise}
                          errors={activeErrors.exercises[index]}
                          workouts={workouts}
                          onChange={(field, value) => updateActiveExercise(index, field, value)}
                          onRemove={
                            activeForm.exerciseEntries.length > 0
                              ? () => removeActiveExercise(index)
                              : undefined
                          }
                        />
                      ))}
                    </div>
                  )}
                </div>

                <div className="action-row">
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
            <div className="start-workout-stack">
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
                          <span>
                            {exercise.sets} x {exercise.reps} at {exercise.weightKg} kg
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>

                  <div className="action-row">
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

          <div className="template-toolbar">
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

            <button
              type="button"
              className="ghost-button"
              onClick={() => void handleSaveTemplate()}
              disabled={isSavingTemplate}
            >
              {isSavingTemplate ? 'Saving...' : 'Save as template'}
            </button>
          </div>

          <form className="weight-form" onSubmit={handleQuickLogSubmit} noValidate>
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
              {quickLogErrors.notes ? <small className="field-error">{quickLogErrors.notes}</small> : null}
            </label>

            <div className="exercise-builder">
              <div className="section-title-row">
                <div>
                  <h3>Exercises</h3>
                  <p>Add each movement in the order you performed it.</p>
                </div>
                <button type="button" className="ghost-button" onClick={addQuickLogExercise}>
                  Add exercise
                </button>
              </div>

              {quickLogErrors.exerciseEntries ? (
                <small className="field-error">{quickLogErrors.exerciseEntries}</small>
              ) : null}

              <div className="exercise-list">
                {quickLogForm.exerciseEntries.map((exercise, index) => (
                  <ExerciseEditorCard
                    key={`quick-log-${index}`}
                    title={`Exercise ${index + 1}`}
                    exercise={exercise}
                    errors={quickLogErrors.exercises[index]}
                    workouts={workouts}
                    onChange={(field, value) => updateQuickLogExercise(index, field, value)}
                    onRemove={
                      quickLogForm.exerciseEntries.length > 1
                        ? () => removeQuickLogExercise(index)
                        : undefined
                    }
                  />
                ))}
              </div>
            </div>

            <button type="submit" className="primary-button" disabled={isSaving}>
              {isSaving ? 'Saving...' : 'Save workout'}
            </button>
          </form>
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Personal records</h2>
              <p>Highest recorded working weight for each exercise.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading personal records" description="Calculating your best lifts by exercise." loading />
          ) : personalRecords.length === 0 ? (
            <StateCard
              title="No personal records yet"
              description="Save a workout to establish your first recorded best."
            />
          ) : (
            <div className="records-list" role="list">
              {personalRecords.map((record) => (
                <article key={record.exerciseName} className="record-card" role="listitem">
                  <div>
                    <p className="entry-date">{record.exerciseName}</p>
                    <strong className="entry-weight">{record.weightKg} kg</strong>
                  </div>
                  <span className="record-date">Set on {formatDate(record.date)}</span>
                </article>
              ))}
            </div>
          )}
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

          <div className="filter-toolbar">
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
                      <strong className="entry-weight">{workout.exerciseEntries.length} exercises</strong>
                    </div>
                  </div>

                  {workout.notes ? <p className="workout-notes">{workout.notes}</p> : null}

                  <div className="exercise-summary-list">
                    {workout.exerciseEntries.map((exercise) => (
                      <div key={exercise.id} className="exercise-summary-item">
                        <div className="exercise-summary-copy">
                          <strong>{exercise.exerciseName}</strong>
                          <span>
                            {exercise.sets} x {exercise.reps} at {exercise.weightKg} kg
                          </span>
                        </div>
                        <div className="exercise-summary-meta">
                          {exercise.isPersonalRecord ? <span className="pr-badge">PR</span> : null}
                          <span className="record-hint">Best: {exercise.personalRecordWeightKg} kg</span>
                        </div>
                      </div>
                    ))}
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>
      </section>
    </main>
  )
}

function compareWorkouts(left: Workout, right: Workout) {
  return new Date(right.date).getTime() - new Date(left.date).getTime() || right.id - left.id
}

function ExerciseEditorCard({
  title,
  exercise,
  errors,
  workouts,
  onChange,
  onRemove,
}: {
  title: string
  exercise: ExerciseFormState
  errors?: ExerciseFieldErrors
  workouts: Workout[]
  onChange: (field: keyof ExerciseFormState, value: string) => void
  onRemove?: () => void
}) {
  const sets = !exercise.sets.trim() || Number.isNaN(Number(exercise.sets)) ? null : Number(exercise.sets)
  const reps = !exercise.reps.trim() || Number.isNaN(Number(exercise.reps)) ? null : Number(exercise.reps)

  return (
    <div className="exercise-card">
      <div className="exercise-card-header">
        <h3>{title}</h3>
        {onRemove ? (
          <button type="button" className="danger-button" onClick={onRemove}>
            Remove
          </button>
        ) : null}
      </div>

      {exercise.exerciseName.trim() ? (
        <ExerciseSuggestionNotice
          suggestion={getSuggestedNextWeight(workouts, exercise.exerciseName, sets, reps)}
        />
      ) : null}

      <div className="exercise-fields">
        <label className="field field-span-2">
          <span>Name</span>
          <input
            type="text"
            placeholder="Bench Press"
            value={exercise.exerciseName}
            onChange={(event) => onChange('exerciseName', event.target.value)}
            aria-invalid={Boolean(errors?.exerciseName)}
          />
          {errors?.exerciseName ? <small className="field-error">{errors.exerciseName}</small> : null}
        </label>

        <label className="field">
          <span>Sets</span>
          <input
            type="number"
            min="1"
            max="20"
            value={exercise.sets}
            onChange={(event) => onChange('sets', event.target.value)}
            aria-invalid={Boolean(errors?.sets)}
          />
          {errors?.sets ? <small className="field-error">{errors.sets}</small> : null}
        </label>

        <label className="field">
          <span>Reps</span>
          <input
            type="number"
            min="1"
            max="100"
            value={exercise.reps}
            onChange={(event) => onChange('reps', event.target.value)}
            aria-invalid={Boolean(errors?.reps)}
          />
          {errors?.reps ? <small className="field-error">{errors.reps}</small> : null}
        </label>

        <label className="field">
          <span>Weight (kg)</span>
          <input
            type="number"
            min="0"
            max="500"
            step="0.1"
            value={exercise.weightKg}
            onChange={(event) => onChange('weightKg', event.target.value)}
            aria-invalid={Boolean(errors?.weightKg)}
          />
          {errors?.weightKg ? <small className="field-error">{errors.weightKg}</small> : null}
        </label>
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
    </div>
  )
}
