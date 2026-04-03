import { useEffect, useMemo, useState } from 'react'
import { createWorkout, fetchWorkouts } from '../api/workouts'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { Workout } from '../types/workout'

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
  const [form, setForm] = useState<WorkoutFormState>(initialFormState)
  const [errors, setErrors] = useState<WorkoutFormErrors>({ exercises: [] })
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadWorkouts()
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

  async function loadWorkouts() {
    try {
      setIsLoading(true)
      setErrorMessage(null)
      const data = await fetchWorkouts()
      setWorkouts(data)
    } catch {
      setErrorMessage('Unable to load workouts. Check that the API is running.')
    } finally {
      setIsLoading(false)
    }
  }

  function resetForm() {
    setForm(initialFormState())
    setErrors({ exercises: [] })
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
              <p>Create a workout, then add as many exercises as you need before saving.</p>
            </div>
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
              <h2>Recent workouts</h2>
              <p>Newest sessions first.</p>
            </div>
          </div>

          {isLoading ? (
            <div className="empty-state">Loading workouts...</div>
          ) : workouts.length === 0 ? (
            <div className="empty-state">No workouts yet. Save one with at least one exercise.</div>
          ) : (
            <div className="workout-list" role="list">
              {workouts.map((workout) => (
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
                        <strong>{exercise.exerciseName}</strong>
                        <span>
                          {exercise.sets} x {exercise.reps} at {exercise.weightKg} kg
                        </span>
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
