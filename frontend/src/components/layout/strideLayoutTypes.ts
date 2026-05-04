import type { ReactNode } from 'react'

export type ThemeMode = 'light' | 'dark'

export type IconName =
  | 'dashboard'
  | 'weight'
  | 'workouts'
  | 'aiWorkout'
  | 'progress'
  | 'nutrition'
  | 'cycle'
  | 'profile'
  | 'admin'
  | 'library'
  | 'moon'
  | 'sun'
  | 'logout'

export type NavItem = {
  to: string
  label: string
  icon: IconName
}

export type StrideSidebarNavItem = {
  to: string
  label: string
  icon: ReactNode
  meta: string
}

export type StrideTopbarMeta = {
  title: string
  eyebrow: string
  description: string
}
