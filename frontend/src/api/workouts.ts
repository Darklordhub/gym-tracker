import axios from 'axios'
import type {
  Workout,
  WorkoutPayload,
  WorkoutTemplate,
  WorkoutTemplatePayload,
} from '../types/workout'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5224/api',
})

export async function fetchWorkouts() {
  const response = await api.get<Workout[]>('/Workouts')
  return response.data
}

export async function createWorkout(payload: WorkoutPayload) {
  const response = await api.post<Workout>('/Workouts', payload)
  return response.data
}

export async function fetchWorkoutTemplates() {
  const response = await api.get<WorkoutTemplate[]>('/WorkoutTemplates')
  return response.data
}

export async function createWorkoutTemplate(payload: WorkoutTemplatePayload) {
  const response = await api.post<WorkoutTemplate>('/WorkoutTemplates', payload)
  return response.data
}
