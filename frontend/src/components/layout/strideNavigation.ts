import type { NavItem } from './strideLayoutTypes'

export const primaryNavItems: readonly NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
  { to: '/weight', label: 'Weight', icon: 'weight' },
  { to: '/workouts', label: 'Workouts', icon: 'workouts' },
  { to: '/ai-workout-generator', label: 'AI Workout Generator', icon: 'aiWorkout' },
  { to: '/nutrition', label: 'Nutrition', icon: 'nutrition' },
  { to: '/exercise-library', label: 'Exercise Library', icon: 'library' },
  { to: '/exercise-progress', label: 'Exercise Progress', icon: 'progress' },
  { to: '/cycle', label: 'Cycle', icon: 'cycle' },
  { to: '/profile', label: 'Profile', icon: 'profile' },
] as const

export const adminNavItems: readonly NavItem[] = [
  { to: '/admin', label: 'Admin', icon: 'admin' },
] as const

export function getNavMeta(pathname: string) {
  switch (pathname) {
    case '/dashboard':
      return 'Overview'
    case '/weight':
      return 'Body metrics'
    case '/workouts':
      return 'Session logs'
    case '/ai-workout-generator':
      return 'Generator'
    case '/nutrition':
      return 'Meals'
    case '/exercise-library':
      return 'Catalog'
    case '/exercise-progress':
      return 'Lift history'
    case '/cycle':
      return 'Readiness'
    case '/profile':
      return 'Account'
    case '/admin':
      return 'Controls'
    default:
      return 'Workspace'
  }
}
