import { apiClient, isNotFoundError } from '../lib/http'
import type {
  ActiveWorkoutSession,
  ActiveWorkoutSessionPayload,
  Workout,
  WorkoutPayload,
  WorkoutTemplate,
  WorkoutTemplatePayload,
} from '../types/workout'

export async function fetchWorkouts() {
  const response = await apiClient.get<Workout[]>('/Workouts')
  return response.data
}

export async function createWorkout(payload: WorkoutPayload) {
  const response = await apiClient.post<Workout>('/Workouts', payload)
  return response.data
}

export async function deleteWorkout(id: number) {
  await apiClient.delete(`/Workouts/${id}`)
}

export async function fetchWorkoutTemplates() {
  const response = await apiClient.get<WorkoutTemplate[]>('/WorkoutTemplates')
  return response.data
}

export async function createWorkoutTemplate(payload: WorkoutTemplatePayload) {
  const response = await apiClient.post<WorkoutTemplate>('/WorkoutTemplates', payload)
  return response.data
}

export async function fetchActiveWorkoutSession() {
  try {
    const response = await apiClient.get<ActiveWorkoutSession>('/ActiveWorkoutSession')
    return response.data
  } catch (error) {
    if (isNotFoundError(error)) {
      return null
    }

    throw error
  }
}

export async function startActiveWorkoutSession(payload: ActiveWorkoutSessionPayload) {
  const response = await apiClient.post<ActiveWorkoutSession>('/ActiveWorkoutSession', payload)
  return response.data
}

export async function updateActiveWorkoutSession(payload: ActiveWorkoutSessionPayload) {
  const response = await apiClient.put<ActiveWorkoutSession>('/ActiveWorkoutSession', payload)
  return response.data
}

export async function completeActiveWorkoutSession() {
  const response = await apiClient.post<Workout>('/ActiveWorkoutSession/complete')
  return response.data
}
