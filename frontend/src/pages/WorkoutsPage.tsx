import { useEffect, useMemo, useState } from 'react'
import {
  createWorkout,
  createWorkoutTemplate,
  fetchWorkouts,
  fetchWorkoutTemplates,
} from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getSuggestedNextWeight } from '../lib/exerciseSuggestions'
import { getRequestErrorMessage } from '../lib/http'
import type { Workout, WorkoutTemplate } from '../types/workout'

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

const initialFormState = (): WorkoutFormState => ({
  date: new Date().toISOString().slice(0, 10),
  notes: '',
  exerciseEntries: [createExerciseForm()],
})

function validateWorkoutForm(form: WorkoutFormState): WorkoutFormErrors {
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

  if (!form.date) {
    errors.date = 'Date is required.'
  } else if (form.date > new Date().toISOString().slice(0, 10)) {
    errors.date = 'Date cannot be in the future.'
  }

  if (form.notes.length > 500) {
    errors.notes = 'Notes must be 500 characters or less.'
  }

  if (form.exerciseEntries.length === 0) {
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

export function WorkoutsPage() {
  const [workouts, setWorkouts] = useState<Workout[]>([])
  const [templates, setTemplates] = useState<WorkoutTemplate[]>([])
  const [form, setForm] = useState<WorkoutFormState>(initialFormState)
  const [errors, setErrors] = useState<WorkoutFormErrors>({ exercises: [] })
  const [workoutSearch, setWorkoutSearch] = useState('')
  const [workoutDateFrom, setWorkoutDateFrom] = useState('')
  const [workoutDateTo, setWorkoutDateTo] = useState('')
  const [templateName, setTemplateName] = useState('')
  const [templateErrors, setTemplateErrors] = useState<TemplateFormErrors>({})
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [isSavingTemplate, setIsSavingTemplate] = useState(false)
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
      const [workoutData, templateData] = await Promise.all([
        fetchWorkouts(),
        fetchWorkoutTemplates(),
      ])
      setWorkouts(workoutData)
      setTemplates(templateData)
    } catch {
      setErrorMessage('Unable to load workouts and templates. Check that the API is running.')
    } finally {
      setIsLoading(false)
    }
  }

  function resetForm() {
    setForm(initialFormState())
    setErrors({ exercises: [] })
  }

  function applyTemplate(template: WorkoutTemplate) {
    setForm({
      date: new Date().toISOString().slice(0, 10),
      notes: template.notes,
      exerciseEntries: template.exerciseEntries.map((exercise) => ({
        exerciseName: exercise.exerciseName,
        sets: exercise.sets.toString(),
        reps: exercise.reps.toString(),
        weightKg: exercise.weightKg.toString(),
      })),
    })
    setErrors({ exercises: [] })
    setFeedback(`Started a new workout from "${template.name}".`)
    setErrorMessage(null)
  }

  function updateExercise(index: number, field: keyof ExerciseFormState, value: string) {
    setForm((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.map((exercise, currentIndex) =>
        currentIndex === index ? { ...exercise, [field]: value } : exercise,
      ),
    }))
  }

  function addExercise() {
    setForm((current) => ({
      ...current,
      exerciseEntries: [...current.exerciseEntries, createExerciseForm()],
    }))
  }

  function removeExercise(index: number) {
    setForm((current) => ({
      ...current,
      exerciseEntries: current.exerciseEntries.filter((_, currentIndex) => currentIndex !== index),
    }))
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const nextErrors = validateWorkoutForm(form)
    setErrors(nextErrors)
    setFeedback(null)
    setErrorMessage(null)

    if (hasWorkoutErrors(nextErrors)) {
      return
    }

    const payload = {
      date: form.date,
      notes: form.notes.trim(),
      exerciseEntries: form.exerciseEntries.map((exercise) => ({
        exerciseName: exercise.exerciseName.trim(),
        sets: Number(exercise.sets),
        reps: Number(exercise.reps),
        weightKg: Number(exercise.weightKg),
      })),
    }

    try {
      setIsSaving(true)
      const createdWorkout = await createWorkout(payload)
      setWorkouts((current) => [createdWorkout, ...current].sort(compareWorkouts))
      setFeedback('Workout saved.')
      resetForm()
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

    const nextErrors = validateWorkoutForm(form)
    setErrors(nextErrors)
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
        notes: form.notes.trim(),
        exerciseEntries: form.exerciseEntries.map((exercise) => ({
          exerciseName: exercise.exerciseName.trim(),
          sets: Number(exercise.sets),
          reps: Number(exercise.reps),
          weightKg: Number(exercise.weightKg),
        })),
      })

      setTemplates((current) =>
        [...current, createdTemplate].sort((left, right) => left.name.localeCompare(right.name) || left.id - right.id),
      )
      setTemplateName('')
      setFeedback(`Template "${createdTemplate.name}" saved.`)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to save this template.'))
    } finally {
      setIsSavingTemplate(false)
    }
  }

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Gym Tracker</span>
          <h1>Workouts</h1>
          <p className="hero-text">
            Build one workout at a time, stack multiple exercises into it, and keep the session details together.
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
        </div>
      </section>

      <section className="content-grid workout-grid">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Add workout</h2>
              <p>Create a workout, or start from a saved template before saving.</p>
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

          <form className="weight-form" onSubmit={handleSubmit} noValidate>
            <label className="field">
              <span>Date</span>
              <input
                type="date"
                value={form.date}
                onChange={(event) => setForm((current) => ({ ...current, date: event.target.value }))}
                aria-invalid={Boolean(errors.date)}
              />
              {errors.date ? <small className="field-error">{errors.date}</small> : null}
            </label>

            <label className="field">
              <span>Notes</span>
              <textarea
                className="text-area"
                rows={4}
                maxLength={500}
                placeholder="Optional notes for this session"
                value={form.notes}
                onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))}
                aria-invalid={Boolean(errors.notes)}
              />
              {errors.notes ? <small className="field-error">{errors.notes}</small> : null}
            </label>

            <div className="exercise-builder">
              <div className="section-title-row">
                <div>
                  <h3>Exercises</h3>
                  <p>Add each movement in the order you performed it.</p>
                </div>
                <button type="button" className="ghost-button" onClick={addExercise}>
                  Add exercise
                </button>
              </div>

              {errors.exerciseEntries ? <small className="field-error">{errors.exerciseEntries}</small> : null}

              <div className="exercise-list">
                {form.exerciseEntries.map((exercise, index) => (
                  <div key={index} className="exercise-card">
                    <div className="exercise-card-header">
                      <h3>Exercise {index + 1}</h3>
                      {form.exerciseEntries.length > 1 ? (
                        <button
                          type="button"
                          className="danger-button"
                          onClick={() => removeExercise(index)}
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
                          Number.isNaN(Number(exercise.sets)) || !exercise.sets.trim()
                            ? null
                            : Number(exercise.sets),
                          Number.isNaN(Number(exercise.reps)) || !exercise.reps.trim()
                            ? null
                            : Number(exercise.reps),
                        )}
                      />
                    ) : null}

                    <div className="exercise-fields">
                      <label className="field field-span-2">
                        <span>Name</span>
                        <input
                          type="text"
                          placeholder="Bench Press"
                          value={exercise.exerciseName}
                          onChange={(event) => updateExercise(index, 'exerciseName', event.target.value)}
                          aria-invalid={Boolean(errors.exercises[index]?.exerciseName)}
                        />
                        {errors.exercises[index]?.exerciseName ? (
                          <small className="field-error">{errors.exercises[index]?.exerciseName}</small>
                        ) : null}
                      </label>

                      <label className="field">
                        <span>Sets</span>
                        <input
                          type="number"
                          min="1"
                          max="20"
                          value={exercise.sets}
                          onChange={(event) => updateExercise(index, 'sets', event.target.value)}
                          aria-invalid={Boolean(errors.exercises[index]?.sets)}
                        />
                        {errors.exercises[index]?.sets ? (
                          <small className="field-error">{errors.exercises[index]?.sets}</small>
                        ) : null}
                      </label>

                      <label className="field">
                        <span>Reps</span>
                        <input
                          type="number"
                          min="1"
                          max="100"
                          value={exercise.reps}
                          onChange={(event) => updateExercise(index, 'reps', event.target.value)}
                          aria-invalid={Boolean(errors.exercises[index]?.reps)}
                        />
                        {errors.exercises[index]?.reps ? (
                          <small className="field-error">{errors.exercises[index]?.reps}</small>
                        ) : null}
                      </label>

                      <label className="field">
                        <span>Weight (kg)</span>
                        <input
                          type="number"
                          min="0"
                          max="500"
                          step="0.1"
                          value={exercise.weightKg}
                          onChange={(event) => updateExercise(index, 'weightKg', event.target.value)}
                          aria-invalid={Boolean(errors.exercises[index]?.weightKg)}
                        />
                        {errors.exercises[index]?.weightKg ? (
                          <small className="field-error">{errors.exercises[index]?.weightKg}</small>
                        ) : null}
                      </label>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <button type="submit" className="primary-button" disabled={isSaving}>
              {isSaving ? 'Saving...' : 'Save workout'}
            </button>
          </form>

          {feedback ? <p className="feedback success">{feedback}</p> : null}
          {errorMessage ? <p className="feedback error">{errorMessage}</p> : null}
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Templates</h2>
              <p>Reusable workout structures you can apply to a new session.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading templates" description="Fetching your saved workout structures." loading />
          ) : templates.length === 0 ? (
            <StateCard
              title="No templates yet"
              description="Save your current workout structure to reuse it later."
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
                    <button type="button" className="primary-button" onClick={() => applyTemplate(template)}>
                      Use template
                    </button>
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
                          <span className="record-hint">
                            Best: {exercise.personalRecordWeightKg} kg
                          </span>
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
