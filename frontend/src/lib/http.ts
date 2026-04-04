import axios from 'axios'

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5224/api',
})

export function isNotFoundError(error: unknown) {
  return axios.isAxiosError(error) && error.response?.status === 404
}

export function getRequestErrorMessage(error: unknown, fallbackMessage: string) {
  if (axios.isAxiosError(error)) {
    const apiMessage = error.response?.data?.message
    if (typeof apiMessage === 'string' && apiMessage.trim()) {
      return apiMessage
    }

    const apiTitle = error.response?.data?.title
    if (typeof apiTitle === 'string' && apiTitle.trim()) {
      return apiTitle
    }

    const apiErrors = error.response?.data?.errors

    if (apiErrors && typeof apiErrors === 'object') {
      const firstError = Object.values(apiErrors).flat().find(Boolean)
      if (typeof firstError === 'string') {
        return firstError
      }
    }
  }

  return fallbackMessage
}
