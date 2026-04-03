import axios from 'axios'
import type { Workout, WorkoutPayload } from '../types/workout'

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
